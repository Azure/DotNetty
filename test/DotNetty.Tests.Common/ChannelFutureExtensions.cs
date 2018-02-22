// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Tests.Common
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    public static class ChannelFutureExtensions
    {
        public static bool Wait(this ChannelFuture future, TimeSpan timeout)
        {
            return Task.Run(async () => await future).Wait(timeout);
            
            /*var mre = new ManualResetEventSlim(false);
            future.OnCompleted(mre.Set);
            return mre.Wait(timeout);*/
        }
    }
}