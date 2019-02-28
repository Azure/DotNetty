// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Channel.Sockets
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Tests.Common;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit;
    using Xunit.Abstractions;

    [Collection("UDP Transport Tests")]
    public class SocketDatagramChannelUnicastTest : TestBase
    {
        const int DefaultTimeOutInMilliseconds = 800;

        public SocketDatagramChannelUnicastTest(ITestOutputHelper output)
            : base(output)
        {
        }

        class TestHandler : SimpleChannelInboundHandler<DatagramPacket>
        {
            readonly byte[] expectedData;
            readonly ManualResetEventSlim resetEvent;
            bool sequenceEqual;

            public TestHandler(byte[] expectedData)
            {
                this.expectedData = expectedData;
                this.resetEvent = new ManualResetEventSlim(false);
                this.sequenceEqual = false;
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, DatagramPacket msg)
            {
                IByteBuffer buffer = msg.Content;
                try
                {
                    this.sequenceEqual = this.expectedData.Length == buffer.ReadableBytes;
                    if (!this.sequenceEqual)
                    {
                        return;
                    }

                    foreach (byte expectedByte in this.expectedData)
                    {
                        if (expectedByte != buffer.ReadByte())
                        {
                            this.sequenceEqual = false;
                            break;
                        }
                    }
                }
                finally
                {
                    this.resetEvent.Set();
                }
            }

            public bool WaitForResult()
            {
                bool result = false;

                try
                {
                    if (this.resetEvent.Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds)))
                    {
                        result = this.sequenceEqual;
                    }
                }
                finally
                {
                    this.resetEvent.Reset();
                    this.sequenceEqual = false;
                }

                return result;
            }
        }

        static readonly byte[] Data = { 0, 1, 2, 3 };
        static readonly bool[] BindClientOption = { true, false };

        public static IEnumerable<object[]> GetData()
        {
            foreach (AddressFamily addressFamily in NetUtil.AddressFamilyTypes)
            {
                foreach (IByteBufferAllocator allocator in NetUtil.Allocators)
                {
                    foreach (bool bindClient in BindClientOption)
                    {
                        yield return new object[]
                        {
                                Unpooled.Buffer().WriteBytes(Data),
                                bindClient,
                                allocator,
                                addressFamily,
                                Data,
                                1
                        };

                        yield return new object[]
                        {
                                Unpooled.Buffer().WriteBytes(Data),
                                bindClient,
                                allocator,
                                addressFamily,
                                Data,
                                4
                        };

                        yield return new object[]
                        {
                               Unpooled.WrappedBuffer(
                                   Unpooled.CopiedBuffer(Data, 0, 2), Unpooled.CopiedBuffer(Data, 2, 2)),
                                bindClient,
                                allocator,
                                addressFamily,
                                Data,
                                1
                        };

                        yield return new object[]
                        {
                                Unpooled.WrappedBuffer(
                                    Unpooled.CopiedBuffer(Data, 0, 2), Unpooled.CopiedBuffer(Data, 2, 2)),
                                bindClient,
                                allocator,
                                addressFamily,
                                Data,
                                4
                        };
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(GetData))]
        public void SimpleSend(IByteBuffer source, bool bindClient, IByteBufferAllocator allocator, AddressFamily addressFamily, byte[] expectedData, int count)
        {
            SocketDatagramChannel serverChannel = null;
            IChannel clientChannel = null;
            var serverGroup = new MultithreadEventLoopGroup(1);
            var clientGroup = new MultithreadEventLoopGroup(1);
            try
            {
                var handler = new TestHandler(expectedData);
                var serverBootstrap = new Bootstrap();
                serverBootstrap
                    .Group(serverGroup)
                    .ChannelFactory(() => new SocketDatagramChannel(addressFamily))
                    .Option(ChannelOption.Allocator, allocator)
                    .Option(ChannelOption.SoBroadcast, true)
                    .Option(ChannelOption.IpMulticastLoopDisabled, false)
                    .Handler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        channel.Pipeline.AddLast(nameof(SocketDatagramChannelUnicastTest), handler);
                    }));

                IPAddress address = NetUtil.GetLoopbackAddress(addressFamily);
                this.Output.WriteLine($"Unicast server binding to:({addressFamily}){address}");
                Task<IChannel> task = serverBootstrap.BindAsync(address, IPEndPoint.MinPort);

                Assert.True(task.Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds * 5)),
                    $"Unicast server binding to:({addressFamily}){address} timed out!");

                serverChannel = (SocketDatagramChannel)task.Result;
                var endPoint = (IPEndPoint)serverChannel.LocalAddress;

                var clientBootstrap = new Bootstrap();
                clientBootstrap
                    .Group(clientGroup)
                    .ChannelFactory(() => new SocketDatagramChannel(addressFamily))
                    .Option(ChannelOption.Allocator, allocator)
                    .Option(ChannelOption.SoBroadcast, true)
                    .Option(ChannelOption.IpMulticastLoopDisabled, false)
                    .Handler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        channel.Pipeline.AddLast("Dummy", new NetUtil.DummyHandler());
                    }));

                var clientEndPoint = new IPEndPoint(
                    addressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any,
                    IPEndPoint.MinPort);

                clientBootstrap
                    .LocalAddress(clientEndPoint)
                    .RemoteAddress(new IPEndPoint(address, endPoint.Port));

                if (bindClient)
                {
                    this.Output.WriteLine($"Unicast client binding to:({addressFamily}){address}");
                    task = clientBootstrap.BindAsync(clientEndPoint);

                    Assert.True(task.Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds * 5)),
                        $"Unicast client binding to:({clientEndPoint}) timed out!");
                    clientChannel = task.Result;
                }
                else
                {
                    this.Output.WriteLine($"Register client binding to:({addressFamily}){address}");
                    task = (Task<IChannel>)clientBootstrap.RegisterAsync();
                    Assert.True(task.Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds)), "Unicast client register timed out!");
                    clientChannel = task.Result;
                }

                for (int i = 0; i < count; i++)
                {
                    var packet = new DatagramPacket((IByteBuffer)source.Retain(), new IPEndPoint(address, endPoint.Port));
                    clientChannel.WriteAndFlushAsync(packet).Wait();
                    Assert.True(handler.WaitForResult());

                    var duplicatedPacket = (DatagramPacket)packet.Duplicate();
                    duplicatedPacket.Retain();
                    clientChannel.WriteAndFlushAsync(duplicatedPacket).Wait();
                    Assert.True(handler.WaitForResult());
                }
            }
            finally
            {
                serverChannel?.CloseAsync().Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds));
                clientChannel?.CloseAsync().Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds));

                source.Release();
                Task.WaitAll(
                    serverGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
                    clientGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)));
            }
        }
    }
}