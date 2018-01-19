// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using Xunit;

    using static TestUtil;

    [Collection(LibuvTransport)]
    public sealed class AutoReadTests : IDisposable
    {
        readonly IEventLoopGroup group;
        IChannel serverChannel;
        IChannel clientChannel;

        public AutoReadTests()
        {
            this.group = new EventLoopGroup(1);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AutoReadOffDuringReadOnlyReadsOneTime(bool readOutsideEventLoopThread)
        {
            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.group)
                .Channel<TcpServerChannel>();
            Bootstrap cb = new Bootstrap()
                .Group(this.group)
                .Channel<TcpChannel>();
            this.AutoReadOffDuringReadOnlyReadsOneTime(readOutsideEventLoopThread, sb, cb);
        }

        void AutoReadOffDuringReadOnlyReadsOneTime(bool readOutsideEventLoopThread, ServerBootstrap sb, Bootstrap cb)
        {
            var serverInitializer = new AutoReadInitializer(!readOutsideEventLoopThread);
            var clientInitializer = new AutoReadInitializer(!readOutsideEventLoopThread);
            sb.Option(ChannelOption.SoBacklog, 1024)
              .Option(ChannelOption.AutoRead, true)
              .ChildOption(ChannelOption.AutoRead, true)
              // We want to ensure that we attempt multiple individual read operations per read loop so we can
              // test the auto read feature being turned off when data is first read.
              .ChildOption(ChannelOption.RcvbufAllocator, new TestRecvByteBufAllocator())
              .ChildHandler(serverInitializer);

            // start server
            Task<IChannel> task = sb.BindAsync(LoopbackAnyPort);
            Assert.True(task.Wait(DefaultTimeout), "Server bind timed out");
            this.serverChannel = task.Result;
            Assert.NotNull(this.serverChannel.LocalAddress);
            var endPoint = (IPEndPoint)this.serverChannel.LocalAddress;

            cb.Option(ChannelOption.AutoRead, true)
              // We want to ensure that we attempt multiple individual read operations per read loop so we can
              // test the auto read feature being turned off when data is first read.
              .Option(ChannelOption.RcvbufAllocator, new TestRecvByteBufAllocator())
              .Handler(clientInitializer);

            // connect to server
            task = cb.ConnectAsync(endPoint);
            Assert.True(task.Wait(DefaultTimeout), "Connect to server timed out");
            this.clientChannel = task.Result;
            Assert.NotNull(this.clientChannel.LocalAddress);

            // 3 bytes means 3 independent reads for TestRecvByteBufAllocator
            Task writeTask = this.clientChannel.WriteAndFlushAsync(Unpooled.WrappedBuffer(new byte[3]));
            Assert.True(writeTask.Wait(TimeSpan.FromSeconds(5)), "Client write task timed out");
            serverInitializer.AutoReadHandler.AssertSingleRead();

            // 3 bytes means 3 independent reads for TestRecvByteBufAllocator
            writeTask = serverInitializer.Channel.WriteAndFlushAsync(Unpooled.WrappedBuffer(new byte[3]));
            Assert.True(writeTask.Wait(TimeSpan.FromSeconds(5)), "Server write task timed out");
            clientInitializer.AutoReadHandler.AssertSingleRead();

            if (readOutsideEventLoopThread)
            {
                serverInitializer.Channel.Read();
            }
            serverInitializer.AutoReadHandler.AssertSingleReadSecondTry();

            if (readOutsideEventLoopThread)
            {
                this.clientChannel.Read();
            }
            clientInitializer.AutoReadHandler.AssertSingleReadSecondTry();
        }

        sealed class AutoReadInitializer : ChannelInitializer<IChannel>
        {
            internal readonly AutoReadHandler AutoReadHandler;
            internal IChannel Channel;

            internal AutoReadInitializer(bool readInEventLoop)
            {
                this.AutoReadHandler = new AutoReadHandler(readInEventLoop);
            }

            protected override void InitChannel(IChannel ch)
            {
                this.Channel = ch;
                ch.Pipeline.AddLast(this.AutoReadHandler);
            }
        }

        sealed class AutoReadHandler : ChannelHandlerAdapter
        {
            readonly CountdownEvent latch;
            readonly CountdownEvent latch2;
            readonly bool callRead;
            int count;

            internal AutoReadHandler(bool callRead)
            {
                this.callRead = callRead;
                this.latch = new CountdownEvent(1);
                this.latch2 = new CountdownEvent(callRead ? 3 : 2);
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                ReferenceCountUtil.Release(msg);
                if (Interlocked.Increment(ref this.count) == 1)
                {
                    ctx.Channel.Configuration.AutoRead = false;
                }
                if (this.callRead)
                {
                    ctx.Read();
                }
            }

            public override void ChannelReadComplete(IChannelHandlerContext context)
            {
                if (!this.latch.IsSet)
                {
                    this.latch.Signal();
                }
                this.latch2.Signal();
            }

            internal void AssertSingleRead()
            {
                Assert.True(this.latch.Wait(TimeSpan.FromSeconds(5)));
                Assert.True(this.count > 0);
            }

            internal void AssertSingleReadSecondTry()
            {
                Assert.True(this.latch2.Wait(TimeSpan.FromSeconds(5)), $"Expected count down remaining {this.latch2.CurrentCount}");
                Assert.Equal(this.callRead ? 3 : 2, this.count);
            }
        }

        sealed class TestRecvByteBufAllocator : IRecvByteBufAllocator
        {
            public IRecvByteBufAllocatorHandle NewHandle() => new Handle();

            sealed class Handle : IRecvByteBufAllocatorHandle
            {
                IChannelConfiguration config;

                public IByteBuffer Allocate(IByteBufferAllocator alloc) => alloc.Buffer(this.Guess(), this.Guess());

                // only ever allocate buffers of size 1 to ensure the number of reads is controlled.
                public int Guess() => 1; 

                public void Reset(IChannelConfiguration channelConfig)
                {
                    this.config = channelConfig;
                }

                public void IncMessagesRead(int numMessages)
                {
                    // No need to track the number of messages read because it is not used.
                }

                public int LastBytesRead { get; set; }

                public int AttemptedBytesRead { get; set; }

                public bool ContinueReading() => this.config.AutoRead;

                public void ReadComplete()
                {
                    // Nothing needs to be done or adjusted after each read cycle is completed.
                }
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
