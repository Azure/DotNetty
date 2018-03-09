// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Performance.Utilities
{
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

        public override void Write(IChannelHandlerContext context, object message, IPromise promise)
        {
            this.throughput.Increment();
            context.WriteAsync(message, promise);
        }

        public override bool IsSharable => true;
    }
}