// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Performance.Utilities
{
    using DotNetty.Transport.Channels;

    public class ReadFinishedHandler : ChannelHandlerAdapter
    {
        readonly int expectedReads;
        readonly IReadFinishedSignal signal;
        int actualReads;

        public ReadFinishedHandler(IReadFinishedSignal signal, int expectedReads)
        {
            this.signal = signal;
            this.expectedReads = expectedReads;
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            if (++this.actualReads == this.expectedReads)
            {
                this.signal.Signal();
            }
            context.FireChannelRead(message);
        }
    }
}