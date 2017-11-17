// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Pool
{
    using System.Threading;

    public sealed class CountingChannelPoolHandler : IChannelPoolHandler
    {
        int channelCount;
        int acquiredCount;
        int releasedCount;
        
        public int ChannelCount => this.channelCount;

        public int AcquiredCount => this.acquiredCount;

        public int ReleasedCount => this.releasedCount;

        public void ChannelCreated(IChannel ch) => Interlocked.Increment(ref this.channelCount);

        public void ChannelReleased(IChannel ch) => Interlocked.Increment(ref this.releasedCount);

        public void ChannelAcquired(IChannel ch) => Interlocked.Increment(ref this.acquiredCount);
    }
}