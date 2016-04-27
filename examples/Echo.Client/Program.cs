// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Echo.Client
{
    using System;
    using System.Diagnostics.Tracing;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using DotNetty.Codecs;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;

    class Program
    {
        static async Task RunClient()
        {
            var eventListener = new ObservableEventListener();
            eventListener.LogToConsole();
            eventListener.EnableEvents(DefaultEventSource.Log, EventLevel.Verbose);

            var group = new MultithreadEventLoopGroup();

            try
            {
                var bootstrap = new Bootstrap();
                bootstrap
                    .Group(group)
                    .Channel<TcpSocketChannel>()
                    .Option(ChannelOption.TcpNodelay, true)
                    .Handler(new ActionChannelInitializer<ISocketChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;

                        if (EchoClientSettings.IsSsl)
                        {
                            var cert = new X509Certificate2("dotnetty.com.pfx", "password");
                            string targetHost = cert.GetNameInfo(X509NameType.DnsName, false);
                            pipeline.AddLast(TlsHandler.Client(targetHost, null, (sender, certificate, chain, errors) => true));
                        }
                        pipeline.AddLast(new LengthFieldPrepender(2));
                        pipeline.AddLast(new LengthFieldBasedFrameDecoder(ushort.MaxValue, 0, 2, 0, 2));

                        pipeline.AddLast(new EchoClientHandler());
                    }));

                IChannel bootstrapChannel = await bootstrap.ConnectAsync(new IPEndPoint(EchoClientSettings.Host, EchoClientSettings.Port));

                Console.ReadLine();

                await bootstrapChannel.CloseAsync();
            }
            finally
            {
                group.ShutdownGracefullyAsync().Wait(1000);
                eventListener.Dispose();
            }
        }

        static void Main(string[] args)
        {
            Task.Run(() => RunClient()).Wait();
        }
    }
}