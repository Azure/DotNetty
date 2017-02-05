namespace DotNetty.Rpc.Server
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Codecs;
    using DotNetty.Rpc.Protocol;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;

    public class RpcServer
    {
        static readonly int Paralle = Environment.ProcessorCount / 2;
        readonly string ipAndPort;
        readonly Func<IMessage, Task<IResult>> handler;

        public RpcServer(string ipAndPort, Func<IMessage, Task<IResult>> func)
        {
            this.ipAndPort = ipAndPort;
            this.handler = func;
        }

        public async Task StartAsync()
        {
            var bossGroup = new MultithreadEventLoopGroup();
            var workerGroup = new MultithreadEventLoopGroup(Paralle);

            try
            {
                var bootstrap = new ServerBootstrap();
                bootstrap
                    .Group(bossGroup, workerGroup)
                    .Channel<TcpServerSocketChannel>()
                    .Option(ChannelOption.SoBacklog, 1024)
                    .Option(ChannelOption.SoKeepalive, true)
                    .ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;

                        pipeline.AddLast(new LengthFieldBasedFrameDecoder(int.MaxValue, 0, 4, 0, 0));
                        pipeline.AddLast(new RpcDecoder<RpcRequest>());
                        pipeline.AddLast(new RpcEncoder<RpcResponse>());
                        pipeline.AddLast(new RpcHandler(this.handler));
                    }));

                string[] parts = this.ipAndPort.Split(':');
                int port = int.Parse(parts[1]);

                IChannel bootstrapChannel = await bootstrap.BindAsync(port);

                Console.ReadKey();

                await bootstrapChannel.CloseAsync();
            }
            finally
            {
                Task.WaitAll(bossGroup.ShutdownGracefullyAsync(), workerGroup.ShutdownGracefullyAsync());
            }
        }
    }
}
