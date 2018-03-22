// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Pool
{
    /**
     * Handler which is called for various actions done by the {@link ChannelPool}.
     */
    public interface IChannelPoolHandler
    {
        /**
         * Called once a {@link Channel} was released by calling {@link ChannelPool#release(Channel)} or
         * {@link ChannelPool#release(Channel, Promise)}.
         *
         * This method will be called by the {@link EventLoop} of the {@link Channel}.
         */
        void ChannelReleased(IChannel channel);

        /**
         * Called once a {@link Channel} was acquired by calling {@link ChannelPool#acquire()} or
         * {@link ChannelPool#acquire(Promise)}.
         *
         * This method will be called by the {@link EventLoop} of the {@link Channel}.
         */
        void ChannelAcquired(IChannel channel);

        /**
         * Called once a new {@link Channel} is created in the {@link ChannelPool}.
         *
         * This method will be called by the {@link EventLoop} of the {@link Channel}.
         */
        void ChannelCreated(IChannel channel);
    }
}