using System;

namespace DotNetty.Rpc.Client
{
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Channels;

    public class ReconnectHandler : ChannelHandlerAdapter
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance("ReconnectHandler");
        readonly Func<EndPoint, Task> doConnect;

        public ReconnectHandler(Func<EndPoint, Task> doConnectFunc)
        {
            this.doConnect = doConnectFunc;
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            base.ChannelInactive(context);
            Logger.Warn("ChannelInactive connected to {}", context.Channel.RemoteAddress);
            context.Channel.EventLoop.Schedule(_ => this.doConnect((EndPoint)_), context.Channel.RemoteAddress, TimeSpan.FromMilliseconds(1000));
        }
    }
}
