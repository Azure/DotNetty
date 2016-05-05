// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Performance.Sockets
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
    using DotNetty.Transport.Channels.Sockets;
    using DotNetty.Transport.Tests.Performance.Utilities;
    using NBench;

    public class TcpChannelPerfSpecs
    {
        const string InboundThroughputCounterName = "inbound ops";

        const string OutboundThroughputCounterName = "outbound ops";

        // The number of times we're going to warmup + run each benchmark
        public const int IterationCount = 5;
        public const int WriteCount = 1000000;

        public const int MessagesPerMinute = 1000000;
        public TimeSpan Timeout = TimeSpan.FromMinutes((double)WriteCount / MessagesPerMinute);

        static readonly IPEndPoint TEST_ADDRESS = new IPEndPoint(IPAddress.IPv6Loopback, 0);
        protected readonly ManualResetEventSlim ResetEvent = new ManualResetEventSlim(false);
        IChannel clientChannel;
        Counter inboundThroughputCounter;
        Counter outboundThroughputCounter;

        IChannel serverChannel;
        IReadFinishedSignal signal;

        protected Bootstrap ClientBootstrap;

        protected IEventLoopGroup ClientGroup;

        byte[] message;
        protected ServerBootstrap ServerBoostrap;
        protected IEventLoopGroup ServerGroup;
        protected IEventLoopGroup WorkerGroup;

        IByteBufferAllocator serverBufferAllocator;
        IByteBufferAllocator clientBufferAllocator;

        static TcpChannelPerfSpecs()
        {
            // Disable the logging factory
            //LoggingFactory.DefaultFactory = new NoOpLoggerFactory();
        }

        protected virtual IChannelHandler GetEncoder()
        {
            return new LengthFieldPrepender(4, false);
        }

        protected virtual IChannelHandler GetDecoder()
        {
            return new LengthFieldBasedFrameDecoder(int.MaxValue, 0, 4, 0, 4);
        }

        [PerfSetup]
        public void SetUp(BenchmarkContext context)
        {
            this.ClientGroup = new MultithreadEventLoopGroup(1);
            this.ServerGroup = new MultithreadEventLoopGroup(1);
            this.WorkerGroup = new MultithreadEventLoopGroup();

            Encoding iso = Encoding.GetEncoding("ISO-8859-1");
            this.message = iso.GetBytes("ABC");

            this.inboundThroughputCounter = context.GetCounter(InboundThroughputCounterName);
            this.outboundThroughputCounter = context.GetCounter(OutboundThroughputCounterName);
            var counterHandler = new CounterHandlerInbound(this.inboundThroughputCounter);
            this.signal = new ManualResetEventSlimReadFinishedSignal(this.ResetEvent);

            // reserve up to 10mb of 16kb buffers on both client and server; we're only sending about 700k worth of messages
            this.serverBufferAllocator = new PooledByteBufferAllocator(256, 10 * 1024 * 1024 / Environment.ProcessorCount);
            this.clientBufferAllocator = new PooledByteBufferAllocator(256, 10 * 1024 * 1024 / Environment.ProcessorCount);

            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.ServerGroup, this.WorkerGroup)
                .Channel<TcpServerSocketChannel>()
                .ChildOption(ChannelOption.Allocator, this.serverBufferAllocator)
                .ChildHandler(new ActionChannelInitializer<TcpSocketChannel>(channel =>
                {
                    channel.Pipeline
                        .AddLast(this.GetEncoder())
                        .AddLast(this.GetDecoder())
                        .AddLast(counterHandler)
                        .AddLast(new CounterHandlerOutbound(this.outboundThroughputCounter))
                        .AddLast(new ReadFinishedHandler(this.signal, WriteCount));
                }));

            Bootstrap cb = new Bootstrap()
                .Group(this.ClientGroup)
                .Channel<TcpSocketChannel>()
                .Option(ChannelOption.Allocator, this.clientBufferAllocator)
                .Handler(new ActionChannelInitializer<TcpSocketChannel>(
                    channel =>
                    {
                        channel.Pipeline
                            .AddLast(this.GetEncoder())
                            .AddLast(this.GetDecoder())
                            .AddLast(counterHandler)
                            .AddLast(new CounterHandlerOutbound(this.outboundThroughputCounter));
                    }));

            // start server
            this.serverChannel = sb.BindAsync(TEST_ADDRESS).Result;

            // connect to server
            this.clientChannel = cb.ConnectAsync(this.serverChannel.LocalAddress).Result;
        }

        [PerfBenchmark(Description = "Measures how quickly and with how much GC overhead a TcpSocketChannel --> TcpServerSocketChannel connection can decode / encode realistic messages, 100 writes per flush",
            NumberOfIterations = IterationCount, RunMode = RunMode.Iterations)]
        [CounterMeasurement(InboundThroughputCounterName)]
        [CounterMeasurement(OutboundThroughputCounterName)]
        [GcMeasurement(GcMetric.TotalCollections, GcGeneration.AllGc)]
        [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
        public void TcpChannel_Duplex_Throughput_100_messages_per_flush(BenchmarkContext context)
        {
            for (int i = 0; i < WriteCount; i++)
            {
                this.clientChannel.WriteAsync(Unpooled.WrappedBuffer(this.message));
                if (i % 100 == 0) // flush every 100 writes
                {
                    this.clientChannel.Flush();
                }
            }
            this.clientChannel.Flush();
            this.ResetEvent.Wait(this.Timeout);
        }

        [PerfCleanup]
        public void TearDown()
        {
            CloseChannel(this.clientChannel);
            CloseChannel(this.serverChannel);
            Task.WaitAll(this.ClientGroup.ShutdownGracefullyAsync(), this.ServerGroup.ShutdownGracefullyAsync(), this.WorkerGroup.ShutdownGracefullyAsync());
        }

        static void CloseChannel(IChannel cc)
        {
            cc?.CloseAsync().Wait();
        }
    }
}