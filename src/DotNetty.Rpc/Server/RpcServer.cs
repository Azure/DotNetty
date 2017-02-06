namespace DotNetty.Rpc.Server
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Codecs;
    using DotNetty.Handlers.Timeout;
    using DotNetty.Rpc.Protocol;
    using DotNetty.Rpc.Service;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;

    public class RpcServer
    {
        static readonly int Paralle = Math.Max(Environment.ProcessorCount / 2, 2);
        readonly string ipAndPort;

        public RpcServer(string ipAndPort)
        {
            this.ipAndPort = ipAndPort;
        }

        public async Task StartAsync()
        {
            ModuleRegistration();

            var bossGroup = new MultithreadEventLoopGroup();
            var workerGroup = new MultithreadEventLoopGroup(Paralle);

            try
            {
                var bootstrap = new ServerBootstrap();
                bootstrap
                    .Group(bossGroup, workerGroup)
                    .Channel<TcpServerSocketChannel>()
                    .Option(ChannelOption.SoBacklog, 1024)
                    .ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;

                        pipeline.AddLast(new LengthFieldBasedFrameDecoder(int.MaxValue, 0, 4, 0, 0));
                        pipeline.AddLast(new RpcDecoder<RpcRequest>());
                        pipeline.AddLast(new RpcEncoder<RpcResponse>());
                        pipeline.AddLast(new RpcHandler());
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

        static void ModuleRegistration()
        {
            IModule[] modules = ModuleRegistrations.FindModules();
            foreach (IModule module in modules)
            {
                module.Initialize();
            }
        }
    }
}
