// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Echo.Server
{
    using System;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using DotNetty.Codecs;
    using DotNetty.Handlers.Logging;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Examples.Common;

    class Program
    {
        static async Task RunServerAsync()
        {
            ExampleHelper.SetConsoleLogger();

            var bossGroup = new MultithreadEventLoopGroup(1);
            var workerGroup = new MultithreadEventLoopGroup();
            X509Certificate2 tlsCertificate = null;
            if (ServerSettings.IsSsl)
            {
                tlsCertificate = new X509Certificate2(Path.Combine(ExampleHelper.ProcessDirectory, "dotnetty.com.pfx"), "password");
            }
            try
            {
                var bootstrap = new ServerBootstrap();
                bootstrap
                    .Group(bossGroup, workerGroup)
                    .Channel<TcpServerSocketChannel>()
                    .Option(ChannelOption.SoBacklog, 100)
                    .Handler(new LoggingHandler("SRV-LSTN"))
                    .ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;
                        if (tlsCertificate != null)
                        {
                            pipeline.AddLast("tls", TlsHandler.Server(tlsCertificate));
                        }
                        pipeline.AddLast(new LoggingHandler("SRV-CONN"));
                        pipeline.AddLast("framing-enc", new LengthFieldPrepender(2));
                        pipeline.AddLast("framing-dec", new LengthFieldBasedFrameDecoder(ushort.MaxValue, 0, 2, 0, 2));

                        pipeline.AddLast("echo", new EchoServerHandler());
                    }));

                IChannel boundChannel = await bootstrap.BindAsync(ServerSettings.Port);

                Console.ReadLine();

                await boundChannel.CloseAsync();
            }
            finally
            {
                await Task.WhenAll(
                    bossGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
                    workerGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)));
            }
        }

        static void Main() => RunServerAsync().Wait();
    }
}