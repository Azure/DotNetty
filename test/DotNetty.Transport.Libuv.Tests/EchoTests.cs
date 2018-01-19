// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using Xunit;
    using Xunit.Abstractions;

    using static TestUtil;

    [Collection(LibuvTransport)]
    public sealed class EchoTests : IDisposable
    {
        readonly ITestOutputHelper output;
        readonly Random random = new Random();
        readonly byte[] data = new byte[1048576];
        readonly IEventLoopGroup group;
        IChannel serverChannel;
        IChannel clientChannel;

        public EchoTests(ITestOutputHelper output)
        {
            this.output = output;
            this.group = new EventLoopGroup(1);
        }

        [Fact]
        public void SimpleEcho() => this.Run(false, true);

        [Fact]
        public void SimpleEchoNotAutoRead() => this.Run(false, false);

        [Fact]
        public void SimpleEchoWithAdditionalExecutor() => this.Run(true, true);

        [Fact]
        public void SimpleEchoWithAdditionalExecutorNotAutoRead() => this.Run(true, false);

        void Run(bool additionalExecutor, bool autoRead)
        {
            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.group)
                .Channel<TcpServerChannel>();
            Bootstrap cb = new Bootstrap()
                .Group(this.group)
                .Channel<TcpChannel>();
            this.SimpleEcho0(sb, cb, additionalExecutor, autoRead);
        }

        void SimpleEcho0(ServerBootstrap sb, Bootstrap cb, bool additionalExecutor, bool autoRead)
        {
            var sh = new EchoHandler(autoRead, this.data, this.output);
            var ch = new EchoHandler(autoRead, this.data, this.output);

            if (additionalExecutor)
            {
                sb.ChildHandler(new ActionChannelInitializer<TcpChannel>(channel =>
                {
                    channel.Pipeline.AddLast(this.group, sh);
                }));
                cb.Handler(new ActionChannelInitializer<TcpChannel>(channel =>
                {
                    channel.Pipeline.AddLast(this.group, ch);
                }));
            }
            else
            {
                sb.ChildHandler(sh);
                sb.Handler(new ErrorOutputHandler(this.output));
                cb.Handler(ch);
            }
            sb.ChildOption(ChannelOption.AutoRead, autoRead);
            cb.Option(ChannelOption.AutoRead, autoRead);

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

            for (int i = 0; i < this.data.Length;)
            {
                int length = Math.Min(this.random.Next(1024 * 64), this.data.Length - i);
                IByteBuffer buf = Unpooled.WrappedBuffer(this.data, i, length);
                this.clientChannel.WriteAndFlushAsync(buf);
                i += length;
            }

            Assert.True(Task.WhenAll(ch.Completion, sh.Completion).Wait(DefaultTimeout), "Echo read/write timed out");
        }

        sealed class ErrorOutputHandler : ChannelHandlerAdapter
        {
            readonly ITestOutputHelper output;

            public ErrorOutputHandler(ITestOutputHelper output)
            {
                this.output = output;
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause) => this.output.WriteLine(cause.StackTrace);
        }

        sealed class EchoHandler : SimpleChannelInboundHandler<IByteBuffer>
        {
            readonly bool autoRead;
            readonly byte[] expected;
            readonly ITestOutputHelper output;
            readonly TaskCompletionSource completion;

            IChannel channel;
            int counter;

            public EchoHandler(bool autoRead, byte[] expected, ITestOutputHelper output)
            {
                this.autoRead = autoRead;
                this.expected = expected;
                this.output = output;
                this.completion = new TaskCompletionSource();
            }

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                this.channel = ctx.Channel;
                if (!this.autoRead)
                {
                    ctx.Read();
                }
            }

            public Task Completion => this.completion.Task;

            protected override void ChannelRead0(IChannelHandlerContext ctx, IByteBuffer msg)
            {
                var actual = new byte[msg.ReadableBytes];
                msg.ReadBytes(actual);
                int lastIdx = this.counter;
                for (int i = 0; i < actual.Length; i++)
                {
                    Assert.Equal(this.expected[i + lastIdx], actual[i]);
                }

                if (this.channel.Parent != null)
                {
                    this.channel.WriteAsync(Unpooled.WrappedBuffer(actual));
                }

                this.counter += actual.Length;
                if (this.counter == this.expected.Length)
                {
                    this.completion.TryComplete();
                }
            }

            public override void ChannelReadComplete(IChannelHandlerContext ctx)
            {
                try
                {
                    ctx.Flush();
                }
                finally
                {
                    if (!this.autoRead)
                    {
                        ctx.Read();
                    }
                }
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                this.output.WriteLine(cause.StackTrace);
                ctx.CloseAsync();
                this.completion.TrySetException(cause);
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
