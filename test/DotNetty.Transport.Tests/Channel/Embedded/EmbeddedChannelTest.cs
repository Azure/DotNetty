// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Channel.Embedded
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
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
            var channel = new EmbeddedChannel(new ActionChannelInitializer<IChannel>(ch => { ch.Pipeline.AddLast(handler); }));
            IChannelPipeline pipeline = channel.Pipeline;
            Assert.Same(handler, pipeline.FirstContext().Handler);
            Assert.True(channel.WriteInbound(3));
            Assert.True(channel.Finish());
            Assert.Equal(first, channel.ReadInbound<object>());
            Assert.Equal(second, channel.ReadInbound<object>());
            Assert.Null(channel.ReadInbound<object>());
        }

        [Fact]
        public void TestScheduling()
        {
            var ch = new EmbeddedChannel(new ChannelHandlerAdapter());
            var latch = new CountdownEvent(2);
            Task future = ch.EventLoop.ScheduleAsync(() => latch.Signal(), TimeSpan.FromSeconds(1));
            future.ContinueWith(t => latch.Signal());
            PreciseTimeSpan next = ch.RunScheduledPendingTasks();
            Assert.True(next > PreciseTimeSpan.Zero);
            // Sleep for the nanoseconds but also give extra 50ms as the clock my not be very precise and so fail the test
            // otherwise.
            Thread.Sleep(next.ToTimeSpan() + TimeSpan.FromMilliseconds(50));
            Assert.Equal(PreciseTimeSpan.MinusOne, ch.RunScheduledPendingTasks());
            latch.Wait();
        }

        [Fact]
        public void TestScheduledCancelled()
        {
            var ch = new EmbeddedChannel(new ChannelHandlerAdapter());
            Task future = ch.EventLoop.ScheduleAsync(() => { }, TimeSpan.FromDays(1));
            ch.Finish();
            Assert.True(future.IsCanceled);
        }

        [Fact]
        public async Task TestScheduledCancelledDirectly()
        {
            var ch = new EmbeddedChannel(new ChannelHandlerAdapter());

            IScheduledTask task1 = ch.EventLoop.Schedule(() => { }, new TimeSpan(1));
            IScheduledTask task2 = ch.EventLoop.Schedule(() => { }, new TimeSpan(1));
            IScheduledTask task3 = ch.EventLoop.Schedule(() => { }, new TimeSpan(1));
            task2.Cancel();
            ch.RunPendingTasks();
            Task<bool> checkTask1 = ch.EventLoop.SubmitAsync(() => task1.Completion.IsCompleted);
            Task<bool> checkTask2 = ch.EventLoop.SubmitAsync(() => task2.Completion.IsCanceled);
            Task<bool> checkTask3 = ch.EventLoop.SubmitAsync(() => task3.Completion.IsCompleted);
            ch.RunPendingTasks();
            ch.CheckException();
            Assert.True(await checkTask1);
            Assert.True(await checkTask2);
            Assert.True(await checkTask3);
        }

        [Fact]
        public async Task TestScheduledCancelledAsync()
        {
            var ch = new EmbeddedChannel(new ChannelHandlerAdapter());
            var cts = new CancellationTokenSource();
            Task task = ch.EventLoop.ScheduleAsync(() => { }, TimeSpan.FromDays(1), cts.Token);
            await Task.Run(() => cts.Cancel());
            Task<bool> checkTask = ch.EventLoop.SubmitAsync(() => task.IsCanceled);
            ch.RunPendingTasks();
            Assert.True(await checkTask);
        }

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
        [InlineData(3000)]
        public void TestHandlerAddedExecutedInEventLoop(int timeout)
        {
            var latch = new CountdownEvent(1);
            var ex = new AtomicReference<Exception>();
            IChannelHandler handler = new ChannelHandler3(latch, ex);
            var channel = new EmbeddedChannel(handler);
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
            var channel = new EmbeddedChannel();
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
        public void TestFireChannelInactiveAndUnregisteredOnDisconnect(int timeout) =>
            this.TestFireChannelInactiveAndUnregisteredOnClose(channel => channel.DisconnectAsync(), timeout);

        void TestFireChannelInactiveAndUnregisteredOnClose(Func<IChannel, Task> action, int timeout)
        {
            var latch = new CountdownEvent(3);
            var channel = new EmbeddedChannel(new ChannelHandlerWithInactiveAndRegister(latch));
            action(channel);
            Assert.True(latch.Wait(timeout));
        }

        class ChannelHandlerWithInactiveAndRegister : ChannelHandlerAdapter
        {
            readonly CountdownEvent latch;

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

            public override void ChannelUnregistered(IChannelHandlerContext context) => this.latch.Signal();
        }
    }
}