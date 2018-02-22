// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Performance.Utilities
{
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;
    using NBench;

    class CounterHandlerOutbound : ChannelHandlerAdapter
    {
        readonly Counter throughput;

        public CounterHandlerOutbound(Counter throughput)
        {
            this.throughput = throughput;
        }

        public override ChannelFuture WriteAsync(IChannelHandlerContext context, object message)
        {
            this.throughput.Increment();
            return context.WriteAsync(message);
        }

        public override bool IsSharable => true;
    }
}