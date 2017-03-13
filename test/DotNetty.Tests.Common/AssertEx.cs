// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Tests.Common
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using Xunit;

    public static class AssertEx
    {
        public static Task EventuallyAsync(Func<bool> testFunc, TimeSpan interval, TimeSpan timeout) => EventuallyAsync(() => Task.FromResult(testFunc()), interval, timeout);

        public static async Task EventuallyAsync(Func<Task<bool>> testFunc, TimeSpan interval, TimeSpan timeout)
        {
            PreciseTimeSpan deadline = PreciseTimeSpan.Deadline(timeout);
            while (true)
            {
                if (await testFunc())
                {
                    return;
                }
                if (PreciseTimeSpan.FromStart - deadline > PreciseTimeSpan.Zero)
                {
                    Assert.True(false, "Did not reach expected state in time.");
                }
                await Task.Delay(interval);
            }
        }
    }
}