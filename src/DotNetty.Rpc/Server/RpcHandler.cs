namespace DotNetty.Rpc.Server
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Codecs;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Handlers.Timeout;
    using DotNetty.Rpc.Protocol;
    using DotNetty.Rpc.Service;
    using DotNetty.Transport.Channels;

    public class RpcHandler: ChannelHandlerAdapter
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance("RpcHandler");

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            var request = (RpcRequest) message;
            Task.Factory.StartNew(
                async o =>
                {
                    var state = (Tuple<IChannelHandlerContext, RpcRequest>) o;
                    var rpcResponse = new RpcResponse
                    {
                        RequestId = state.Item2.RequestId
                    };

                    if (request.RequestId == "-1")
                    {
                        rpcResponse.Result = "pong";
                    }
                    else
                    {
                        try
                        {
                            rpcResponse.Result = await ServiceBus.Instance.Publish(state.Item2.Message);
                        }
                        catch (Exception ex)
                        {
                            rpcResponse.Error = ex.Message;
                        }
                    }

                    IChannelHandlerContext ctx = state.Item1;
                    WriteAndFlushAsync(ctx, rpcResponse);
                },
                Tuple.Create(context, request),
                default(CancellationToken),
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);
        }

        /// <summary>
        /// WriteAndFlushAsync
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="rpcResponse"></param>
        /// <returns></returns>
        static void WriteAndFlushAsync(IChannelHandlerContext ctx, RpcResponse rpcResponse)
        {
            ctx.WriteAndFlushAsync(rpcResponse).ContinueWith(n =>
            {
                if (n.IsFaulted)
                {
                    Logger.Error(n.Exception);
                }
            });
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
                        Logger.Debug("WriterIdle context.CloseAsync");
                    }
                    context.CloseAsync();
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
