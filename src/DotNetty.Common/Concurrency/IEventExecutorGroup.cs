// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides an access to a set of <see cref="IEventExecutor"/>s it manages.
    /// </summary>
    public interface IEventExecutorGroup : IScheduledExecutorService
    {
        /// <summary>
        /// Returns list of owned event executors.
        /// </summary>
        IEnumerable<IEventExecutor> Items { get; }

        /// <summary>
        ///     Returns <c>true</c> if and only if this executor is being shut down via <see cref="ShutdownGracefullyAsync()" />.
        /// </summary>
        bool IsShuttingDown { get; }

        /// <summary>
        /// Terminates this <see cref="IEventExecutorGroup"/> and all its <see cref="IEventExecutor"/>s.
        /// </summary>
        /// <returns><see cref="Task"/> for completion of termination.</returns>
        Task ShutdownGracefullyAsync();

        /// <summary>
        /// Terminates this <see cref="IEventExecutorGroup"/> and all its <see cref="IEventExecutor"/>s.
        /// </summary>
        /// <returns><see cref="Task"/> for completion of termination.</returns>
        Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout);

        /// <summary>
        /// A <see cref="Task"/> for completion of termination. <see cref="ShutdownGracefullyAsync()"/>.
        /// </summary>
        Task TerminationCompletion { get; }

        /// <summary>
        /// Returns <see cref="IEventExecutor"/>.
        /// </summary>
        IEventExecutor GetNext();
    }
}