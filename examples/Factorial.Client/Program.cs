// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Factorial.Client
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using DotNetty.Handlers.Logging;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Examples.Common;

    class Program
    {
        static async Task RunClientAsync()
        {
            ExampleHelper.SetConsoleLogger();

            var group = new MultithreadEventLoopGroup();

            X509Certificate2 cert = null;
            string targetHost = null;
            if (Examples.Common.ClientSettings.IsSsl)
            {
                cert = new X509Certificate2(Path.Combine(ExampleHelper.ProcessDirectory, "dotnetty.com.pfx"), "password");
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

                        pipeline.AddLast(new LoggingHandler("CONN"));
                        pipeline.AddLast(new BigIntegerDecoder());
                        pipeline.AddLast(new NumberEncoder());
                        pipeline.AddLast(new FactorialClientHandler());
                    }));

                IChannel bootstrapChannel = await bootstrap.ConnectAsync(new IPEndPoint(Examples.Common.ClientSettings.Host, Examples.Common.ClientSettings.Port));

                // Get the handler instance to retrieve the answer.
                var handler = (FactorialClientHandler)bootstrapChannel.Pipeline.Last();

                // Print out the answer.
                Console.WriteLine("Factorial of {0} is: {1}", ClientSettings.Count.ToString(), handler.GetFactorial().ToString());

                Console.ReadLine();

                await bootstrapChannel.CloseAsync();
            }
            finally
            {
                group.ShutdownGracefullyAsync().Wait(1000);
            }
        }

        public static void Main() => RunClientAsync().Wait();
    }
}