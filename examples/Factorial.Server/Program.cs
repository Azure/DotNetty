// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Factorial.Server
{
    using System;
    using System.Diagnostics.Tracing;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Handlers.Logging;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;

    class Program
    {
        static async Task RunServerAsync()
        {
            var eventListener = new ObservableEventListener();
            eventListener.LogToConsole();
            eventListener.EnableEvents(DefaultEventSource.Log, EventLevel.LogAlways);

            var bossGroup = new MultithreadEventLoopGroup(1);
            var workerGroup = new MultithreadEventLoopGroup();
            X509Certificate2 tlsCertificate = null;
            if (FactorialSettings.IsSsl)
            {
                tlsCertificate = new X509Certificate2("dotnetty.com.pfx", "password");
            }
            try
            {
                var bootstrap = new ServerBootstrap();
                bootstrap
                    .Group(bossGroup, workerGroup)
                    .Channel<TcpServerSocketChannel>()
                    .Option(ChannelOption.SoBacklog, 100)
                    .Handler(new LoggingHandler(LogLevel.INFO))
                    .ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;
                        if (tlsCertificate != null)
                        {
                            pipeline.AddLast(TlsHandler.Server(tlsCertificate));
                        }
                        pipeline.AddLast(new Factorial.NumberEncoder(), new Factorial.BigIntegerDecoder(), new FactorialServerHandler());
                    }));

                IChannel bootstrapChannel = await bootstrap.BindAsync(FactorialSettings.Port);

                Console.ReadLine();

                await bootstrapChannel.CloseAsync();
            }
            finally
            {
                Task.WaitAll(bossGroup.ShutdownGracefullyAsync(), workerGroup.ShutdownGracefullyAsync());
                eventListener.Dispose();
            }
        }

        static void Main() => RunServerAsync().Wait();
    }
}