using System;

namespace DotNetty.Transport.Tests.Performance.Utilities
{
    using DotNetty.Transport.Channels;
    using NBench;

    public class ErrorCounterHandler : ChannelHandlerAdapter
    {
        readonly Counter errorCount;

        public ErrorCounterHandler(Counter errorCount)
        {
            this.errorCount = errorCount;
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) => this.errorCount.Increment();
    }
}
