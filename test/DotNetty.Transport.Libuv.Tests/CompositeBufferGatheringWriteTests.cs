// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using Xunit;

    using static TestUtil;

    [Collection(LibuvTransport)]
    public sealed class CompositeBufferGatheringWriteTests : IDisposable
    {
        const int ExpectedBytes = 20;

        readonly IEventLoopGroup group;
        IChannel serverChannel;
        IChannel clientChannel;

        public CompositeBufferGatheringWriteTests()
        {
            this.group = new EventLoopGroup(1);
        }

        [Fact]
        public void SingleCompositeBufferWrite()
        {
            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.group)
                .Channel<TcpServerChannel>();
            Bootstrap cb = new Bootstrap()
                .Group(this.group)
                .Channel<TcpChannel>();
            this.SingleCompositeBufferWrite(sb, cb);
        }

        void SingleCompositeBufferWrite(ServerBootstrap sb, Bootstrap cb)
        {
            sb.ChildHandler(new ActionChannelInitializer<TcpChannel>(channel =>
            {
                channel.Pipeline.AddLast(new ServerHandler());
            }));

            var clientHandler = new ClientHandler();
            cb.Handler(new ActionChannelInitializer<TcpChannel>(channel =>
            {
                channel.Pipeline.AddLast(clientHandler);
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

            IByteBuffer expected = NewCompositeBuffer(this.clientChannel.Allocator);
            clientHandler.AssertReceived(expected);
        }

        sealed class ClientHandler : ChannelHandlerAdapter
        {
            readonly TaskCompletionSource completion;

            IByteBuffer aggregator;

            public ClientHandler()
            {
                this.completion = new TaskCompletionSource();
            }

            public override void HandlerAdded(IChannelHandlerContext ctx)
            {
                this.aggregator = ctx.Allocator.Buffer(ExpectedBytes);
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                try
                {
                    if (msg is IByteBuffer buf)
                    {
                        this.aggregator.WriteBytes(buf);
                    }
                }
                finally
                {
                    ReferenceCountUtil.Release(msg);
                }
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception exception)
            {
                this.completion.TrySetException(exception);
            }

            public override void ChannelInactive(IChannelHandlerContext ctx)
            {
                this.completion.TryComplete();
            }

            public void AssertReceived(IByteBuffer expected)
            {
                try
                {
                    Assert.True(this.completion.Task.Wait(DefaultTimeout), "ChannelInactive timed out");
                    Assert.NotNull(this.aggregator);
                    Assert.Equal(ExpectedBytes, this.aggregator.ReadableBytes);
                    Assert.Equal(expected, this.aggregator);
                }
                finally
                {
                    expected.Release();
                    this.aggregator?.Release();
                }
            }
        }

        sealed class ServerHandler : ChannelHandlerAdapter
        {
            public override void ChannelActive(IChannelHandlerContext ctx) =>
                ctx.WriteAndFlushAsync(NewCompositeBuffer(ctx.Allocator))
                   .ContinueWith((t, s) => ((IChannelHandlerContext)s).CloseAsync(), 
                        ctx, TaskContinuationOptions.ExecuteSynchronously);
        }

        static IByteBuffer NewCompositeBuffer(IByteBufferAllocator alloc)
        {
            CompositeByteBuffer compositeByteBuf = alloc.CompositeBuffer();
            compositeByteBuf.AddComponent(true, alloc.DirectBuffer(4).WriteInt(100));
            compositeByteBuf.AddComponent(true, alloc.DirectBuffer(8).WriteLong(123));
            compositeByteBuf.AddComponent(true, alloc.DirectBuffer(8).WriteLong(456));
            Assert.Equal(ExpectedBytes, compositeByteBuf.ReadableBytes);
            return compositeByteBuf;
        }

        public void Dispose()
        {
            this.clientChannel?.CloseAsync().Wait(DefaultTimeout);
            this.serverChannel?.CloseAsync().Wait(DefaultTimeout);
            this.group.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero).Wait(DefaultTimeout);
        }
    }
}
