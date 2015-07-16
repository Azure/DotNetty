// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Tests.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using Xunit;

    public class EventExecutorTests
    {
        [Fact]
        public async Task TaskSchedulerIsPreserved()
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

            await executor.ShutdownGracefullyAsync(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
    }
}