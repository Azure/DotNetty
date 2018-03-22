// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tests.Flow
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Common.Utilities;
    using DotNetty.Handlers.Flow;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit;

    public class FlowControlHandlerTest : IDisposable
    {
        readonly IEventLoopGroup group;

        public FlowControlHandlerTest()
        {
            this.group = new MultithreadEventLoopGroup();
        }

        public void Dispose()
        {
            this.group?.ShutdownGracefullyAsync();
        }

        /**
         * The {@link OneByteToThreeStringsDecoder} decodes this {@code byte[]} into three messages.
         */
        static IByteBuffer NewOneMessage() => Unpooled.WrappedBuffer(new byte[] { 1 });

        Task<IChannel> NewServer(bool autoRead, params IChannelHandler[] handlers)
        {
            Assert.True(handlers.Length >= 1);

            var serverBootstrap = new ServerBootstrap();
            serverBootstrap.Group(this.group)
                .Channel<TcpServerSocketChannel>()
                .ChildOption(ChannelOption.AutoRead, autoRead)
                .ChildHandler(
                    new ActionChannelInitializer<IChannel>(
                        ch =>
                        {
                            IChannelPipeline pipeline = ch.Pipeline;
                            pipeline.AddLast(new OneByteToThreeStringsDecoder());
                            pipeline.AddLast(handlers);
                        }));

            return serverBootstrap.BindAsync(IPAddress.Loopback, 0);
        }

        Task<IChannel> NewClient(EndPoint server)
        {
            var bootstrap = new Bootstrap();

            bootstrap.Group(this.group)
                .Channel<TcpSocketChannel>()
                .Option(ChannelOption.ConnectTimeout, TimeSpan.FromMilliseconds(1000))
                .Handler(new TestHandler(onRead: (ctx, m) => Assert.True(false, "In this test the client is never receiving a message from the server.")));

            return bootstrap.ConnectAsync(server);
        }

        /**
         * This test demonstrates the default behavior if auto reading
         * is turned on from the get-go and you're trying to turn it off
         * once you've received your first message.
         *
         * NOTE: This test waits for the client to disconnect which is
         * interpreted as the signal that all {@code byte}s have been
         * transferred to the server.
         */
        [Fact]
        public async Task TestAutoReadingOn()
        {
            var latch = new CountdownEvent(3);

            ChannelHandlerAdapter handler = new TestHandler(
                onRead: (ctx, msg) =>
                {
                    ReferenceCountUtil.Release(msg);
                    // We're turning off auto reading in the hope that no
                    // new messages are being sent but that is not true.
                    ctx.Channel.Configuration.AutoRead = false;

                    latch.Signal();
                });

            IChannel server = await this.NewServer(true, handler);
            IChannel client = await this.NewClient(server.LocalAddress);

            try
            {
                await client.WriteAndFlushAsync(NewOneMessage());

                // We received three messages even through auto reading
                // was turned off after we received the first message.
                Assert.True(latch.Wait(TimeSpan.FromSeconds(1)));
            }
            finally
            {
                Task.WhenAll(client.CloseAsync(), server.CloseAsync()).Wait(TimeSpan.FromSeconds(5));
            }
        }

        /**
         * This test demonstrates the default behavior if auto reading
         * is turned off from the get-go and you're calling read() in
         * the hope that only one message will be returned.
         *
         * NOTE: This test waits for the client to disconnect which is
         * interpreted as the signal that all {@code byte}s have been
         * transferred to the server.
         */
        [Fact]
        public async Task TestAutoReadingOff()
        {
            IChannel channel = null;
            var mre = new ManualResetEventSlim(false);

            var latch = new CountdownEvent(3);

            ChannelHandlerAdapter handler = new TestHandler(
                onActive: ctx =>
                {
                    Interlocked.Exchange(ref channel, ctx.Channel);
                    mre.Set();

                    ctx.FireChannelActive();
                },
                onRead: (ctx, msg) =>
                {
                    ReferenceCountUtil.Release(msg);
                    latch.Signal();
                }
            );

            IChannel server = await this.NewServer(false, handler);
            IChannel client = await this.NewClient(server.LocalAddress);

            try
            {
                // The client connection on the server side
                mre.Wait(TimeSpan.FromSeconds(1));
                IChannel peer = Interlocked.Exchange(ref channel, null);

                // Write the message
                await client.WriteAndFlushAsync(NewOneMessage());

                // Read the message
                peer.Read();

                // We received all three messages but hoped that only one
                // message was read because auto reading was off and we
                // invoked the read() method only once.
                Assert.True(latch.Wait(TimeSpan.FromSeconds(1)));
            }
            finally
            {
                Task.WhenAll(client.CloseAsync(), server.CloseAsync()).Wait(TimeSpan.FromSeconds(5));
            }
        }

        /**
         * The {@link FlowControlHandler} will simply pass-through all messages
         * if auto reading is on and remains on.
         */
        [Fact]
        public async Task TestFlowAutoReadOn()
        {
            var latch = new CountdownEvent(3);

            ChannelHandlerAdapter handler = new TestHandler(onRead: (ctx, msg) => latch.Signal());

            var flow = new FlowControlHandler();
            IChannel server = await this.NewServer(true, flow, handler);
            IChannel client = await this.NewClient(server.LocalAddress);
            try
            {
                // Write the message
                await client.WriteAndFlushAsync(NewOneMessage());

                // We should receive 3 messages
                Assert.True(latch.Wait(TimeSpan.FromSeconds(1)));
                Assert.True(flow.IsQueueEmpty);
            }
            finally
            {
                Task.WhenAll(client.CloseAsync(), server.CloseAsync()).Wait(TimeSpan.FromSeconds(5));
            }
        }

        /**
         * The {@link FlowControlHandler} will pass down messages one by one
         * if {@link ChannelConfig#setAutoRead(boolean)} is being toggled.
         */
        [Fact]
        public async Task TestFlowToggleAutoRead()
        {
            IChannel channel = null;
            var mre = new ManualResetEventSlim(false);

            var msgRcvLatch1 = new CountdownEvent(1);
            var msgRcvLatch2 = new CountdownEvent(1);
            var msgRcvLatch3 = new CountdownEvent(1);
            var setAutoReadLatch1 = new CountdownEvent(1);
            var setAutoReadLatch2 = new CountdownEvent(1);

            int msgRcvCount = 0;
            int expectedMsgCount = 0;
            ChannelHandlerAdapter handler = new TestHandler(
                onActive: ctx =>
                {
                    Interlocked.Exchange(ref channel, ctx.Channel);
                    mre.Set();
                    ctx.FireChannelActive();
                },
                onRead: (ctx, msg) =>
                {
                    ReferenceCountUtil.Release(msg);

                    // Disable auto reading after each message
                    ctx.Channel.Configuration.AutoRead = false;

                    if (msgRcvCount++ != expectedMsgCount)
                    {
                        return;
                    }
                    switch (msgRcvCount)
                    {
                        case 1:
                            msgRcvLatch1.Signal();
                            if (setAutoReadLatch1.Wait(TimeSpan.FromSeconds(1)))
                            {
                                ++expectedMsgCount;
                            }
                            break;
                        case 2:
                            msgRcvLatch2.Signal();
                            if (setAutoReadLatch2.Wait(TimeSpan.FromSeconds(1)))
                            {
                                ++expectedMsgCount;
                            }
                            break;
                        default:
                            msgRcvLatch3.Signal();
                            break;
                    }
                }
            );

            var flow = new FlowControlHandler();
            IChannel server = await this.NewServer(true, flow, handler);
            IChannel client = await this.NewClient(server.LocalAddress);
            try
            {
                // The client connection on the server side
                mre.Wait(TimeSpan.FromSeconds(1));
                IChannel peer = Interlocked.Exchange(ref channel, null);

                await client.WriteAndFlushAsync(NewOneMessage());

                // channelRead(1)
                Assert.True(msgRcvLatch1.Wait(TimeSpan.FromSeconds(1)));

                // channelRead(2)
                peer.Configuration.AutoRead = true;
                setAutoReadLatch1.Signal();
                Assert.True(msgRcvLatch1.Wait(TimeSpan.FromSeconds(1)));

                // channelRead(3)
                peer.Configuration.AutoRead = true;
                setAutoReadLatch2.Signal();
                Assert.True(msgRcvLatch3.Wait(TimeSpan.FromSeconds(1)));
                Assert.True(flow.IsQueueEmpty);
            }
            finally
            {
                Task.WhenAll(client.CloseAsync(), server.CloseAsync()).Wait(TimeSpan.FromSeconds(5));
            }
        }

        /**
         * The {@link FlowControlHandler} will pass down messages one by one
         * if auto reading is off and the user is calling {@code read()} on
         * their own.
         */
        [Fact]
        public async Task TestFlowAutoReadOff()
        {
            IChannel channel = null;
            var mre = new ManualResetEventSlim(false);

            var msgRcvLatch1 = new CountdownEvent(1);
            var msgRcvLatch2 = new CountdownEvent(2);
            var msgRcvLatch3 = new CountdownEvent(3);

            ChannelHandlerAdapter handler = new TestHandler(
                onActive: ctx =>
                {
                    ctx.FireChannelActive();
                    //peerRef.exchange(ctx.Channel, 1L, SECONDS);
                    Interlocked.Exchange(ref channel, ctx.Channel);
                    mre.Set();
                },
                onRead: (ctx, msg) =>
                {
                    Signal(msgRcvLatch1);
                    Signal(msgRcvLatch2);
                    Signal(msgRcvLatch3);
                }
            );

            var flow = new FlowControlHandler();
            IChannel server = await this.NewServer(false, flow, handler);
            IChannel client = await this.NewClient(server.LocalAddress);
            try
            {
                // The client connection on the server side
                mre.Wait(TimeSpan.FromSeconds(1));
                IChannel peer = Interlocked.Exchange(ref channel, null);

                // Write the message
                await client.WriteAndFlushAsync(NewOneMessage());

                // channelRead(1)
                peer.Read();
                Assert.True(msgRcvLatch1.Wait(TimeSpan.FromSeconds(10)));

                // channelRead(2)
                peer.Read();
                Assert.True(msgRcvLatch2.Wait(TimeSpan.FromSeconds(10)));

                // channelRead(3)
                peer.Read();
                Assert.True(msgRcvLatch3.Wait(TimeSpan.FromSeconds(10)));
                Assert.True(flow.IsQueueEmpty);
            }
            finally
            {
                Task.WhenAll(client.CloseAsync(), server.CloseAsync()).Wait(TimeSpan.FromSeconds(5));
            }

            void Signal(CountdownEvent evt)
            {
                if (!evt.IsSet)
                {
                    evt.Signal();
                }
            }
        }

        class TestHandler : ChannelHandlerAdapter
        {
            readonly Action<IChannelHandlerContext> onActive;
            readonly Action<IChannelHandlerContext, object> onRead;

            public TestHandler(
                Action<IChannelHandlerContext> onActive = null,
                Action<IChannelHandlerContext, object> onRead = null
            )
            {
                this.onActive = onActive;
                this.onRead = onRead;
            }

            public override void ChannelActive(IChannelHandlerContext context)
            {
                if (this.onActive != null)
                {
                    this.onActive(context);
                }
                else
                {
                    base.ChannelActive(context);
                }
            }

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                if (this.onRead != null)
                {
                    this.onRead(context, message);
                }
                else
                {
                    base.ChannelRead(context, message);
                }
            }
        }

        /**
         * This is a fictional message decoder. It decodes each {@code byte}
         * into three strings.
         */
        class OneByteToThreeStringsDecoder : ByteToMessageDecoder
        {
            protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
            {
                for (int i = 0; i < input.ReadableBytes; i++)
                {
                    output.Add("1");
                    output.Add("2");
                    output.Add("3");
                }
                input.SetReaderIndex(input.ReadableBytes);
            }
        }
    }
}