// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Pool
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Allows the acquisition and release of <see cref="IChannel"/> instances, and so act as a pool of these.
    /// </summary>
    public interface IChannelPool : IDisposable
    {
        /// <summary>
        /// Acquires an <see cref="IChannel"/> from this <see cref="IChannelPool"/>.
        /// <para>
        /// It is important that an acquired <see cref="IChannel"/> is always released to the pool again via the
        /// <see cref="ReleaseAsync"/> method, even if the <see cref="IChannel"/> is explicitly closed.
        /// </para>
        /// </summary>
        /// <returns>The aquired <see cref="IChannel"/>.</returns>
        ValueTask<IChannel> AcquireAsync();

        /// <summary>
        /// Releases a previously aquired <see cref="IChannel"/> from this <see cref="IChannelPool"/>, allowing it to
        /// be aquired again by another caller.
        /// </summary>
        /// <param name="channel">The <see cref="IChannel"/> instance to be released.</param>
        /// <returns>
        /// <c>true</c> if the <see cref="IChannel"/> was successfully released, otherwise <c>false</c>.
        /// </returns>
        ValueTask<bool> ReleaseAsync(IChannel channel);
    }
}