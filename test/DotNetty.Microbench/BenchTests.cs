// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using DotNetty.Common.Concurrency;
    using DotNetty.Microbench.Utilities;
    using Xunit;
    using Xunit.Abstractions;

    public class BenchTests
    {
        readonly ITestOutputHelper output;

        public BenchTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void BenchSingleThreadEventExecutorWaiting()
        {
            const int Iterations = 10 * 1000 * 1000;

            var testSubjects = new Dictionary<string, IEventExecutor>
            {
                { "MRES", new SingleThreadEventExecutor("MRES", TimeSpan.FromSeconds(1)) },
                //{ "Semaphore", new SingleThreadEventExecutorOld("Semaphore", TimeSpan.FromSeconds(1)) }
            };

            var mre = new ManualResetEvent(false);

            Action<object, object> action = null;
            action = (s, i) =>
            {
                var container = (Container<int>)i;
                if (container.Value < Iterations)
                {
                    container.Value++;
                    ((IEventExecutor)s).Execute(action, s, container);
                }
                else
                {
                    mre.Set();
                }
            };

            CodeTimer.Benchmark(testSubjects, "STEE in loop ({0})", 1, this.output,
                scheduler =>
                {
                    scheduler.Execute(action, scheduler, new Container<int>());

                    if (!mre.WaitOne(TimeSpan.FromMinutes(1)))
                    {
                        throw new TimeoutException(string.Format("{0} benchmark timed out.", scheduler.GetType().Name));
                    }
                    mre.Reset();
                });

            Action<object> execFromOutsideAction = i =>
            {
                var container = (Container<int>)i;
                if (++container.Value >= Iterations)
                {
                    mre.Set();
                }
            };

            CodeTimer.Benchmark(testSubjects, "STEE out of loop ({0})", 1, this.output, 
                scheduler =>
                {
                    var container = new Container<int>();
                    for (int i = 0; i < Iterations; i++)
                    {
                        scheduler.Execute(execFromOutsideAction, container);
                    }

                    if (!mre.WaitOne(TimeSpan.FromMinutes(1)))
                    {
                        throw new TimeoutException(string.Format("{0} benchmark timed out.", scheduler.GetType().Name));
                    }
                    mre.Reset();
                });

            foreach (IEventExecutor scheduler in testSubjects.Values)
            {
                scheduler.ShutdownGracefullyAsync();
            }
        }

        public class Container<T>
        {
            public T Value;
        }
    }
}