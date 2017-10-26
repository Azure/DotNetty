namespace DotNetty.Rpc.Client
{
    using System;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Codecs;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Handlers.Timeout;
    using DotNetty.Rpc.Protocol;
    using DotNetty.Transport.Channels;

    public class RpcClientHandler : ChannelHandlerAdapter
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance("RpcClientHandler");
        private readonly ConcurrentDictionary<string, RequestContext> pendingRpc;
        private volatile IChannel channel;
        private EndPoint remotePeer;

        public RpcClientHandler()
        {
            this.pendingRpc = new ConcurrentDictionary<string, RequestContext>();
        }

        public IChannel GetChannel() => this.channel;

        public EndPoint GetRemotePeer() => this.remotePeer;

        public override void ChannelActive(IChannelHandlerContext context)
        {
            base.ChannelActive(context);
            this.remotePeer = this.channel.RemoteAddress;
        }

        public override void ChannelRegistered(IChannelHandlerContext context)
        {
            base.ChannelRegistered(context);
            this.channel = context.Channel;
        }


        public override void ChannelRead(IChannelHandlerContext ctx, object message)
        {
            var response = (RpcResponse)message;
            string requestId = response.RequestId;
            if (requestId == "-1")
            {
                if (Logger.DebugEnabled)
                {
                    Logger.Debug("get server response pong");
                }
            }
            else
            {
                RequestContext requestContext;
                this.pendingRpc.TryGetValue(requestId, out requestContext);
                if (requestContext != null)
                {
                    this.pendingRpc.TryRemove(requestId, out requestContext);
                    requestContext.TaskCompletionSource.SetResult(response);
                    requestContext.TimeOutTimer.Cancel();
                }
            }
        }

        public Task<RpcResponse> SendRequest(RpcRequest request, int timeout = 10000)
        {
            var tcs = new TaskCompletionSource<RpcResponse>();

            IScheduledTask timeOutTimer = this.channel.EventLoop.Schedule(n => this.GetRpcResponseTimeOut(n), request, TimeSpan.FromMilliseconds(timeout));

            var context = new RequestContext(tcs, timeOutTimer);

            this.pendingRpc.TryAdd(request.RequestId, context);

            this.channel.WriteAndFlushAsync(request).ContinueWith(n =>
            {
                if (n.IsFaulted)
                {
                    Logger.Error(n.Exception);
                }
            });

            return tcs.Task;
        }

        void GetRpcResponseTimeOut(object n)
        {
            string requestId = ((RpcRequest)n).RequestId;
            RequestContext requestContext;
            this.pendingRpc.TryGetValue(requestId, out requestContext);
            if (requestContext != null)
            {
                this.pendingRpc.TryRemove(requestId, out requestContext);
                requestContext.TaskCompletionSource.SetException(new Handlers.TimeoutException("Get RpcResponse TimeOut"));
            }
        }

        public override void UserEventTriggered(IChannelHandlerContext context, object evt)
        {
            if (evt is IdleStateEvent)
            {
                var e = (IdleStateEvent)evt;
                if (e.State == IdleState.ReaderIdle)
                {
                    if (Logger.DebugEnabled)
                    {
                        Logger.Debug("ReaderIdle context.CloseAsync");
                    }

                    context.CloseAsync();
                }
                else if (e.State == IdleState.WriterIdle)
                {
                    if (Logger.DebugEnabled)
                    {
                        Logger.Debug("WriterIdle send request ping");
                    }

                    context.WriteAndFlushAsync(new RpcRequest
                    {
                        RequestId = "-1",
                        Message = "ping"
                    });
                }
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Logger.Error(exception);

            context.CloseAsync();
        }
    }
}
