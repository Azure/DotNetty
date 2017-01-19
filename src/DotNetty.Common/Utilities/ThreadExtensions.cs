// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Threading;
    using Thread = DotNetty.Common.Concurrency.XThread;

    public static class ThreadExtensions
    {
        public static bool Join(this Thread thread, TimeSpan timeout)
        {
            long tm = (long)timeout.TotalMilliseconds;
            Contract.Requires(tm >= 0 && tm <= int.MaxValue);

            return thread.Join((int)tm);
        }
    }
}