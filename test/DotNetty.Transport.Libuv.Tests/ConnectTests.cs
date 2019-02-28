// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Handlers.Logging;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;
    using Xunit;

    using static TestUtil;

    [Collection(LibuvTransport)]
    public sealed class ConnectTests : IDisposable
    {
        readonly IEventLoopGroup group;
        IChannel serverChannel;
        IChannel clientChannel;

        public ConnectTests()
        {
            this.group = new EventLoopGroup(1);
        }

        [Fact]
        public void Connect()
        {
            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.group)
                .Channel<TcpServerChannel>()
                .ChildHandler(new ActionChannelInitializer<TcpChannel>(channel =>
                {
                    channel.Pipeline.AddLast("server logger", new LoggingHandler($"{nameof(ConnectTests)}"));
                }));

            Bootstrap cb = new Bootstrap()
                .Group(this.group)
                .Channel<TcpChannel>()
                .Handler(new ActionChannelInitializer<TcpChannel>(channel =>
                {
                    channel.Pipeline.AddLast("client logger", new LoggingHandler($"{nameof(ConnectTests)}"));
                }));

            // start server
            Task<IChannel> task = sb.BindAsync(LoopbackAnyPort);
            Assert.True(task.Wait(DefaultTimeout), "Server bind timed out");
            this.serverChannel = task.Result;
            Assert.NotNull(this.serverChannel.LocalAddress);
            var endPoint = (IPEndPoint)this.serverChannel.LocalAddress;

            // connect to server
            task = cb.ConnectAsync(endPoint);
            Assert.True(task.Wait(DefaultTimeout), "Connect to server timed out");
            this.clientChannel = task.Result;
            Assert.NotNull(this.clientChannel.LocalAddress);
        }

        [Fact]
        public void MultipleConnect()
        {
            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.group)
                .Channel<TcpServerChannel>()
                .ChildHandler(new ChannelHandlerAdapter());

            var address = new IPEndPoint(IPAddress.IPv6Loopback, 0);
            // start server
            Task<IChannel> task = sb.BindAsync(address);
            Assert.True(task.Wait(DefaultTimeout), "Server bind timed out");
            this.serverChannel = task.Result;
            Assert.NotNull(this.serverChannel.LocalAddress);
            var endPoint = (IPEndPoint)this.serverChannel.LocalAddress;

            Bootstrap cb = new Bootstrap()
                .Group(this.group)
                .Channel<TcpChannel>()
                .Handler(new ChannelHandlerAdapter());

            // connect to server
            task = (Task<IChannel>)cb.RegisterAsync();
            Assert.True(task.Wait(DefaultTimeout));
            this.clientChannel = task.Result;
            Task connectTask = this.clientChannel.ConnectAsync(endPoint);
            Assert.True(connectTask.Wait(DefaultTimeout), "Connect to server timed out");

            // Attempt to connect again
            connectTask = this.clientChannel.ConnectAsync(endPoint);
            var exception = Assert.Throws<AggregateException>(() => connectTask.Wait(DefaultTimeout));
            Assert.IsType<OperationException>(exception.InnerExceptions[0]);
            var operationException = (OperationException)exception.InnerExceptions[0];
            Assert.Equal("EISCONN", operationException.Name); // socket is already connected
        }

        public void Dispose()
        {
            this.clientChannel?.CloseAsync().Wait(DefaultTimeout);
            this.serverChannel?.CloseAsync().Wait(DefaultTimeout);
            this.group.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero).Wait(DefaultTimeout);
        }
    }
}
