namespace DotNetty.Rpc.Server
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Rpc.Protocol;
    using DotNetty.Transport.Channels;

    public class RpcHandler: ChannelHandlerAdapter
    {
        readonly Func<object, Task<object>> handler;

        public RpcHandler(Func<object, Task<object>> func)
        {
            this.handler = func;
        }

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
                        object result = await this.handler(state.Item2.Message);
                        rpcResponse.Result = result;
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

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) => context.CloseAsync();
    }
}
