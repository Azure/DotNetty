// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Tests.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Tests.Common;
    using Xunit;
    using Xunit.Abstractions;

    public class SingleThreadEventExecutorTests : TestBase
    {
        public SingleThreadEventExecutorTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public void TaskSchedulerIsPreserved()
        {
            var executor = new SingleThreadEventExecutor("test", TimeSpan.FromSeconds(5));
            IEnumerable<Task<int>> tasks = Enumerable.Range(1, 1).Select(async i =>
            {
                //Clear SynchronizationContext set by xunit
                SynchronizationContext.SetSynchronizationContext(null);

                var completion = new TaskCompletionSource();
                executor.Execute(async () =>
                {
                    try
                    {
                        Assert.True(executor.InEventLoop);
                        await Task.Delay(1);
                        Assert.True(executor.InEventLoop);
                        completion.TryComplete(); // all is well
                    }
                    catch (Exception ex)
                    {
                        completion.TrySetException(ex);
                    }
                });
                await completion.Task;
                Assert.False(executor.InEventLoop);
                return i;
            });

            Task.WhenAll(tasks).Wait(TimeSpan.FromSeconds(500));
            executor.ShutdownGracefullyAsync();
        }

        [Theory]
        [InlineData(1, true, 20)]
        [InlineData(10, true, 20)]
        [InlineData(1, false, 20)]
        public void FuzzyScheduling(int producerCount, bool perCpu, int taskPerProducer)
        {
            int producerCountFinal = perCpu ? producerCount * Environment.ProcessorCount : producerCount;
            var valueContainer = new Container<int>
            {
                Value = taskPerProducer * producerCountFinal
            };
            var mre = new ManualResetEvent(false);
            Action noop = () =>
            {
                if (--valueContainer.Value <= 0)
                {
                    Assert.Equal(0, valueContainer.Value);
                    mre.Set();
                }
            };
            var scheduler = new SingleThreadEventExecutor("test", TimeSpan.FromSeconds(1));
            IEnumerable<Task<Task>> producers = Enumerable.Range(1, producerCountFinal).Select(x => Task.Factory.StartNew(
                async () =>
                {
                    var r = new Random((int)Stopwatch.GetTimestamp() ^ x);
                    for (int i = 0; i < taskPerProducer; i++)
                    {
                        scheduler.Execute(noop);
                        await Task.Delay(r.Next(10, 100));
                    }
                },
                TaskCreationOptions.LongRunning));
            Task.WhenAll(producers).Wait();
            Assert.True(mre.WaitOne(TimeSpan.FromSeconds(5)));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ScheduledTaskFiresOnTime(bool scheduleFromExecutor)
        {
            var scheduler = new SingleThreadEventExecutor(null, TimeSpan.FromMinutes(1));
            var promise = new TaskCompletionSource();
            Func<Task> scheduleFunc = () => scheduler.ScheduleAsync(() => promise.Complete(), TimeSpan.FromMilliseconds(100));
            Task task = scheduleFromExecutor ? await scheduler.SubmitAsync(scheduleFunc) : scheduleFunc();
            await Task.WhenAny(task, Task.Delay(TimeSpan.FromMilliseconds(300)));
            Assert.True(task.IsCompleted);
        }

        [Fact]
        public async Task ScheduledTaskFiresOnTimeWhileBusy()
        {
            var scheduler = new SingleThreadEventExecutor(null, TimeSpan.FromMilliseconds(10));
            var promise = new TaskCompletionSource();
            Action selfQueueAction = null;
            selfQueueAction = () =>
            {
                if (!promise.Task.IsCompleted)
                {
                    scheduler.Execute(selfQueueAction);
                }
            };

            scheduler.Execute(selfQueueAction);
            Task task = scheduler.ScheduleAsync(() => promise.Complete(), TimeSpan.FromMilliseconds(100));
            await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.True(task.IsCompleted);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(200)]
        public async Task ShutdownWhileIdle(int delayInMs)
        {
            var scheduler = new SingleThreadEventExecutor("test", TimeSpan.FromMilliseconds(10));
            if (delayInMs > 0)
            {
                Thread.Sleep(delayInMs);
            }
            Task shutdownTask = scheduler.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(50), TimeSpan.FromSeconds(1));
            await Task.WhenAny(shutdownTask, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.True(shutdownTask.IsCompleted);
        }

        class Container<T>
        {
            public T Value;
        }
    }
}