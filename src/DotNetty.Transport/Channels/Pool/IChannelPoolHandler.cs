// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Pool
{
    /// <summary>
    /// Handler which is called for various actions done by the <see cref="IChannelPool"/>.
    /// </summary>
    public interface IChannelPoolHandler
    {
        /// <summary>
        /// Called once a <see cref="IChannel"/> was released by calling <see cref="IChannelPool.ReleaseAsync"/>.
        /// This method will be called by the <see cref="IEventLoop"/> of the <see cref="IChannel"/>.
        /// </summary>
        /// <param name="channel">The <see cref="IChannel"/> instance which was released.</param>
        void ChannelReleased(IChannel channel);

        /// <summary>
        /// Called once a <see cref="IChannel"/> was acquired by calling <see cref="IChannelPool.AcquireAsync"/>.
        /// </summary>
        /// <param name="channel">The <see cref="IChannel"/> instance which was aquired.</param>
        void ChannelAcquired(IChannel channel);

        /// <summary>
        /// Called once a new <see cref="IChannel"/> is created in the <see cref="IChannelPool"/>.
        /// </summary>
        /// <param name="channel">The <see cref="IChannel"/> instance which was aquired.</param>
        void ChannelCreated(IChannel channel);
    }
}