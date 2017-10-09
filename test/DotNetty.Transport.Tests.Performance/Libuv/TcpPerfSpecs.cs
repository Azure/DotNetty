// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Performance.Libuv
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv;
    using DotNetty.Transport.Tests.Performance.Utilities;
    using NBench;

    public sealed class TcpPerfSpecs
    {
        const string InboundThroughputCounterName = "inbound ops";
        const string OutboundThroughputCounterName = "outbound ops";

        // The number of times we're going to warmup + run each benchmark
        public const int IterationCount = 3;
        public const int WriteCount = 1000000;

        public const int MessagesPerMinute = 1000000;
        public TimeSpan Timeout = TimeSpan.FromMinutes((double)WriteCount / MessagesPerMinute);

        static readonly IPEndPoint TestAddress = new IPEndPoint(IPAddress.IPv6Loopback, 0);
        readonly ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);

        IChannel clientChannel;
        Counter inboundThroughputCounter;
        Counter outboundThroughputCounter;

        IChannel serverChannel;
        IReadFinishedSignal signal;

        IEventLoopGroup clientGroup;

        byte[] message;
        IEventLoopGroup serverGroup;
        IEventLoopGroup workerGroup;

        IByteBufferAllocator serverBufferAllocator;
        IByteBufferAllocator clientBufferAllocator;

        static IChannelHandler GetEncoder() => new LengthFieldPrepender(4, false);

        static IChannelHandler GetDecoder() => new LengthFieldBasedFrameDecoder(int.MaxValue, 0, 4, 0, 4);

        [PerfSetup]
        public void SetUp(BenchmarkContext context)
        {
            TaskScheduler.UnobservedTaskException += (sender, args) => Console.WriteLine(args.Exception);

            var dispatcher = new DispatcherEventLoop();
            this.serverGroup = new MultithreadEventLoopGroup(_ => dispatcher, 1);
            this.workerGroup = new WorkerEventLoopGroup(dispatcher);
            this.clientGroup = new MultithreadEventLoopGroup(_ => new EventLoop(), 1);

            this.message = Encoding.UTF8.GetBytes("ABC");

            this.inboundThroughputCounter = context.GetCounter(InboundThroughputCounterName);
            this.outboundThroughputCounter = context.GetCounter(OutboundThroughputCounterName);
            var counterHandler = new CounterHandlerInbound(this.inboundThroughputCounter);
            this.signal = new ManualResetEventSlimReadFinishedSignal(this.resetEvent);

            // reserve up to 10mb of 16kb buffers on both client and server; we're only sending about 700k worth of messages
            this.serverBufferAllocator = new PooledByteBufferAllocator();
            this.clientBufferAllocator = new PooledByteBufferAllocator();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.serverGroup, this.workerGroup)
                .Channel<TcpServerChannel>()
                .ChildOption(ChannelOption.Allocator, this.serverBufferAllocator)
                .ChildHandler(new ActionChannelInitializer<TcpChannel>(channel =>
                {
                    channel.Pipeline
                        .AddLast(GetEncoder())
                        .AddLast(GetDecoder())
                        .AddLast(counterHandler)
                        .AddLast(new ReadFinishedHandler(this.signal, WriteCount));
                }));

            Bootstrap cb = new Bootstrap()
                .Group(this.clientGroup)
                .Channel<TcpChannel>()
                .Option(ChannelOption.Allocator, this.clientBufferAllocator)
                .Handler(new ActionChannelInitializer<TcpChannel>(
                    channel =>
                    {
                        channel.Pipeline
                            .AddLast(GetEncoder())
                            .AddLast(GetDecoder())
                            .AddLast(new CounterHandlerOutbound(this.outboundThroughputCounter));
                    }));

            // start server
            this.serverChannel = sb.BindAsync(TestAddress).Result;

            // connect to server
            this.clientChannel = cb.ConnectAsync(this.serverChannel.LocalAddress).Result;
        }

        [PerfBenchmark(Description = "Measures how quickly and with how much GC overhead a TcpChannel --> TcpServerChannel connection can decode / encode realistic messages, 10 writes per flush",
            NumberOfIterations = IterationCount, RunMode = RunMode.Iterations)]
        [CounterMeasurement(InboundThroughputCounterName)]
        [CounterMeasurement(OutboundThroughputCounterName)]
        [GcMeasurement(GcMetric.TotalCollections, GcGeneration.AllGc)]
        [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
        public void TcpDuplexThroughput10MessagesPerFlush(BenchmarkContext context)
        {
            this.clientChannel.EventLoop.Execute(() =>
            {
                for (int i = 0; i < WriteCount; i++)
                {
                    this.clientChannel.WriteAsync(Unpooled.WrappedBuffer(this.message));
                    if (i % 10 == 0) // flush every 10 writes
                    {
                        this.clientChannel.Flush();
                    }
                }
                this.clientChannel.Flush();
            });

            if (!this.resetEvent.Wait(this.Timeout))
            {
                Console.WriteLine("*** TIMED OUT ***");
            }
        }

        [PerfCleanup]
        public void TearDown()
        {
            CloseChannel(this.clientChannel);
            CloseChannel(this.serverChannel);
            Task.WaitAll(
                this.clientGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
                this.serverGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
                this.workerGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)));
        }

        static void CloseChannel(IChannel cc) => cc?.CloseAsync().Wait();
    }
}
