using System;
using System.Threading.Tasks;

namespace DotNetty.Rpc.Client
{
    using System.Net;
    using System.Threading;
    using DotNetty.Codecs;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Rpc.Protocol;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;

    public class NettyClient
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance("NettyClient");
        static readonly IEventLoopGroup WorkerGroup = new MultithreadEventLoopGroup(Environment.ProcessorCount / 2);

        private readonly RpcClientHandler clientRpcHandler = new RpcClientHandler();
        private Bootstrap bootstrap;
        private IChannel channel;

        private volatile bool closed = false;

        internal Task Connect(EndPoint socketAddress)
        {
            this.bootstrap = new Bootstrap();
            this.bootstrap.Group(WorkerGroup)
                .Channel<TcpSocketChannel>()
                .Option(ChannelOption.TcpNodelay, true)
                .Option(ChannelOption.SoKeepalive, true)
                .Handler(new ActionChannelInitializer<ISocketChannel>(c =>
                {
                    IChannelPipeline pipeline = c.Pipeline;
                    pipeline.AddLast(new LengthFieldBasedFrameDecoder(int.MaxValue, 0, 4, 0, 0));

                    pipeline.AddLast(new RpcDecoder<RpcResponse>());
                    pipeline.AddLast(new RpcEncoder<RpcRequest>());

                    pipeline.AddLast(new ReconnectHandler(this.DoConnect, socketAddress));

                    pipeline.AddLast(this.clientRpcHandler);
                }));
            return this.DoConnect(socketAddress);
        }

        public async Task SendRequest<T>(AbsMessage<T> request, int timeout = 10000) where T : IResult
        {
            var rpcRequest = new RpcRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                Message = request
            };
            RpcResponse rpcReponse = await this.clientRpcHandler.SendRequest(rpcRequest, timeout);
            if (rpcReponse.Error != null)
            {
                throw new Exception(rpcReponse.Error);
            }
            request.ReturnValue = (T)rpcReponse.Result;
        }

        private Task DoConnect(EndPoint socketAddress)
        {
            if (this.closed)
            {
                throw new Exception("NettyClient closed");
            }

            Task<IChannel> task = this.bootstrap.ConnectAsync(socketAddress);
            return task.ContinueWith(n =>
            {
                if (n.IsFaulted || n.IsCanceled)
                {
                    Logger.Info("connected to {} failed", socketAddress);
                    IChannel channel0 = this.clientRpcHandler.GetChannel();
                    channel0.EventLoop.Schedule(_ => this.DoConnect((EndPoint)_), socketAddress, TimeSpan.FromMilliseconds(1000));
                }
                else
                {
                    Logger.Info("connected to {}", socketAddress);
                    this.channel = n.Result;
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        internal async Task Close()
        {
            await this.channel.CloseAsync();
            this.closed = true;
            this.channel = null;
        }

        public bool IsClosed() => this.closed;


    }
}
