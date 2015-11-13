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
            IEnumerable<Task<int>> tasks = Enumerable.Range(1, 1).Select(i =>
            {
                var completion = new TaskCompletionSource<int>();
                executor.Execute(async () =>
                {
                    try
                    {
                        Assert.True(executor.InEventLoop);
                        await Task.Delay(1);
                        Assert.True(executor.InEventLoop);
                        completion.TrySetResult(0); // all is well
                    }
                    catch (Exception ex)
                    {
                        completion.TrySetException(ex);
                    }
                });
                return completion.Task;
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

        public class Container<T>
        {
            public T Value;
        }
    }
}