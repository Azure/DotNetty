// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Echo.Client
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
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
            //ExampleHelper.SetConsoleLogger();

            var group = new MultithreadEventLoopGroup();

            X509Certificate2 cert = null;
            string targetHost = null;
            //if (ClientSettings.IsSsl)
            //{
            //    cert = new X509Certificate2(Path.Combine(ExampleHelper.ProcessDirectory, "dotnetty.com.pfx"), "password");
            //    targetHost = cert.GetNameInfo(X509NameType.DnsName, false);
            //}
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
                            pipeline.AddLast("tls", new TlsHandler(stream => new SslStream(stream, true, (sender, certificate, chain, errors) => true), new ClientTlsSettings(targetHost)));
                        }
                        //pipeline.AddLast(new LoggingHandler());
                        pipeline.AddLast("framing-enc", new LengthFieldPrepender(2));
                        pipeline.AddLast("framing-dec", new LengthFieldBasedFrameDecoder(ushort.MaxValue, 0, 2, 0, 2));

                        pipeline.AddLast("echo", new EchoClientHandler());
                    }));

                IChannel clientChannel = await bootstrap.ConnectAsync(new IPEndPoint(ClientSettings.Host, ClientSettings.Port));

                //await clientChannel.WriteAndFlushAsync(await MockMessageAsync());

                await MockSendMessagesAsync(clientChannel);

                Console.ReadLine();

                await clientChannel.CloseAsync();
            }
            finally
            {
                await group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
            }
        }

        static void Main() => RunClientAsync().Wait();

        const string HelloWorld = "Hello World!";
        static Task<IByteBuffer> MockMessageAsync(string message = null)
        {
            if (string.IsNullOrEmpty(message))
                message = HelloWorld;

            var ubf = Unpooled.Buffer(ClientSettings.Size);
            var messageBytes = Encoding.UTF8.GetBytes(message);
            ubf.WriteBytes(messageBytes);

            return Task.FromResult(ubf);
        }

        static int count = 100000;
        static Task MockSendMessagesAsync(IChannel channel)
        {
            while (true)
            {
                var cde = new System.Threading.CountdownEvent(count);
                var sw = new Stopwatch();
                sw.Start();

                for (int i = 0; i < count; i++)
                {
                    Task.Run(async () =>
                    {
                        await channel.WriteAndFlushAsync(await MockMessageAsync($"Hi DotNetty_{DateTime.Now}"));
                    }).ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            Console.WriteLine(t.Exception);
                        }

                        cde.Signal();
                    });
                }

                cde.Wait();
                sw.Stop();

                Console.WriteLine($"{count}================>{sw.ElapsedMilliseconds}");

                Thread.Sleep(2000);

            }

        }


    }
}