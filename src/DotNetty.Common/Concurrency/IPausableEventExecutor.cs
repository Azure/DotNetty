// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    /// <summary>
    /// Implement this interface if you need your {@link EventExecutor} implementation to be able
    /// to reject new work.
    /// </summary>
    public interface IPausableEventExecutor : IWrappedEventExecutor
    {
        /// <summary>
        /// After a call to this method the {@link EventExecutor} may throw a {@link RejectedExecutionException} when
        /// attempting to assign new work to it (i.e. through a call to {@link EventExecutor#execute(Runnable)}).
        /// </summary>
        void RejectNewTasks();

        /// <summary>
        /// With a call to this method the {@link EventExecutor} signals that it is now accepting new work.
        /// </summary>
        void AcceptNewTasks();

        /// <summary>
        /// Returns {@code true} if and only if this {@link EventExecutor} is accepting a new task.
        /// </summary>
        bool IsAcceptingNewTasks { get; }
    }
}