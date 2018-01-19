// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Tests.Common;
    using Xunit;
    using Xunit.Abstractions;
    using static TestUtil;

    public sealed class EventLoopTests : TestBase, IDisposable
    {
        readonly EventLoop eventLoop;
        readonly NoOp noOp;

        sealed class NoOp : IRunnable
        {
            public void Run() { }
        }

        sealed class RunCounter : IRunnable
        {
            readonly int expected;
            readonly TaskCompletionSource completionSource;
            int count;

            public RunCounter(int expected)
            {
                this.expected = expected;
                this.completionSource = new TaskCompletionSource();
            }

            public Task Completion => this.completionSource.Task;

            public int Count => this.count;

            public long EndTime { get; private set; }

            public void Run()
            {
                if (Interlocked.Increment(ref this.count) >= this.expected)
                {
                    this.EndTime = DateTime.Now.Ticks;
                    this.completionSource.TryComplete();
                }
            }
        }

        public EventLoopTests(ITestOutputHelper output) : base(output)
        {
            this.eventLoop = new EventLoop(null, null);
            this.noOp = new NoOp();
        }

        [Fact]
        public void Shutdown()
        {
            Assert.True(this.eventLoop.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero).Wait(DefaultTimeout));
            Assert.True(this.eventLoop.IsTerminated);
            Assert.Throws<RejectedExecutionException>(() => this.eventLoop.Execute(this.noOp));
        }

        [Fact]
        public void ShutdownAfterExecute()
        {
            var counter = new RunCounter(1);
            this.eventLoop.Execute(counter);

            Assert.True(counter.Completion.Wait(DefaultTimeout));
            Assert.Equal(1, counter.Count);

            Assert.True(this.eventLoop.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero).Wait(DefaultTimeout));
            Assert.True(this.eventLoop.IsTerminated);
            Assert.Throws<RejectedExecutionException>(() => this.eventLoop.Execute(this.noOp));
        }

        [Fact]
        public void ScheduleTask()
        {
            const int Delay = 500;
            var counter = new RunCounter(1);
            long startTime = DateTime.UtcNow.Ticks;
            IScheduledTask task = this.eventLoop.Schedule(counter, TimeSpan.FromMilliseconds(Delay));
            Assert.True(task.Completion.Wait(DefaultTimeout));
            Assert.Equal(1, counter.Count);
            long delay = counter.EndTime - startTime;
            Assert.True(delay > 0);
            TimeSpan duration = TimeSpan.FromTicks(delay);
            Assert.True(duration.TotalMilliseconds >= Delay, $"Expected delay : {Delay} milliseconds, but was : {duration}");
        }

        [Fact]
        public void RegistrationAfterShutdown()
        {
            Assert.True(this.eventLoop.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero).Wait(DefaultTimeout));
            Assert.True(this.eventLoop.IsTerminated);

            var channel = new TcpChannel();
            var exception = Assert.Throws<AggregateException>(() => this.eventLoop.RegisterAsync(channel).Wait(DefaultTimeout));
            Assert.IsType<RejectedExecutionException>(exception.InnerException);
            Assert.False(channel.Open);
        }

        [Fact]
        public void GracefulShutdownQuietPeriod()
        {
            Task task = this.eventLoop.ShutdownGracefullyAsync(TimeSpan.FromSeconds(1), TimeSpan.MaxValue);
            // Keep Scheduling tasks for another 2 seconds.
            for (int i = 0; i < 20; i++)
            {
                Thread.Sleep(100);
                this.eventLoop.Execute(new NoOp());
            }

            long startTime = DateTime.UtcNow.Ticks;

            Assert.True(this.eventLoop.IsShuttingDown);
            Assert.False(this.eventLoop.IsShutdown);
            Assert.True(task.Wait(DefaultTimeout), "Loop shutdown timed out");

            Assert.True(this.eventLoop.IsShuttingDown);
            Assert.True(this.eventLoop.IsShutdown);

            long time = DateTime.UtcNow.Ticks - startTime;
            long duration = (long)TimeSpan.FromTicks(time).TotalMilliseconds;
            Assert.True(duration >= 1000, $"Expecting shutdown quite period >= 1000 milliseconds, but was {duration}");
        }

        [Fact]
        public void GracefulShutdownTimeout()
        {
            Task task = this.eventLoop.ShutdownGracefullyAsync(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
            // Keep Scheduling tasks for another 3 seconds.
            // Submitted tasks must be rejected after 2 second timeout.
            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(100);
                this.eventLoop.Execute(new NoOp());
            }

            bool rejected;
            try
            {
                for (int i = 0; i < 20; i++)
                {
                    Thread.Sleep(100);
                    this.eventLoop.Execute(new NoOp());
                }
                rejected = false;
            }
            catch (RejectedExecutionException)
            {
                // Expected
                rejected = true;
            }

            Assert.True(rejected, "Submitted tasks must be rejected after 2 second timeout");
            Assert.True(this.eventLoop.IsShuttingDown);
            Assert.True(this.eventLoop.IsShutdown);
            Assert.True(task.Wait(DefaultTimeout), "Loop shutdown timed out");
        }

        public void Dispose()
        {
            this.eventLoop.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero).Wait(DefaultTimeout);
        }
    }
}
