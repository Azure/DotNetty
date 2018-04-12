// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Tests.End2End
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Codecs.Mqtt;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Common.Concurrency;
    using DotNetty.Handlers.Logging;
    using DotNetty.Handlers.Tls;
    using DotNetty.Tests.Common;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit;
    using Xunit.Abstractions;

    public class End2EndTests : TestBase
    {
        static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(10);
        static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
        const int Port = 8009;

        public End2EndTests(ITestOutputHelper output)
            : base(output)
        {
        }

        const string ClientId = "scenarioClient1";
        const string SubscribeTopicFilter1 = "test/+";
        const string SubscribeTopicFilter2 = "test2/#";
        const string PublishC2STopic = "loopback/qosZero";
        const string PublishC2SQos0Payload = "C->S, QoS 0 test.";
        const string PublishC2SQos1Topic = "loopback2/qos/One";
        const string PublishC2SQos1Payload = "C->S, QoS 1 test. Different data length.";
        const string PublishS2CQos1Topic = "test2/scenarioClient1/special/qos/One";
        const string PublishS2CQos1Payload = "S->C, QoS 1 test. Different data length #2.";

        [Fact]
        public async Task EchoServerAndClient()
        {
            var testPromise = new TaskCompletionSource();
            var tlsCertificate = TestResourceHelper.GetTestCertificate();
            Func<Task> closeServerFunc = await this.StartServerAsync(true, ch =>
            {
                ch.Pipeline.AddLast("server logger", new LoggingHandler("SERVER"));
                ch.Pipeline.AddLast("server tls", TlsHandler.Server(tlsCertificate));
                ch.Pipeline.AddLast("server logger2", new LoggingHandler("SER***"));
                ch.Pipeline.AddLast("server prepender", new LengthFieldPrepender(2));
                ch.Pipeline.AddLast("server decoder", new LengthFieldBasedFrameDecoder(ushort.MaxValue, 0, 2, 0, 2));
                ch.Pipeline.AddLast(new EchoChannelHandler());
            }, testPromise);

            var group = new MultithreadEventLoopGroup();
            var readListener = new ReadListeningHandler(DefaultTimeout);
            Bootstrap b = new Bootstrap()
                .Group(group)
                .Channel<TcpSocketChannel>()
                .Option(ChannelOption.TcpNodelay, true)
                .Handler(new ActionChannelInitializer<ISocketChannel>(ch =>
                {
                    string targetHost = tlsCertificate.GetNameInfo(X509NameType.DnsName, false);
                    var clientTlsSettings = new ClientTlsSettings(targetHost);
                    ch.Pipeline.AddLast("client logger", new LoggingHandler("CLIENT"));
                    ch.Pipeline.AddLast("client tls", new TlsHandler(stream => new SslStream(stream, true, (sender, certificate, chain, errors) => true), clientTlsSettings));
                    ch.Pipeline.AddLast("client logger2", new LoggingHandler("CLI***"));
                    ch.Pipeline.AddLast("client prepender", new LengthFieldPrepender(2));
                    ch.Pipeline.AddLast("client decoder", new LengthFieldBasedFrameDecoder(ushort.MaxValue, 0, 2, 0, 2));
                    ch.Pipeline.AddLast(readListener);
                }));

            this.Output.WriteLine("Configured Bootstrap: {0}", b);

            IChannel clientChannel = null;
            try
            {
                clientChannel = await b.ConnectAsync(IPAddress.Loopback, Port);

                this.Output.WriteLine("Connected channel: {0}", clientChannel);

                string[] messages = { "message 1", string.Join(",", Enumerable.Range(1, 300)) };
                foreach (string message in messages)
                {
                    await clientChannel.WriteAndFlushAsync(Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes(message))).WithTimeout(DefaultTimeout);

                    var responseMessage = Assert.IsAssignableFrom<IByteBuffer>(await readListener.ReceiveAsync());
                    Assert.Equal(message, responseMessage.ToString(Encoding.UTF8));
                }

                testPromise.TryComplete();
                await testPromise.Task.WithTimeout(TimeSpan.FromSeconds(30));
            }
            finally
            {
                Task serverCloseTask = closeServerFunc();
                clientChannel?.CloseAsync().Wait(TimeSpan.FromSeconds(5));
                group.ShutdownGracefullyAsync().Wait(TimeSpan.FromSeconds(5));
                if (!serverCloseTask.Wait(ShutdownTimeout))
                {
                    this.Output.WriteLine("Didn't stop in time.");
                }
            }
        }

        [Fact]
        public async Task MqttServerAndClient()
        {
            var testPromise = new TaskCompletionSource();

            var tlsCertificate = TestResourceHelper.GetTestCertificate();
            var serverReadListener = new ReadListeningHandler();
            IChannel serverChannel = null;
            Func<Task> closeServerFunc = await this.StartServerAsync(true, ch =>
            {
                serverChannel = ch;
                ch.Pipeline.AddLast("server logger", new LoggingHandler("SERVER"));
                ch.Pipeline.AddLast("server tls", TlsHandler.Server(tlsCertificate));
                ch.Pipeline.AddLast("server logger2", new LoggingHandler("SER***"));
                ch.Pipeline.AddLast(
                    MqttEncoder.Instance,
                    new MqttDecoder(true, 256 * 1024),
                    serverReadListener);
            }, testPromise);

            var group = new MultithreadEventLoopGroup();
            var clientReadListener = new ReadListeningHandler();
            Bootstrap b = new Bootstrap()
                .Group(group)
                .Channel<TcpSocketChannel>()
                .Option(ChannelOption.TcpNodelay, true)
                .Handler(new ActionChannelInitializer<ISocketChannel>(ch =>
                {
                    string targetHost = tlsCertificate.GetNameInfo(X509NameType.DnsName, false);
                    var clientTlsSettings = new ClientTlsSettings(targetHost);

                    ch.Pipeline.AddLast("client logger", new LoggingHandler("CLIENT"));
                    ch.Pipeline.AddLast("client tls", new TlsHandler(stream => new SslStream(stream, true, (sender, certificate, chain, errors) => true), clientTlsSettings));
                    ch.Pipeline.AddLast("client logger2", new LoggingHandler("CLI***"));
                    ch.Pipeline.AddLast(
                        MqttEncoder.Instance,
                        new MqttDecoder(false, 256 * 1024),
                        clientReadListener);
                }));

            this.Output.WriteLine("Configured Bootstrap: {0}", b);

            IChannel clientChannel = null;
            try
            {
                clientChannel = await b.ConnectAsync(IPAddress.Loopback, Port);

                this.Output.WriteLine("Connected channel: {0}", clientChannel);

                await Task.WhenAll(this.RunMqttClientScenarioAsync(clientChannel, clientReadListener), this.RunMqttServerScenarioAsync(serverChannel, serverReadListener))
                    .WithTimeout(TimeSpan.FromSeconds(30));

                testPromise.TryComplete();
                await testPromise.Task;
            }
            finally
            {
                Task serverCloseTask = closeServerFunc();
                clientChannel?.CloseAsync().Wait(TimeSpan.FromSeconds(5));
                group.ShutdownGracefullyAsync().Wait(TimeSpan.FromSeconds(5));
                if (!serverCloseTask.Wait(ShutdownTimeout))
                {
                    this.Output.WriteLine("Didn't stop in time.");
                }
            }
        }

        async Task RunMqttClientScenarioAsync(IChannel channel, ReadListeningHandler readListener)
        {
            await channel.WriteAndFlushAsync(new ConnectPacket
            {
                ClientId = ClientId,
                Username = "testuser",
                Password = "notsafe",
                WillTopicName = "last/word",
                WillMessage = Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes("oops"))
            });

            var connAckPacket = Assert.IsType<ConnAckPacket>(await readListener.ReceiveAsync());
            Assert.Equal(ConnectReturnCode.Accepted, connAckPacket.ReturnCode);

            int subscribePacketId = GetRandomPacketId();
            int unsubscribePacketId = GetRandomPacketId();
            await channel.WriteAndFlushManyAsync(
                new SubscribePacket(subscribePacketId,
                    new SubscriptionRequest(SubscribeTopicFilter1, QualityOfService.ExactlyOnce),
                    new SubscriptionRequest(SubscribeTopicFilter2, QualityOfService.AtLeastOnce),
                    new SubscriptionRequest("for/unsubscribe", QualityOfService.AtMostOnce)),
                new UnsubscribePacket(unsubscribePacketId, "for/unsubscribe"));

            var subAckPacket = Assert.IsType<SubAckPacket>(await readListener.ReceiveAsync());
            Assert.Equal(subscribePacketId, subAckPacket.PacketId);
            Assert.Equal(3, subAckPacket.ReturnCodes.Count);
            Assert.Equal(QualityOfService.ExactlyOnce, subAckPacket.ReturnCodes[0]);
            Assert.Equal(QualityOfService.AtLeastOnce, subAckPacket.ReturnCodes[1]);
            Assert.Equal(QualityOfService.AtMostOnce, subAckPacket.ReturnCodes[2]);
            
            var unsubAckPacket = Assert.IsType<UnsubAckPacket>(await readListener.ReceiveAsync());
            Assert.Equal(unsubscribePacketId, unsubAckPacket.PacketId);

            int publishQoS1PacketId = GetRandomPacketId();
            await channel.WriteAndFlushManyAsync(
                new PublishPacket(QualityOfService.AtMostOnce, false, false)
                {
                    TopicName = PublishC2STopic,
                    Payload = Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes(PublishC2SQos0Payload))
                },
                new PublishPacket(QualityOfService.AtLeastOnce, false, false)
                {
                    PacketId = publishQoS1PacketId,
                    TopicName = PublishC2SQos1Topic,
                    Payload = Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes(PublishC2SQos1Payload))
                });
            //new PublishPacket(QualityOfService.AtLeastOnce, false, false) { TopicName = "feedback/qos/One", Payload = Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes("QoS 1 test. Different data length.")) });

            var pubAckPacket = Assert.IsType<PubAckPacket>(await readListener.ReceiveAsync());
            Assert.Equal(publishQoS1PacketId, pubAckPacket.PacketId);

            var publishPacket = Assert.IsType<PublishPacket>(await readListener.ReceiveAsync());
            Assert.Equal(QualityOfService.AtLeastOnce, publishPacket.QualityOfService);
            Assert.Equal(PublishS2CQos1Topic, publishPacket.TopicName);
            Assert.Equal(PublishS2CQos1Payload, publishPacket.Payload.ToString(Encoding.UTF8));

            await channel.WriteAndFlushManyAsync(
                PubAckPacket.InResponseTo(publishPacket),
                DisconnectPacket.Instance);
        }

        async Task RunMqttServerScenarioAsync(IChannel channel, ReadListeningHandler readListener)
        {
            var connectPacket = Assert.IsType<ConnectPacket>(await readListener.ReceiveAsync());
            // todo verify

            await channel.WriteAndFlushAsync(new ConnAckPacket
            {
                ReturnCode = ConnectReturnCode.Accepted,
                SessionPresent = true
            });

            var subscribePacket = Assert.IsType<SubscribePacket>(await readListener.ReceiveAsync());
            // todo verify

            await channel.WriteAndFlushAsync(SubAckPacket.InResponseTo(subscribePacket, QualityOfService.ExactlyOnce));

            var unsubscribePacket = Assert.IsType<UnsubscribePacket>(await readListener.ReceiveAsync());
            // todo verify

            await channel.WriteAndFlushAsync(UnsubAckPacket.InResponseTo(unsubscribePacket));

            var publishQos0Packet = Assert.IsType<PublishPacket>(await readListener.ReceiveAsync());
            // todo verify

            var publishQos1Packet = Assert.IsType<PublishPacket>(await readListener.ReceiveAsync());
            // todo verify

            int publishQos1PacketId = GetRandomPacketId();
            await channel.WriteAndFlushManyAsync(
                PubAckPacket.InResponseTo(publishQos1Packet),
                new PublishPacket(QualityOfService.AtLeastOnce, false, false)
                {
                    PacketId = publishQos1PacketId,
                    TopicName = PublishS2CQos1Topic,
                    Payload = Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes(PublishS2CQos1Payload))
                });

            var pubAckPacket = Assert.IsType<PubAckPacket>(await readListener.ReceiveAsync());
            Assert.Equal(publishQos1PacketId, pubAckPacket.PacketId);

            var disconnectPacket = Assert.IsType<DisconnectPacket>(await readListener.ReceiveAsync());
        }

        static int GetRandomPacketId() => Guid.NewGuid().GetHashCode() & ushort.MaxValue;

        /// <summary>
        ///     Starts Echo server.
        /// </summary>
        /// <returns>function to trigger closure of the server.</returns>
        async Task<Func<Task>> StartServerAsync(bool tcpNoDelay, Action<IChannel> childHandlerSetupAction, TaskCompletionSource testPromise)
        {
            var bossGroup = new MultithreadEventLoopGroup(1);
            var workerGroup = new MultithreadEventLoopGroup();
            bool started = false;
            try
            {
                ServerBootstrap b = new ServerBootstrap()
                    .Group(bossGroup, workerGroup)
                    .Channel<TcpServerSocketChannel>()
                    .Handler(new ExceptionCatchHandler(ex => testPromise.TrySetException(ex)))
                    .ChildHandler(new ActionChannelInitializer<ISocketChannel>(childHandlerSetupAction))
                    .ChildOption(ChannelOption.TcpNodelay, tcpNoDelay);

                this.Output.WriteLine("Configured ServerBootstrap: {0}", b);

                IChannel serverChannel = await b.BindAsync(Port);

                this.Output.WriteLine("Bound server channel: {0}", serverChannel);

                started = true;

                return async () =>
                {
                    try
                    {
                        await serverChannel.CloseAsync();
                    }
                    finally
                    {
                        bossGroup.ShutdownGracefullyAsync();
                        workerGroup.ShutdownGracefullyAsync();
                    }
                };
            }
            finally
            {
                if (!started)
                {
                    bossGroup.ShutdownGracefullyAsync();
                    workerGroup.ShutdownGracefullyAsync();
                }
            }
        }
    }
}