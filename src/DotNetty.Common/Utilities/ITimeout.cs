// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    /// <summary>
    /// A handle associated with a <see cref="ITimerTask"/> that is returned by a
    /// <see cref="ITimer"/>.
    /// </summary>
    public interface ITimeout
    {
        /// <summary>
        /// Returns the <see cref="ITimer"/> that created this handle.
        /// </summary>
        ITimer Timer { get; }

        /// <summary>
        /// Returns the <see cref="ITimerTask"/> which is associated with this handle.
        /// </summary>
        ITimerTask Task { get; }

        /// <summary>
        /// Returns <c>true</c> if and only if the <see cref="ITimerTask"/> associated
        /// with this handle has been expired.
        /// </summary>
        bool Expired { get; }

        /// <summary>
        /// Returns <c>true</c> if and only if the <see cref="ITimerTask"/> associated
        /// with this handle has been canceled.
        /// </summary>
        bool Canceled { get; }

        /// <summary>
        /// Attempts to cancel the <see cref="ITimerTask"/> associated with this handle.
        /// If the task has been executed or canceled already, it will return with
        /// no side effect.
        /// </summary>
        /// <returns><c>true</c> if the cancellation completed successfully, otherwise <c>false</c>.</returns>
        bool Cancel();
    }
}