// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace QuoteOfTheMoment.Server
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Handlers.Logging;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Examples.Common;

    class Program
    {
        static async Task RunServerAsync()
        {
            ExampleHelper.SetConsoleLogger();

            var group = new MultithreadEventLoopGroup();
            try
            {
                var bootstrap = new Bootstrap();
                bootstrap
                    .Group(group)
                    .Channel<SocketDatagramChannel>()
                    .Option(ChannelOption.SoBroadcast, true)
                    .Handler(new LoggingHandler("SRV-LSTN"))
                    .Handler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        channel.Pipeline.AddLast("Quote", new QuoteOfTheMomentServerHandler());
                    }));

                IChannel boundChannel = await bootstrap.BindAsync(ServerSettings.Port);
                Console.WriteLine("Press any key to terminate the server.");
                Console.ReadLine();

                await boundChannel.CloseAsync();
            }
            finally
            {
                await group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
            }
        }

        static void Main() => RunServerAsync().Wait();
    }
}