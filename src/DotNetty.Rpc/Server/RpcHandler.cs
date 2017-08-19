namespace DotNetty.Rpc.Server
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Codecs;
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

                    IChannelHandlerContext ctx = state.Item1;
                    await WriteAndFlushAsync(ctx, rpcResponse);
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
        static async Task WriteAndFlushAsync(IChannelHandlerContext ctx, RpcResponse rpcResponse)
        {
            try
            {
                await ctx.WriteAndFlushAsync(rpcResponse);
            }
            catch (AggregateException ex)
            {
                Logger.Error(ex.InnerException);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            var decoderException = exception as DecoderException;
            if (decoderException != null)
            {
                Exception ex = decoderException.GetBaseException();
                var deserializeException = ex as DeserializeException;
                if (deserializeException != null)
                {
                    var rpcResponse = new RpcResponse
                    {
                        RequestId = deserializeException.RequestId,
                        Error = string.Format("DeserializeException,RpcMessage:{0}", deserializeException.RpcMessage)
                    };
                    context.WriteAndFlushAsync(rpcResponse);
                }
                else
                {
                    context.CloseAsync();
                    Logger.Error(exception);
                }
            }
            else
            {
                context.CloseAsync();
                Logger.Error(exception);
            }
        }
    }
}
