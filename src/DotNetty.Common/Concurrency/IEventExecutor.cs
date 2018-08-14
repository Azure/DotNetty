// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using Thread = DotNetty.Common.Concurrency.XThread;

    public interface IEventExecutor : IEventExecutorGroup
    {
        /// <summary>
        /// Parent <see cref="IEventExecutorGroup"/>.
        /// </summary>
        IEventExecutorGroup Parent { get; }

        /// <summary>
        ///     Returns <c>true</c> if the current <see cref="Thread" /> belongs to this event loop,
        ///     <c>false</c> otherwise.
        /// </summary>
        /// <remarks>
        ///     It is a convenient way to determine whether code can be executed directly or if it
        ///     should be posted for execution to this executor instance explicitly to ensure execution in the loop.
        /// </remarks>
        bool InEventLoop { get; }

        /// <summary>
        ///     Returns <c>true</c> if the given <see cref="Thread" /> belongs to this event loop,
        ///     <c>false></c> otherwise.
        /// </summary>
        bool IsInEventLoop(Thread thread);
    }
}