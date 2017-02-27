using System;
using System.Threading.Tasks;

namespace DotNetty.Rpc.Client
{
    using System.Net;
    using System.Threading;
    using DotNetty.Codecs;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Rpc.Protocol;
    using DotNetty.Rpc.Service;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;

    public class NettyClient
    {
        const int ConnectTimeout = 10000;
        static readonly int Paralle = Math.Max(Environment.ProcessorCount / 2, 2);
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance("NettyClient");
        static readonly IEventLoopGroup WorkerGroup = new MultithreadEventLoopGroup(Paralle);

        private readonly ManualResetEventSlim emptyEvent = new ManualResetEventSlim(false, 1);
        private Bootstrap bootstrap;
        private RpcClientHandler clientRpcHandler;

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

                    pipeline.AddLast(new ReconnectHandler(this.DoConnect));

                    pipeline.AddLast(new RpcClientHandler());
                }));

            return this.DoConnect(socketAddress);
        }

        public async Task<T> SendRequest<T>(AbsMessage<T> request, int timeout = 10000) where T : IResult
        {
            if (this.clientRpcHandler == null || !this.clientRpcHandler.GetChannel().Active)
            {
                if (!this.emptyEvent.Wait(ConnectTimeout))
                {
                    throw new TimeoutException("Channel Connect TimeOut");
                }
            }

            if (this.clientRpcHandler == null)
            {
                throw new Exception("ClientRpcHandler Null");
            }
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
            return (T)rpcReponse.Result;
        }

        private Task DoConnect(EndPoint socketAddress)
        {
            this.emptyEvent.Reset();
;
            Task<IChannel> task = this.bootstrap.ConnectAsync(socketAddress);
            return task.ContinueWith(n =>
            {
                if (n.IsFaulted || n.IsCanceled)
                {
                    Logger.Info("NettyClient connected to {} failed", socketAddress);
                    if (this.clientRpcHandler != null)
                    {
                        IChannel channel0 = this.clientRpcHandler.GetChannel();
                        channel0.EventLoop.Schedule(_ => this.DoConnect((EndPoint)_), socketAddress, TimeSpan.FromMilliseconds(1000));
                    }
                    else
                    {
                        WorkerGroup.GetNext().Schedule(_ => this.DoConnect((EndPoint)_), socketAddress, TimeSpan.FromMilliseconds(1000));
                    }
                }
                else
                {
                    this.emptyEvent.Set();
                    Logger.Info("NettyClient connected to {}", socketAddress);
                    this.clientRpcHandler = n.Result.Pipeline.Get<RpcClientHandler>();
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }
}
