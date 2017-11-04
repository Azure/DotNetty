// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Pool
{
    using System.Threading.Tasks;

    /**
     * Called before a {@link Channel} will be returned via {@link ChannelPool#acquire()} or
     * {@link ChannelPool#acquire(Promise)}.
     */
    public interface IChannelHealthChecker
    {
        /**
       * Check if the given channel is healthy which means it can be used. The returned {@link Future} is notified once
       * the check is complete. If notified with {@link Boolean#TRUE} it can be used {@link Boolean#FALSE} otherwise.
       *
       * This method will be called by the {@link EventLoop} of the {@link Channel}.
       */
        Task<bool> IsHealthyAsync(IChannel channel);
    }
}