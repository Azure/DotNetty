// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Tests.Utilities
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Tests.Common;
    using Xunit;
    using Xunit.Abstractions;

    public class HashedWheelTimerTest : TestBase
    {
        public HashedWheelTimerTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public void TestScheduleTimeoutShouldNotRunBeforeDelay()
        {
            ITimer timer = new HashedWheelTimer(TimeSpan.FromMilliseconds(100), 512, -1);
            var barrier = new CountdownEvent(1);
            ITimeout timeout = timer.NewTimeout(
                new ActionTimerTask(
                    t =>
                    {
                        Assert.True(false, "This should not have run");
                        barrier.Signal();
                    }),
                TimeSpan.FromSeconds(10));
            Assert.False(barrier.Wait(TimeSpan.FromSeconds(3)));
            Assert.False(timeout.Expired, "timer should not expire");
            timer.StopAsync().Wait();
        }

        [Fact]
        public void TestScheduleTimeoutShouldRunAfterDelay()
        {
            ITimer timer = new HashedWheelTimer();
            var barrier = new CountdownEvent(1);
            ITimeout timeout = timer.NewTimeout(
                new ActionTimerTask(
                    t => { barrier.Signal(); }),
                TimeSpan.FromSeconds(2));
            Assert.True(barrier.Wait(TimeSpan.FromSeconds(3)));
            Assert.True(timeout.Expired, "timer should expire");
            timer.StopAsync().Wait();
        }

        [Fact] // (timeout = 3000)
        public void TestStopTimer()
        {
            var latch = new CountdownEvent(3);
            ITimer timerProcessed = new HashedWheelTimer();
            for (int i = 0; i < 3; i++)
            {
                timerProcessed.NewTimeout(
                    new ActionTimerTask(
                        t => { latch.Signal(); }),
                    TimeSpan.FromMilliseconds(1));
            }

            latch.Wait();
            Assert.Equal(0, timerProcessed.StopAsync().Result.Count); // "Number of unprocessed timeouts should be 0"

            ITimer timerUnprocessed = new HashedWheelTimer();
            for (int i = 0; i < 5; i++)
            {
                timerUnprocessed.NewTimeout(
                    new ActionTimerTask(
                        t => { }),
                    TimeSpan.FromSeconds(5));
            }
            Thread.Sleep(1000); // sleep for a second
            Assert.NotEqual(0, timerUnprocessed.StopAsync().Result.Count); // Number of unprocessed timeouts should be greater than 0
        }

        [Fact] // (timeout = 3000)
        public void TestTimerShouldThrowExceptionAfterShutdownForNewTimeouts()
        {
            var latch = new CountdownEvent(3);
            ITimer timer = new HashedWheelTimer();
            for (int i = 0; i < 3; i++)
            {
                timer.NewTimeout(
                    new ActionTimerTask(
                        t => { latch.Signal(); }),
                    TimeSpan.FromMilliseconds(1));
            }

            latch.Wait(3000);
            timer.StopAsync().Wait();

            Assert.Throws<RejectedExecutionException>(() => timer.NewTimeout(CreateNoOpTimerTask(), TimeSpan.FromMilliseconds(1)));
        }

        [Fact] // (timeout = 5000)
        public void TestTimerOverflowWheelLength()
        {
            var timer = new HashedWheelTimer(TimeSpan.FromMilliseconds(100), 32, -1);
            var latch = new CountdownEvent(3);

            ActionTimerTask task = null;
            task = new ActionTimerTask(
                t =>
                {
                    timer.NewTimeout(task, TimeSpan.FromMilliseconds(100));
                    latch.Signal();
                });
            timer.NewTimeout(task, TimeSpan.FromMilliseconds(100));

            Assert.True(latch.Wait(5000));
            Assert.NotEqual(0, timer.StopAsync().Result.Count);
        }

        [Fact]
        public void TestExecutionOnTime()
        {
            int tickDuration = 200;
            int timeout = 125;
            int maxTimeout = 2 * (tickDuration + timeout);
            var timer = new HashedWheelTimer(TimeSpan.FromMilliseconds(tickDuration), 512, -1);
            var queue = new BlockingCollection<PreciseTimeSpan>();

            int scheduledTasks = 10000;
            for (int i = 0; i < scheduledTasks; i++)
            {
                PreciseTimeSpan start = PreciseTimeSpan.FromStart;
                timer.NewTimeout(
                    new ActionTimerTask(
                        t => { queue.Add(PreciseTimeSpan.FromStart - start); }),
                    TimeSpan.FromMilliseconds(timeout));
            }

            for (int i = 0; i < scheduledTasks; i++)
            {
                double delay = queue.Take().ToTimeSpan().TotalMilliseconds;
                Assert.True(delay >= timeout && delay < maxTimeout, "Timeout + " + scheduledTasks + " delay " + delay + " must be " + timeout + " < " + maxTimeout);
            }

            timer.StopAsync().Wait();
        }

        [Fact]
        public void TestRejectedExecutionExceptionWhenTooManyTimeoutsAreAddedBackToBack()
        {
            var timer = new HashedWheelTimer(TimeSpan.FromMilliseconds(100), 32, 2);
            timer.NewTimeout(CreateNoOpTimerTask(), TimeSpan.FromSeconds(5));
            timer.NewTimeout(CreateNoOpTimerTask(), TimeSpan.FromSeconds(5));
            try
            {
                timer.NewTimeout(CreateNoOpTimerTask(), TimeSpan.FromMilliseconds(1));
                Assert.True(false, "Timer allowed adding 3 timeouts when maxPendingTimeouts was 2");
            }
            catch (RejectedExecutionException)
            {
                // Expected
            }
            finally
            {
                timer.StopAsync().Wait();
            }
        }

        [Fact]
        public void TestNewTimeoutShouldStopThrowingRejectedExecutionExceptionWhenExistingTimeoutIsCancelled()

        {
            int tickDurationMs = 100;
            var timer = new HashedWheelTimer(TimeSpan.FromMilliseconds(tickDurationMs), 32, 2);
            timer.NewTimeout(CreateNoOpTimerTask(), TimeSpan.FromSeconds(5));
            ITimeout timeoutToCancel = timer.NewTimeout(CreateNoOpTimerTask(), TimeSpan.FromSeconds(5));
            Assert.True(timeoutToCancel.Cancel());

            Thread.Sleep(tickDurationMs * 5);

            var secondLatch = new CountdownEvent(1);
            timer.NewTimeout(CreateCountdownEventTimerTask(secondLatch), TimeSpan.FromMilliseconds(90));

            secondLatch.Wait();
            timer.StopAsync().Wait();
        }

        [Fact] // (timeout = 3000)
        public void TestNewTimeoutShouldStopThrowingRejectedExecutionExceptionWhenExistingTimeoutIsExecuted()

        {
            var latch = new CountdownEvent(1);
            var timer = new HashedWheelTimer(TimeSpan.FromMilliseconds(25), 4, 2);
            timer.NewTimeout(CreateNoOpTimerTask(), TimeSpan.FromSeconds(5));
            timer.NewTimeout(CreateCountdownEventTimerTask(latch), TimeSpan.FromMilliseconds(90));

            latch.Wait(3000);

            var secondLatch = new CountdownEvent(1);
            timer.NewTimeout(CreateCountdownEventTimerTask(secondLatch), TimeSpan.FromMilliseconds(90));

            secondLatch.Wait(3000);
            timer.StopAsync().Wait();
        }

        static ActionTimerTask CreateNoOpTimerTask() => new ActionTimerTask(t => { });

        static ActionTimerTask CreateCountdownEventTimerTask(CountdownEvent latch) => new ActionTimerTask(t => { latch.Signal(); });
    }
}