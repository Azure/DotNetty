// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using Xunit;

    using static TestUtil;

    [Collection(LibuvTransport)]
    public class CloseForciblyTests : IDisposable
    {
        readonly IEventLoopGroup group;
        IChannel serverChannel;
        IChannel clientChannel;

        public CloseForciblyTests()
        {
            this.group = new EventLoopGroup(1);
        }

        [Fact]
        public void CloseForcibly()
        {
            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.group)
                .Channel<TcpServerChannel>();
            Bootstrap cb = new Bootstrap()
                .Group(this.group)
                .Channel<TcpChannel>();
            this.CloseForcibly(sb, cb);
        }

        void CloseForcibly(ServerBootstrap sb, Bootstrap cb)
        {
            sb.Handler(new InboundHandler())
              .ChildHandler(new ChannelHandlerAdapter());
            cb.Handler(new ChannelHandlerAdapter());

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
            this.clientChannel.CloseAsync().Wait(DefaultTimeout);

            this.serverChannel.CloseAsync().Wait(DefaultTimeout);
        }

        sealed class InboundHandler : ChannelHandlerAdapter
        {
            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                var childChannel = (TcpChannel)message;
                childChannel.Unsafe.CloseForcibly();
            }
        }

        public void Dispose()
        {
            this.clientChannel?.CloseAsync().Wait(DefaultTimeout);
            this.serverChannel?.CloseAsync().Wait(DefaultTimeout);
            this.group.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero).Wait(DefaultTimeout);
        }
    }
}
