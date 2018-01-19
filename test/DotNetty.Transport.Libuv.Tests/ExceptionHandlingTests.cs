// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using Xunit;

    using static TestUtil;

    [Collection(LibuvTransport)]
    public sealed class ExceptionHandlingTests : IDisposable
    {
        readonly IEventLoopGroup group;
        IChannel serverChannel;
        IChannel clientChannel;

        public ExceptionHandlingTests()
        {
            this.group = new EventLoopGroup(1);
        }

        [Fact]
        public void ReadPendingIsResetAfterEachRead()
        {
            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.group)
                .Channel<TcpServerChannel>();
            Bootstrap cb = new Bootstrap()
                .Group(this.group)
                .Channel<TcpChannel>();
            this.ReadPendingIsResetAfterEachRead(sb, cb);
        }

        void ReadPendingIsResetAfterEachRead(ServerBootstrap sb, Bootstrap cb)
        {
            var serverInitializer = new MyInitializer();
            sb.Option(ChannelOption.SoBacklog, 1024);
            sb.ChildHandler(serverInitializer);

            // start server
            Task<IChannel> task = sb.BindAsync(LoopbackAnyPort);
            Assert.True(task.Wait(DefaultTimeout), "Server bind timed out");
            this.serverChannel = task.Result;
            Assert.NotNull(this.serverChannel.LocalAddress);
            var endPoint = (IPEndPoint)this.serverChannel.LocalAddress;

            cb.Handler(new MyInitializer());
            // connect to server
            task = cb.ConnectAsync(endPoint);
            Assert.True(task.Wait(DefaultTimeout), "Connect to server timed out");
            this.clientChannel = task.Result;
            Assert.NotNull(this.clientChannel.LocalAddress);

            Task writeTask = this.clientChannel.WriteAndFlushAsync(Unpooled.WrappedBuffer(new byte[1024]));
            Assert.True(writeTask.Wait(DefaultTimeout), "Write task timed out");

            ExceptionHandler exceptionHandler = serverInitializer.ErrorHandler;
            Assert.True(exceptionHandler.Inactive.Wait(DefaultTimeout), "Handler inactive timed out");
            Assert.Equal(1, exceptionHandler.Count);
        }

        sealed class MyInitializer : ChannelInitializer<IChannel>
        {
            public ExceptionHandler ErrorHandler { get; } = new ExceptionHandler();

            protected override void InitChannel(IChannel ch)
            {
                IChannelPipeline pipeline = ch.Pipeline;

                pipeline.AddLast(new BuggyChannelHandler());
                pipeline.AddLast(this.ErrorHandler);
            }
        }

        sealed class BuggyChannelHandler : ChannelHandlerAdapter
        {
            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                ReferenceCountUtil.Release(msg);
                throw new NullReferenceException("I am a bug!");
            }
        }

        sealed class ExceptionHandler : ChannelHandlerAdapter
        {
            readonly TaskCompletionSource completionSource;
            int count;

            public ExceptionHandler()
            {
                this.completionSource = new TaskCompletionSource();
            }

            public Task Inactive => this.completionSource.Task;

            public int Count => this.count;

            // We expect to get 1 call to ExceptionCaught
            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception exception)
            {
                Interlocked.Increment(ref this.count);
                // This should not throw any exception
                ctx.CloseAsync();
            }

            public override void ChannelInactive(IChannelHandlerContext context)
            {
                this.completionSource.TryComplete();
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
