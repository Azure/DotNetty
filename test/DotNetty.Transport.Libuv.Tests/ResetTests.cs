// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using Xunit;

    using static TestUtil;

    [Collection(LibuvTransport)]
    public sealed class ResetTests : IDisposable
    {
        readonly IEventLoopGroup group;
        IChannel serverChannel;
        IChannel clientChannel;

        public ResetTests()
        {
            this.group = new EventLoopGroup(1);
        }

        [Fact]
        public void ResetOnClose()
        {
            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.group)
                .Channel<TcpServerChannel>();
            Bootstrap cb = new Bootstrap()
                .Group(this.group)
                .Channel<TcpChannel>();
            this.Run(sb, cb);
        }

        void Run(ServerBootstrap sb, Bootstrap cb)
        {
            var serverInitializer = new ServerInitializer();
            sb.ChildHandler(serverInitializer);

            var clientInitializer = new ClientInitializer();
            cb.Handler(clientInitializer);

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

            Assert.True(serverInitializer.Initialized.Wait(DefaultTimeout), "Channel initialize timed out");
            Assert.True(serverInitializer.Close());

            // Server connection closed will cause the client
            // to receive EOF and the channel should go inactive 
            Assert.True(clientInitializer.Inactive.Wait(DefaultTimeout), "TcpChannel should fire Inactive if the server connection is closed.");
            Assert.Null(clientInitializer.ErrorCaught);
        }

        sealed class ServerInitializer : ChannelInitializer<IChannel>
        {
            readonly TaskCompletionSource completion;
            IChannel serverChannel;

            public ServerInitializer()
            {
                this.completion = new TaskCompletionSource();
            }

            public Task Initialized => this.completion.Task;

            public bool Close() => this.serverChannel.CloseAsync().Wait(DefaultTimeout);

            protected override void InitChannel(IChannel channel)
            {
                if (Interlocked.CompareExchange(ref this.serverChannel, channel, null) == null)
                {
                    this.completion.TryComplete();
                }
            }
        }

        sealed class ClientInitializer : ChannelInitializer<IChannel>
        {
            readonly ClientHandler handler;

            public ClientInitializer()
            {
                this.handler = new ClientHandler();
            }

            public Task Inactive => this.handler.Inactive;

            public Exception ErrorCaught => this.handler.Error;

            protected override void InitChannel(IChannel channel) => channel.Pipeline.AddLast(this.handler);
        }

        sealed class ClientHandler : ChannelHandlerAdapter
        {
            readonly TaskCompletionSource completion;
            internal Exception Error;

            public ClientHandler()
            {
                this.completion = new TaskCompletionSource();
            }

            public Task Inactive => this.completion.Task;

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) =>
                Interlocked.CompareExchange(ref this.Error, exception, null);

            public override void ChannelInactive(IChannelHandlerContext context) => this.completion.TryComplete();
        }

        public void Dispose()
        {
            this.clientChannel?.CloseAsync().Wait(DefaultTimeout);
            this.serverChannel?.CloseAsync().Wait(DefaultTimeout);
            this.group.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero).Wait(DefaultTimeout);
        }
    }
}
