// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Performance.Sockets
{
    using System;
    using System.Net;
    using System.Net.Sockets;
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

    public class TcpSocketChannelInboundOnlyPerfSpecs
    {
        const string InboundThroughputCounterName = "inbound ops";

        // The number of times we're going to warmup + run each benchmark
        public const int IterationCount = 3;
        public const int WriteCount = 1000000;

        public const int MessagesPerMinute = 1000000;
        public TimeSpan Timeout = TimeSpan.FromMinutes((double)WriteCount / MessagesPerMinute);

        static readonly IPEndPoint TEST_ADDRESS = new IPEndPoint(IPAddress.IPv6Loopback, 0);
        protected readonly ManualResetEventSlim ResetEvent = new ManualResetEventSlim(false);
        Counter inboundThroughputCounter;

        IChannel serverChannel;
        IReadFinishedSignal signal;

        protected Socket ClientSocket;
        protected NetworkStream Stream;

        byte[] message;
        protected ServerBootstrap ServerBoostrap;
        protected IEventLoopGroup ServerGroup;
        protected IEventLoopGroup WorkerGroup;

        IByteBufferAllocator serverBufferAllocator;

        static TcpSocketChannelInboundOnlyPerfSpecs()
        {
            // Disable the logging factory
            //LoggingFactory.DefaultFactory = new NoOpLoggerFactory();
        }

        protected virtual IChannelHandler GetEncoder() => new LengthFieldPrepender(4, false);

        protected virtual IChannelHandler GetDecoder() => new LengthFieldBasedFrameDecoder(int.MaxValue, 0, 4, 0, 4);

        [PerfSetup]
        public void SetUp(BenchmarkContext context)
        {
            this.ServerGroup = new MultithreadEventLoopGroup(1);
            this.WorkerGroup = new MultithreadEventLoopGroup();

            Encoding iso = Encoding.GetEncoding("ISO-8859-1");
            IByteBuffer buf = Unpooled.Buffer().WriteInt(3).WriteBytes(iso.GetBytes("ABC"));
            this.message = new byte[buf.ReadableBytes];
            buf.GetBytes(buf.ReaderIndex, this.message);

            this.inboundThroughputCounter = context.GetCounter(InboundThroughputCounterName);
            var counterHandler = new CounterHandlerInbound(this.inboundThroughputCounter);
            this.signal = new ManualResetEventSlimReadFinishedSignal(this.ResetEvent);

            // using default settings
            this.serverBufferAllocator = new PooledByteBufferAllocator();

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
                        .AddLast(new ReadFinishedHandler(this.signal, WriteCount));
                }));

            // start server
            this.serverChannel = sb.BindAsync(TEST_ADDRESS).Result;

            // connect to server
            var address = (IPEndPoint)this.serverChannel.LocalAddress;
            this.ClientSocket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            this.ClientSocket.Connect(address.Address, address.Port);

            this.Stream = new NetworkStream(this.ClientSocket, true);
        }

        [PerfBenchmark(Description = "Measures how quickly and with how much GC overhead a TcpSocketChannel --> TcpServerSocketChannel connection can decode / encode realistic messages, 100 writes per flush",
            NumberOfIterations = IterationCount, RunMode = RunMode.Iterations)]
        [CounterMeasurement(InboundThroughputCounterName)]
        [GcMeasurement(GcMetric.TotalCollections, GcGeneration.AllGc)]
        [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
        public void TcpChannel_InboundOnly_Throughput(BenchmarkContext context)
        {
            for (int i = 0; i < WriteCount; i++)
            {
                this.Stream.Write(this.message, 0, this.message.Length);
            }
            this.ResetEvent.Wait(this.Timeout);
        }

        [PerfCleanup]
        public void TearDown()
        {
            try
            {
                this.Stream.Close();
            }
            finally
            {
                CloseChannel(this.serverChannel);
                Task.WaitAll(
                    this.ServerGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
                    this.WorkerGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)));
            }
        }

        static void CloseChannel(IChannel cc) => cc?.CloseAsync().Wait();
    }
}