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
    public sealed class ReadPendingTests : IDisposable
    {
        readonly IEventLoopGroup group;
        IChannel serverChannel;
        IChannel clientChannel;

        public ReadPendingTests()
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
            var serverInitializer = new ReadPendingInitializer();
            sb.Option(ChannelOption.SoBacklog, 1024)
              .Option(ChannelOption.AutoRead, true)
              .ChildOption(ChannelOption.AutoRead, false)
              // We intend to do 2 reads per read loop wakeup
              .ChildOption(ChannelOption.RcvbufAllocator, new TestNumReadsRecvByteBufAllocator(2))
              .ChildHandler(serverInitializer);

            // start server
            Task<IChannel> task = sb.BindAsync(LoopbackAnyPort);
            Assert.True(task.Wait(DefaultTimeout), "Server bind timed out");
            this.serverChannel = task.Result;
            Assert.NotNull(this.serverChannel.LocalAddress);
            var endPoint = (IPEndPoint)this.serverChannel.LocalAddress;

            var clientInitializer = new ReadPendingInitializer();
            cb.Option(ChannelOption.AutoRead, false)
               // We intend to do 2 reads per read loop wakeup
              .Option(ChannelOption.RcvbufAllocator, new TestNumReadsRecvByteBufAllocator(2))
              .Handler(clientInitializer);

            // connect to server
            task = cb.ConnectAsync(endPoint);
            Assert.True(task.Wait(DefaultTimeout), "Connect to server timed out");
            this.clientChannel = task.Result;
            Assert.NotNull(this.clientChannel.LocalAddress);

            // 4 bytes means 2 read loops for TestNumReadsRecvByteBufAllocator
            Task writeTask = this.clientChannel.WriteAndFlushAsync(Unpooled.WrappedBuffer(new byte[4]));
            Assert.True(writeTask.Wait(TimeSpan.FromSeconds(5)), "Client write task timed out");

            // 4 bytes means 2 read loops for TestNumReadsRecvByteBufAllocator
            Assert.True(serverInitializer.Initialize.Wait(DefaultTimeout), "Server initializer timed out");
            writeTask = serverInitializer.Channel.WriteAndFlushAsync(Unpooled.WrappedBuffer(new byte[4]));
            Assert.True(writeTask.Wait(TimeSpan.FromSeconds(5)), "Server write task timed out");

            serverInitializer.Channel.Read();
            serverInitializer.ReadPendingHandler.AssertAllRead();

            this.clientChannel.Read();
            clientInitializer.ReadPendingHandler.AssertAllRead();
        }

        sealed class ReadPendingInitializer : ChannelInitializer<IChannel>
        {
            internal readonly ReadPendingReadHandler ReadPendingHandler = new ReadPendingReadHandler();
            readonly TaskCompletionSource completionSource = new TaskCompletionSource();
            internal volatile IChannel Channel;

            public Task Initialize => this.completionSource.Task;

            protected override void InitChannel(IChannel ch)
            {
                this.Channel = ch;
                ch.Pipeline.AddLast(this.ReadPendingHandler);
                this.completionSource.TryComplete();
            }
        }

        sealed class ReadPendingReadHandler : ChannelHandlerAdapter
        {
            readonly CountdownEvent latch = new CountdownEvent(1);
            readonly CountdownEvent latch2 = new CountdownEvent(2);
            int count;

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                ReferenceCountUtil.Release(msg);
                if (Interlocked.Increment(ref this.count) == 1)
                {
                    // Call read the first time, to ensure it is not reset the second time.
                    ctx.Read();
                }
            }

            public override void ChannelReadComplete(IChannelHandlerContext ctx)
            {
                if (!this.latch.IsSet)
                {
                    this.latch.Signal();
                }
                this.latch2.Signal();
            }

            public void AssertAllRead()
            {
                Assert.True(this.latch.Wait(TimeSpan.FromSeconds(5)), "First ChannelReadComplete timed out");

                // We should only do 1 read loop, because we only called read() on the first channelRead.
                Assert.True(this.latch2.Wait(TimeSpan.FromSeconds(5)), "Second ChannelReadComplete timed out");
                Assert.Equal(2, this.count);
            }
        }

        sealed class TestNumReadsRecvByteBufAllocator : IRecvByteBufAllocator
        {
            readonly int numReads;

            public TestNumReadsRecvByteBufAllocator(int numReads)
            {
                this.numReads = numReads;
            }

            public IRecvByteBufAllocatorHandle NewHandle() => new AllocatorHandle(this.numReads);

            sealed class AllocatorHandle : IRecvByteBufAllocatorHandle
            {
                readonly int numReads;
                int numMessagesRead;

                public AllocatorHandle(int numReads)
                {
                    this.numReads = numReads;
                }

                public IByteBuffer Allocate(IByteBufferAllocator alloc) =>
                    alloc.Buffer(this.Guess(), this.Guess());

                // only ever allocate buffers of size 1 to ensure the number of reads is controlled.
                public int Guess() => 1; 

                public void Reset(IChannelConfiguration config)
                {
                    this.numMessagesRead = 0;
                }

                public void IncMessagesRead(int numMessages)
                {
                    this.numMessagesRead += numMessages;
                }

                public int LastBytesRead { get; set; }

                public int AttemptedBytesRead { get; set; }

                public bool ContinueReading()
                {
                    return this.numMessagesRead < this.numReads;
                }

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
