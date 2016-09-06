// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Microbench.Utilities;
    using Xunit;
    using Xunit.Abstractions;

    public class BenchTests
    {
        const int Iterations = 10 * 1000 * 1000;
        readonly ITestOutputHelper output;

        public BenchTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void BenchSingleThreadEventExecutorWaiting()
        {
            var testSubjects = new Dictionary<string, IEventExecutor>
            {
                { "CompatibleConcurrentQueue", new TestExecutor("ConcurrentQueueCustom", TimeSpan.FromSeconds(1), new CompatibleConcurrentQueue<IRunnable>()) },
                { "ArrayQueue", new TestExecutor("ArrayQueue", TimeSpan.FromSeconds(1), PlatformDependent.NewFixedMpscQueue<IRunnable>(1 * 1000 * 1000)) }
            };

            var mre = new ManualResetEvent(false);

            CodeTimer.Benchmark(testSubjects, "STEE in loop ({0})", 1, this.output,
                scheduler =>
                {
                    var action = new BenchActionIn(scheduler, mre);
                    scheduler.Execute(action);

                    if (!mre.WaitOne(TimeSpan.FromMinutes(1)))
                    {
                        throw new TimeoutException($"{scheduler.GetType().Name} benchmark timed out.");
                    }
                    mre.Reset();
                });

            CodeTimer.Benchmark(testSubjects, "STEE out of loop ({0})", 1, this.output,
                scheduler =>
                {
                    var action = new BenchActionOut(mre);
                    for (int i = 0; i < Iterations; i++)
                    {
                        scheduler.Execute(action);
                    }

                    if (!mre.WaitOne(TimeSpan.FromMinutes(1)))
                    {
                        throw new TimeoutException($"{scheduler.GetType().Name} benchmark timed out.");
                    }
                    mre.Reset();
                });

            foreach (IEventExecutor scheduler in testSubjects.Values)
            {
                scheduler.ShutdownGracefullyAsync();
            }
        }

        sealed class TestExecutor : SingleThreadEventExecutor
        {
            public TestExecutor(string threadName, TimeSpan breakoutInterval, IQueue<IRunnable> queue)
                : base(threadName, breakoutInterval, queue)
            {
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

        public class Container<T>
        {
            public T Value;
        }
    }
}