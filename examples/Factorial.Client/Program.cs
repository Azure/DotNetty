// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Factorial.Client
{
    using System;
    using System.Diagnostics.Tracing;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;

    class Program
    {
        static async Task RunClientAsync()
        {
            var eventListener = new ObservableEventListener();
            eventListener.LogToConsole();
            eventListener.EnableEvents(DefaultEventSource.Log, EventLevel.Verbose);

            var group = new MultithreadEventLoopGroup();

            X509Certificate2 cert = null;
            string targetHost = null;
            if (FactorialClientSettings.IsSsl)
            {
                cert = new X509Certificate2("dotnetty.com.pfx", "password");
                targetHost = cert.GetNameInfo(X509NameType.DnsName, false);
            }
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

                        if (cert != null)
                        {
                            pipeline.AddLast(new TlsHandler(stream => new SslStream(stream, true, (sender, certificate, chain, errors) => true), new ClientTlsSettings(targetHost)));
                        }

                        pipeline.AddLast(new Factorial.BigIntegerDecoder());
                        pipeline.AddLast(new Factorial.NumberEncoder());
                        pipeline.AddLast(new FactorialClientHandler());
                    }));

                IChannel bootstrapChannel = await bootstrap.ConnectAsync(new IPEndPoint(FactorialClientSettings.Host, FactorialClientSettings.Port));

                Console.ReadLine();

                await bootstrapChannel.CloseAsync();
            }
            finally
            {
                group.ShutdownGracefullyAsync().Wait(1000);
                eventListener.Dispose();
            }
        }

        static void Main() => RunClientAsync().Wait();
    }
}