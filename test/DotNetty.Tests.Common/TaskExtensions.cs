// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Tests.Common
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public static class TaskExtensions
    {
        public static Task WithTimeout(this Task task, TimeSpan timeout)
        {
            if (task.IsCompleted || (timeout == Timeout.InfiniteTimeSpan))
            {
                return task;
            }

            return WithTimeoutInternal(task, timeout);
        }

        static async Task WithTimeoutInternal(Task task, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource())
            {
                if (task == await Task.WhenAny(task, Task.Delay(timeout, cts.Token)))
                {
                    cts.Cancel();
                    await task;
                    return;
                }
            }

            throw new TimeoutException();
        }
    }
}