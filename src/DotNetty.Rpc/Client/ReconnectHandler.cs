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
        readonly EndPoint remotePeer;

        public ReconnectHandler(Func<EndPoint, Task> doConnectFunc, EndPoint endPoint)
        {
            this.doConnect = doConnectFunc;
            this.remotePeer = endPoint;
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            base.ChannelInactive(context);
            Logger.Warn("ChannelInactive connected to {}", context.Channel.RemoteAddress);
            context.Channel.EventLoop.Schedule(_ => this.doConnect((EndPoint)_), this.remotePeer, TimeSpan.FromMilliseconds(1000));
        }
    }
}
