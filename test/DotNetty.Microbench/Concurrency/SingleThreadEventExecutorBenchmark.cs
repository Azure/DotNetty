// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench.Concurrency
{
    using System;
    using System.Threading;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Attributes.Jobs;
    using BenchmarkDotNet.Engines;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;

    [SimpleJob(RunStrategy.Monitoring)]
    [BenchmarkCategory("Concurrency")]
    public class SingleThreadEventExecutorBenchmark
    {
        const int Iterations = 10 * 1000 * 1000;
        TestExecutor concurrentQueueExecutor;
        TestExecutor fixedMpscQueueExecutor;

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.concurrentQueueExecutor = new TestExecutor("CompatibleConcurrentQueue", TimeSpan.FromSeconds(1), new CompatibleConcurrentQueue<IRunnable>());
            this.fixedMpscQueueExecutor = new TestExecutor("FixedMpscQueue", TimeSpan.FromSeconds(1), PlatformDependent.NewFixedMpscQueue<IRunnable>(1 * 1000 * 1000));
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.concurrentQueueExecutor?.ShutdownGracefullyAsync();
            this.fixedMpscQueueExecutor?.ShutdownGracefullyAsync();
        }

        [Benchmark]
        public void ConcurrentQueue() => Run(this.concurrentQueueExecutor);

        [Benchmark]
        public void FixedMpscQueue() => Run(this.fixedMpscQueueExecutor);

        static void Run(TestExecutor executor)
        {
            var mre = new ManualResetEvent(false);
            var actionIn = new BenchActionIn(executor, mre);
            executor.Execute(actionIn);

            if (!mre.WaitOne(TimeSpan.FromMinutes(1)))
            {
                throw new TimeoutException($"{executor.Name} benchmark timed out.");
            }
            mre.Reset();

            var actionOut = new BenchActionOut(mre);
            for (int i = 0; i < Iterations; i++)
            {
                executor.Execute(actionOut);
            }

            if (!mre.WaitOne(TimeSpan.FromMinutes(1)))
            {
                throw new TimeoutException($"{executor.Name} benchmark timed out.");
            }
        }

        sealed class BenchActionIn : IRunnable
        {
            int value;
            readonly IEventExecutor executor;
            readonly ManualResetEvent evt;

            public BenchActionIn(IEventExecutor executor, ManualResetEvent evt)
            {
                this.executor = executor;
                this.evt = evt;
            }

            public void Run()
            {
                if (++this.value < Iterations)
                {
                    this.executor.Execute(this);
                }
                else
                {
                    this.evt.Set();
                }
            }
        }

        sealed class BenchActionOut : IRunnable
        {
            int value;
            readonly ManualResetEvent evt;

            public BenchActionOut(ManualResetEvent evt)
            {
                this.evt = evt;
            }

            public void Run()
            {
                if (++this.value >= Iterations)
                {
                    this.evt.Set();
                }
            }
        }

        sealed class TestExecutor : SingleThreadEventExecutor
        {
            public string Name { get; }

            public TestExecutor(string threadName, TimeSpan breakoutInterval, IQueue<IRunnable> queue)
                : base(threadName, breakoutInterval, queue)
            {
                this.Name = threadName;
            }
        }
    }
}
