// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tests
{
    using DotNetty.Tests.Common;
    using Xunit;
    using Xunit.Abstractions;
    using DotNetty.Handlers.Timeout;
    using DotNetty.Common.Concurrency;
    using System;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using System.Collections.Generic;
    using DotNetty.Buffers;

    public class IdleStateHandlerTest : TestBase
    {
        readonly TimeSpan oneSecond = TimeSpan.FromSeconds(1);
        readonly TimeSpan zeroSecond = TimeSpan.Zero;

        public IdleStateHandlerTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public void TestReaderIdle()
        {
            var idleStateHandler = new TestableIdleStateHandler(
                    false, this.oneSecond, this.zeroSecond, this.zeroSecond);

            // We start with one FIRST_READER_IDLE_STATE_EVENT, followed by an infinite number of READER_IDLE_STATE_EVENTs
            this.AnyIdle(idleStateHandler, IdleStateEvent.FirstReaderIdleStateEvent,
                    IdleStateEvent.ReaderIdleStateEvent, IdleStateEvent.ReaderIdleStateEvent);
        }

        [Fact]
        public void TestWriterIdle()
        {
            var idleStateHandler = new TestableIdleStateHandler(
                    false, this.zeroSecond, this.oneSecond, this.zeroSecond);

            this.AnyIdle(idleStateHandler, IdleStateEvent.FirstWriterIdleStateEvent,
                    IdleStateEvent.WriterIdleStateEvent, IdleStateEvent.WriterIdleStateEvent);
        }

        [Fact]
        public void TestAllIdle()
        {
            var idleStateHandler = new TestableIdleStateHandler(
                    false, this.zeroSecond, this.zeroSecond, this.oneSecond);

            this.AnyIdle(idleStateHandler, IdleStateEvent.FirstAllIdleStateEvent,
                    IdleStateEvent.AllIdleStateEvent, IdleStateEvent.AllIdleStateEvent);
        }

        void AnyIdle(TestableIdleStateHandler idleStateHandler, params object[] expected)
        {
            Assert.True(expected.Length >= 1, "The number of expected events must be >= 1");

            var events = new List<object>();
            var handler = new TestEventChannelInboundHandlerAdapter(events);

            var channel = new EmbeddedChannel(idleStateHandler, handler);
            try
            {
                // For each expected event advance the ticker and run() the task. Each
                // step should yield in an IdleStateEvent because we haven't written
                // or read anything from the channel.
                for (int i = 0; i < expected.Length; i++)
                {
                    idleStateHandler.TickRun();
                }

                Assert.Equal(expected.Length, events.Count);

                // Compare the expected with the actual IdleStateEvents
                for (int i = 0; i < expected.Length; i++)
                {
                    object evt = events[i];
                    Assert.Same(expected[i], evt);//"Element " + i + " is not matching"
                }
            }
            finally
            {
                channel.FinishAndReleaseAll();
            }
        }

        [Fact]
        public void TestReaderNotIdle()
        {
            var idleStateHandler = new TestableIdleStateHandler(
                    false, this.oneSecond, this.zeroSecond, this.zeroSecond);

            Action<EmbeddedChannel> action = (channel) => channel.WriteInbound("Hello, World!");

            this.AnyNotIdle(idleStateHandler, action, IdleStateEvent.FirstReaderIdleStateEvent);
        }

        [Fact]
        public void TestWriterNotIdle()
        {
            var idleStateHandler = new TestableIdleStateHandler(
                    false, this.zeroSecond, this.oneSecond, this.zeroSecond);

            Action<EmbeddedChannel> action = (channel) => channel.WriteAndFlushAsync("Hello, World!");

            this.AnyNotIdle(idleStateHandler, action, IdleStateEvent.FirstWriterIdleStateEvent);
        }

        [Fact]
        public void TestAllNotIdle()
        {
            // Reader...
            var idleStateHandler = new TestableIdleStateHandler(
                    false, this.zeroSecond, this.zeroSecond, this.oneSecond);

            Action<EmbeddedChannel> reader = (channel) => channel.WriteInbound("Hello, World!");

            this.AnyNotIdle(idleStateHandler, reader, IdleStateEvent.FirstAllIdleStateEvent);

            // Writer...
            idleStateHandler = new TestableIdleStateHandler(
                    false, this.zeroSecond, this.zeroSecond, this.oneSecond);

            Action<EmbeddedChannel> writer = (channel) => channel.WriteAndFlushAsync("Hello, World!");

            this.AnyNotIdle(idleStateHandler, writer, IdleStateEvent.FirstAllIdleStateEvent);
        }

        void AnyNotIdle(TestableIdleStateHandler idleStateHandler,
            Action<EmbeddedChannel> action, object expected)
        {
            var events = new List<object>();
            var handler = new TestEventChannelInboundHandlerAdapter(events);

            var channel = new EmbeddedChannel(idleStateHandler, handler);
            try
            {
                //TODO: No NANOSECONDS(0.01 Ticks) support in DotNetty for IdleStateHandler, but is it really needed?
                idleStateHandler.DoTick(TimeSpan.FromTicks(1));
                action.Invoke(channel);

                // Advance the ticker by some fraction and run() the task.
                // There shouldn't be an IdleStateEvent getting fired because
                // we've just performed an action on the channel that is meant
                // to reset the idle task.
                TimeSpan delayInNanos = idleStateHandler.Delay;
                Assert.NotEqual(this.zeroSecond, delayInNanos);

                idleStateHandler.TickRun(TimeSpan.FromTicks(delayInNanos.Ticks / 2));
                Assert.Empty(events);

                // Advance the ticker by the full amount and it should yield
                // in an IdleStateEvent.
                idleStateHandler.TickRun();
                Assert.Single(events);
                Assert.Same(expected, events[0]);
            }
            finally
            {
                channel.FinishAndReleaseAll();
            }
        }

        [Fact]
        public void TestObserveWriterIdle() => this.ObserveOutputIdle(true);

        [Fact]
        public void TestObserveAllIdle() => this.ObserveOutputIdle(false);

        void ObserveOutputIdle(bool writer)
        {
            TimeSpan writerIdleTime = this.zeroSecond;
            TimeSpan allIdleTime = this.zeroSecond;
            IdleStateEvent expected;

            if (writer)
            {
                writerIdleTime = TimeSpan.FromSeconds(5);
                expected = IdleStateEvent.FirstWriterIdleStateEvent;
            }
            else
            {
                allIdleTime = TimeSpan.FromSeconds(5);
                expected = IdleStateEvent.FirstAllIdleStateEvent;
            }

            var idleStateHandler = new TestableIdleStateHandler(
                    true, this.zeroSecond, writerIdleTime, allIdleTime);

            var events = new List<object>();
            var handler = new TestEventChannelInboundHandlerAdapter(events);

            var channel = new ObservableChannel(idleStateHandler, handler);
            try
            {
                // We're writing 3 messages that will be consumed at different rates!
                channel.WriteAndFlushAsync(Unpooled.WrappedBuffer(new byte[] { 1 }));
                channel.WriteAndFlushAsync(Unpooled.WrappedBuffer(new byte[] { 2 }));
                channel.WriteAndFlushAsync(Unpooled.WrappedBuffer(new byte[] { 3 }));

                // Establish a baseline. We're not consuming anything and let it idle once.
                idleStateHandler.TickRun();
                Assert.Single(events);
                Assert.Same(expected, events[0]);
                events.Clear();

                // Our ticker should be at second 5
                Assert.Equal(TimeSpan.FromSeconds(5), idleStateHandler.Tick);

                // Consume one message in 4 seconds, then be idle for 2 seconds,
                // then run the task and we shouldn't get an IdleStateEvent because
                // we haven't been idle for long enough!
                idleStateHandler.DoTick(TimeSpan.FromSeconds(4));
                AssertNotNullAndRelease(channel.Consume());

                idleStateHandler.TickRun(TimeSpan.FromSeconds(2));
                Assert.Empty(events);
                Assert.Equal(TimeSpan.FromSeconds(11), idleStateHandler.Tick); // 5s + 4s + 2s

                // Consume one message in 3 seconds, then be idle for 4 seconds,
                // then run the task and we shouldn't get an IdleStateEvent because
                // we haven't been idle for long enough!
                idleStateHandler.DoTick(TimeSpan.FromSeconds(3));
                AssertNotNullAndRelease(channel.Consume());

                idleStateHandler.TickRun(TimeSpan.FromSeconds(4));
                Assert.Empty(events);
                Assert.Equal(TimeSpan.FromSeconds(18), idleStateHandler.Tick); // 11s + 3s + 4s

                // Don't consume a message and be idle for 5 seconds.
                // We should get an IdleStateEvent!
                idleStateHandler.TickRun(TimeSpan.FromSeconds(5));
                Assert.Single(events);
                Assert.Equal(TimeSpan.FromSeconds(23), idleStateHandler.Tick); // 18s + 5s
                events.Clear();

                // Consume one message in 2 seconds, then be idle for 1 seconds,
                // then run the task and we shouldn't get an IdleStateEvent because
                // we haven't been idle for long enough!
                idleStateHandler.DoTick(TimeSpan.FromSeconds(2));
                AssertNotNullAndRelease(channel.Consume());

                idleStateHandler.TickRun(TimeSpan.FromSeconds(1));
                Assert.Empty(events);
                Assert.Equal(TimeSpan.FromSeconds(26), idleStateHandler.Tick); // 23s + 2s + 1s

                // There are no messages left! Advance the ticker by 3 seconds,
                // attempt a consume() but it will be null, then advance the
                // ticker by an another 2 seconds and we should get an IdleStateEvent
                // because we've been idle for 5 seconds.
                idleStateHandler.DoTick(TimeSpan.FromSeconds(3));
                Assert.Null(channel.Consume());

                idleStateHandler.TickRun(TimeSpan.FromSeconds(2));
                Assert.Single(events);
                Assert.Equal(TimeSpan.FromSeconds(31), idleStateHandler.Tick); // 26s + 3s + 2s

                // q.e.d.
            }
            finally
            {
                channel.FinishAndReleaseAll();
            }
        }

        static void AssertNotNullAndRelease(object msg)
        {
            Assert.NotNull(msg);
            ReferenceCountUtil.Release(msg);
        }

        /*private interface Action
        {
            void run(EmbeddedChannel channel) throws Exception;
        }*/

        class TestableIdleStateHandler : IdleStateHandler
        {
            Action task;
            public TimeSpan Delay { get; private set; }
            public TimeSpan Tick { get; private set; }

            public TestableIdleStateHandler(bool observeOutput,
                TimeSpan readerIdleTime, TimeSpan writerIdleTime, TimeSpan allIdleTime)
                : base(observeOutput, readerIdleTime, writerIdleTime, allIdleTime)
            {
            }

            void Run() => this.task.Invoke();

            public void TickRun() => this.TickRun(this.Delay);

            public void TickRun(TimeSpan delay)
            {
                this.DoTick(delay);
                this.Run();
            }

            public void DoTick(TimeSpan delay)
            {
                this.Tick += delay;
            }

            internal override TimeSpan Ticks() => this.Tick;

            internal override IScheduledTask Schedule(IChannelHandlerContext ctx, Action<object, object> task, object context, object state, TimeSpan delay)
            {
                this.task = task != null ? () => task(context, state) : default(Action);
                this.Delay = delay;
                return null;
            }
        }

        class TestEventChannelInboundHandlerAdapter : ChannelHandlerAdapter
        {
            readonly List<object> events;

            public TestEventChannelInboundHandlerAdapter(List<object> events)
            {
                this.events = events;
            }

            public override void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
                this.events.Add(evt);
            }
        }

        class ObservableChannel : EmbeddedChannel
        {
            public ObservableChannel(params IChannelHandler[] handlers)
                : base(handlers)
            {
            }

            protected override void DoWrite(ChannelOutboundBuffer input)
            {
                // Overridden to change EmbeddedChannel's default behavior. We went to keep
                // the messages in the ChannelOutboundBuffer.
            }

            public object Consume()
            {
                ChannelOutboundBuffer buf = this.Unsafe.OutboundBuffer;
                object msg = buf?.Current;
                if (msg != null)
                {
                    ReferenceCountUtil.Retain(msg);
                    buf.Remove();
                    return msg;
                }
                return null;
            }
        }
    }
}