// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    /// <summary>
    /// A task which is executed after the delay specified with
    /// <see cref="ITimer.NewTimeout"/>.
    /// </summary>
    public interface ITimerTask
    {
        /// <summary>
        /// Executed after the delay specified with
        /// <see cref="ITimer.NewTimeout"/>.
        /// </summary>
        /// <param name="timeout">a handle which is associated with this task</param>
        void Run(ITimeout timeout);
    }
}