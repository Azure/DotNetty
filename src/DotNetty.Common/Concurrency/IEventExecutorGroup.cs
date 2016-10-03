// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Threading.Tasks;

    public interface IEventExecutorGroup
    {
        Task TerminationCompletion { get; }

        IEventExecutor GetNext();

        Task ShutdownGracefullyAsync();

        Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout);
    }
}