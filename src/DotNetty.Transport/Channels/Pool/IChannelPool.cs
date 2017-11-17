// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Pool
{
    using System;
    using System.Threading.Tasks;

    /**
     * Allows to acquire and release {@link Channel} and so act as a pool of these.
     */
    public interface IChannelPool : IDisposable
    {
        /**
         * Acquire a {@link Channel} from this {@link ChannelPool}. The returned {@link Future} is notified once
         * the acquire is successful and failed otherwise.
         *
         * <strong>Its important that an acquired is always released to the pool again, even if the {@link Channel}
         * is explicitly closed..</strong>
         */
        Task<IChannel> AcquireAsync();

        /**
         * Acquire a {@link Channel} from this {@link ChannelPool}. The given {@link Promise} is notified once
         * the acquire is successful and failed otherwise.
         *
         * <strong>Its important that an acquired is always released to the pool again, even if the {@link Channel}
         * is explicitly closed..</strong>
         */
        Task ReleaseAsync(IChannel channel);
    }
}