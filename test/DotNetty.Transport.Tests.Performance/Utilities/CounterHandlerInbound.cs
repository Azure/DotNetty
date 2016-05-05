// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Performance.Utilities
{
    using DotNetty.Transport.Channels;
    using NBench;

    class CounterHandlerInbound : ChannelHandlerAdapter
    {
        readonly Counter throughput;

        public CounterHandlerInbound(Counter throughput)
        {
            this.throughput = throughput;
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            this.throughput.Increment();
            context.FireChannelRead(message);
        }

        public override bool IsSharable => true;
    }
}