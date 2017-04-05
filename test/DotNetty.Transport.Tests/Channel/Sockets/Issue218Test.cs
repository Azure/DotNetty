// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Channel.Sockets
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Tests.Common;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit;
    using Xunit.Abstractions;

    [Collection("Issue 218(WriteAndFlush on IOCP's IO thread) Test")]
    public class Issue218Test : TestBase
    {
        private const int blockSize = 1024;
        private const int repeatCount = 64;
        private const int DefaultTimeOutInMilliseconds = 800;

        public Issue218Test(ITestOutputHelper output)
            : base(output)
        {
        }

        private class Issue218ClientHandler : SimpleChannelInboundHandler<IByteBuffer>
        {
            private TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            public Exception LastException { get; private set; }
            public Task Tsk => tcs.Task;

            public override void ChannelActive(IChannelHandlerContext context)
            {
                DoTestAsync(context.Channel);
            }

            private async void DoTestAsync(IChannel channel)
            {
                try
                {
                    for (int i = 0; i < repeatCount; i++)
                    {
                        //The server handler can verify the output(order, count and values) if needed.
                        var buf = Unpooled.WrappedBuffer(new byte[blockSize]).SetInt(0, i);
                        await channel.WriteAndFlushAsync(buf);
                    }
                    tcs.TrySetResult(0);
                }
                catch (Exception ex)
                {
                    MarkException(ex);
                }
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, IByteBuffer msg)
            {
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                MarkException(exception);
                context.Channel.CloseAsync();
            }

            private void MarkException(Exception ex)
            {
                if (ex is AggregateException)
                    ex = ((AggregateException)ex).GetBaseException();

                if (LastException == null)
                {
                    LastException = ex;
                    tcs.TrySetException(ex);
                }
            }
        }

        public class DummyHandler : SimpleChannelInboundHandler<IByteBuffer>
        {
            protected override void ChannelRead0(IChannelHandlerContext ctx, IByteBuffer msg)
            {
            }
        }

        [Fact]
        public void IOCPSend()
        {
            var addressFamily = AddressFamily.InterNetwork;

            TcpServerSocketChannel serverChannel = null;
            IChannel clientChannel = null;
            var serverGroup = new MultithreadEventLoopGroup(1);
            var clientGroup = new MultithreadEventLoopGroup(1);
            try
            {
                var serverBootstrap = new ServerBootstrap();
                serverBootstrap
                    .Group(serverGroup)
                    .Channel<TcpServerSocketChannel>()
                    .Option(ChannelOption.SoRcvbuf, blockSize * 2)
                    .Handler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        channel.Pipeline.AddLast("Dummy", new DummyHandler());
                    }))
                    //.ChildOption(ChannelOption.AutoRead, false)
                    .ChildHandler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        channel.Pipeline.AddLast("Dummy", new DummyHandler());
                    }));

                var address = addressFamily == AddressFamily.InterNetwork ? IPAddress.Loopback : IPAddress.IPv6Loopback;
                this.Output.WriteLine($"TCP(IOCP) server binding to:({addressFamily}){address}");
                Task<IChannel> task = serverBootstrap.BindAsync(address, IPEndPoint.MinPort);

                Assert.True(task.Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds * 5)),
                    $"TCP(IOCP) server binding to:({addressFamily}){address} timed out!");

                serverChannel = (TcpServerSocketChannel)task.Result;
                var endPoint = (IPEndPoint)serverChannel.LocalAddress;

                var clientBootstrap = new Bootstrap();
                var clientHandler = new Issue218ClientHandler();
                clientBootstrap
                    .Group(clientGroup)
                    .Channel<TcpSocketChannel>()
                    .Option(ChannelOption.TcpNodelay, true)
                    .Option(ChannelOption.SoSndbuf, blockSize * 2)
                    .Handler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        channel.Pipeline.AddLast("Main", clientHandler);
                    }));

                var clientEndPoint = new IPEndPoint(
                    addressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any,
                    IPEndPoint.MinPort);

                clientBootstrap
                    .LocalAddress(clientEndPoint);

                this.Output.WriteLine($"TCP(IOCP) client binding to:({addressFamily}){address}");
                task = clientBootstrap.ConnectAsync(new IPEndPoint(address, endPoint.Port));

                Assert.True(task.Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds * 5)),
                    $"TCP(IOCP) client binding to:({clientEndPoint}) timed out!");
                clientChannel = task.Result;

                Assert.True(clientHandler.Tsk.Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds * 5)),
                    $"TCP(IOCP) server binding to:({addressFamily}){address} timed out!");

                Assert.Null(clientHandler.LastException);
            }
            finally
            {
                serverChannel?.CloseAsync().Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds));
                clientChannel?.CloseAsync().Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds));

                Task.WaitAll(
                    serverGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
                    clientGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)));
            }
        }
    }
}
