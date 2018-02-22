// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Groups
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    public interface IChannelGroup : ICollection<IChannel>, IComparable<IChannelGroup>
    {
        /// <summary>
        ///     Returns the name of this group.  A group name is purely for helping
        ///     you to distinguish one group from others.
        /// </summary>
        string Name { get; }

        IChannel Find(IChannelId id);

        ChannelFuture WriteAsync(object message);

        ChannelFuture WriteAsync(object message, IChannelMatcher matcher);

        IChannelGroup Flush();

        IChannelGroup Flush(IChannelMatcher matcher);

        ChannelFuture WriteAndFlushAsync(object message);

        ChannelFuture WriteAndFlushAsync(object message, IChannelMatcher matcher);

        Task DisconnectAsync();

        Task DisconnectAsync(IChannelMatcher matcher);

        Task CloseAsync();

        Task CloseAsync(IChannelMatcher matcher);

        Task DeregisterAsync();

        Task DeregisterAsync(IChannelMatcher matcher);

        Task NewCloseFuture();

        Task NewCloseFuture(IChannelMatcher matcher);
    }
}