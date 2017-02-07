namespace DotNetty.Rpc.Server
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Rpc.Exceptions;
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
                    try
                    {
                        rpcResponse.Result = await ServiceBus.Instance.Publish(state.Item2.Message);
                    }
                    catch (Exception ex)
                    {
                        rpcResponse.Error = ex.Message;
                    }
                    await state.Item1.WriteAndFlushAsync(rpcResponse);
                },
                Tuple.Create(context, request),
                default(CancellationToken),
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            base.ExceptionCaught(context, exception);

            var deserializeException = exception as DeserializeException;
            if (deserializeException != null)
            {
                var rpcResponse = new RpcResponse
                {
                    RequestId = deserializeException.RequestId,
                    Error = "DeserializeException"
                };
                context.WriteAndFlushAsync(rpcResponse);
            }
            else
            {
                context.CloseAsync();
            }
            Logger.Error(exception);
        }
    }
}
