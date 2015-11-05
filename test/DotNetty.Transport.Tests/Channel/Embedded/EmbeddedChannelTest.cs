// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Channel.Embedded
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class EmbeddedChannelTest
    {
        class ChannelHandler1 : ChannelHandlerAdapter
        {
            readonly int first;
            readonly int second;

            public ChannelHandler1(int first, int second)
            {
                this.first = first;
                this.second = second;
            }

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                context.FireChannelRead(this.first);
                context.FireChannelRead(this.second);
            }
        }

        [Fact]
        public void TestConstructWithChannelInitializer()
        {
            int first = 1;
            int second = 2;
            IChannelHandler handler = new ChannelHandler1(first, second);
            EmbeddedChannel channel = new EmbeddedChannel(new ActionChannelInitializer<IChannel>(ch => { ch.Pipeline.AddLast(handler); }));
            IChannelPipeline pipeline = channel.Pipeline;
            Assert.Same(handler, pipeline.FirstContext().Handler);
            Assert.True(channel.WriteInbound(3));
            Assert.True(channel.Finish());
            Assert.Equal(first, channel.ReadInbound<object>());
            Assert.Equal(second, channel.ReadInbound<object>());
            Assert.Null(channel.ReadInbound<object>());
        }

        // TODO: can't test scheduling without https://github.com/Azure/DotNetty/issues/35
        //class ChannelHandler2 : ChannelHandlerAdapter{ }

        //[Fact]
        //public void TestScheduling()
        //{
        //    EmbeddedChannel ch = new EmbeddedChannel(new ChannelHandler2());
        //    CountdownEvent latch = new CountdownEvent(2);
        //    var future = ch.EventLoop.Schedule(_ =>
        //    {
        //        latch.Signal();
        //    }, TimeSpan.FromSeconds(1));
        //}

        class ChannelHandler3 : ChannelHandlerAdapter
        {
            readonly CountdownEvent latch;
            AtomicReference<Exception> error;

            public ChannelHandler3(CountdownEvent latch, AtomicReference<Exception> error)
            {
                this.latch = latch;
                this.error = error;
            }

            public override void HandlerAdded(IChannelHandlerContext context)
            {
                try
                {
                    Assert.True(context.Executor.InEventLoop);
                }
                catch (Exception ex)
                {
                    this.error = ex;
                }
                finally
                {
                    this.latch.Signal();
                }
            }
        }

        [Theory]
        [InlineData(3000)] //ghetto timeout
        public void TestHandlerAddedExecutedInEventLoop(int timeout)
        {
            CountdownEvent latch = new CountdownEvent(1);
            AtomicReference<Exception> ex = new AtomicReference<Exception>();
            IChannelHandler handler = new ChannelHandler3(latch, ex);
            EmbeddedChannel channel = new EmbeddedChannel(handler);
            Assert.False(channel.Finish());
            Assert.True(latch.Wait(timeout));
            Exception cause = ex.Value;
            if (cause != null)
            {
                throw cause;
            }
        }

        [Fact]
        public void TestConstructWithoutHandler()
        {
            EmbeddedChannel channel = new EmbeddedChannel();
            Assert.True(channel.WriteInbound(1));
            Assert.True(channel.WriteOutbound(2));
            Assert.True(channel.Finish());
            Assert.Equal(1, channel.ReadInbound<object>());
            Assert.Null(channel.ReadInbound<object>());
            Assert.Equal(2, channel.ReadOutbound<object>());
            Assert.Null(channel.ReadOutbound<object>());
        }

        [Theory]
        [InlineData(1000)]
        public void TestFireChannelInactiveAndUnregisteredOnDisconnect(int timeout)
        {
            this.TestFireChannelInactiveAndUnregisteredOnClose(channel => channel.DisconnectAsync(), timeout);
        }

        public void TestFireChannelInactiveAndUnregisteredOnClose(Func<IChannel, Task> action, int timeout)
        {
            CountdownEvent latch = new CountdownEvent(3);
            EmbeddedChannel channel = new EmbeddedChannel(new ChannelHandlerWithInactiveAndRegister(latch));
            action(channel);
            Assert.True(latch.Wait(timeout));
        }

        class ChannelHandlerWithInactiveAndRegister : ChannelHandlerAdapter
        {
            CountdownEvent latch;

            public ChannelHandlerWithInactiveAndRegister(CountdownEvent latch)
            {
                this.latch = latch;
            }

            public override void ChannelInactive(IChannelHandlerContext context)
            {
                this.latch.Signal();
                context.Executor.Execute(() =>
                {
                    // should be executed
                    this.latch.Signal();
                });
            }

            public override void ChannelUnregistered(IChannelHandlerContext context)
            {
                this.latch.Signal();
            }
        }
    }
}