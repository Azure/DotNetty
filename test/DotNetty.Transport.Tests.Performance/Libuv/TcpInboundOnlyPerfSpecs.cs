// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Performance.Libuv
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
    using DotNetty.Transport.Libuv;
    using DotNetty.Transport.Tests.Performance.Utilities;
    using NBench;

    public sealed class TcpInboundOnlyPerfSpecs
    {
        const string InboundThroughputCounterName = "inbound ops";

        // The number of times we're going to warmup + run each benchmark
        public const int IterationCount = 3;
        public const int WriteCount = 1000000;

        public const int MessagesPerMinute = 1000000;
        public TimeSpan Timeout = TimeSpan.FromMinutes((double)WriteCount / MessagesPerMinute);

        static readonly IPEndPoint TestAddress = new IPEndPoint(IPAddress.IPv6Loopback, 0);

        readonly ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);
        Counter inboundThroughputCounter;

        IChannel serverChannel;
        IReadFinishedSignal signal;

        Socket clientSocket;
        NetworkStream stream;

        byte[] message;
        IEventLoopGroup serverGroup;

        IByteBufferAllocator serverBufferAllocator;

        static IChannelHandler GetEncoder() => new LengthFieldPrepender(4, false);

        static IChannelHandler GetDecoder() => new LengthFieldBasedFrameDecoder(int.MaxValue, 0, 4, 0, 4);

        [PerfSetup]
        public void SetUp(BenchmarkContext context)
        {
            this.serverGroup = new MultithreadEventLoopGroup(_ => new EventLoop(), 1);

            Encoding iso = Encoding.GetEncoding("ISO-8859-1");
            IByteBuffer buf = Unpooled.Buffer().WriteInt(3).WriteBytes(iso.GetBytes("ABC"));
            this.message = new byte[buf.ReadableBytes];
            buf.GetBytes(buf.ReaderIndex, this.message);

            this.inboundThroughputCounter = context.GetCounter(InboundThroughputCounterName);
            var counterHandler = new CounterHandlerInbound(this.inboundThroughputCounter);
            this.signal = new ManualResetEventSlimReadFinishedSignal(this.resetEvent);

            // using default settings
            this.serverBufferAllocator = new PooledByteBufferAllocator();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.serverGroup)
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

            // start server
            this.serverChannel = sb.BindAsync(TestAddress).Result;

            // connect to server
            var address = (IPEndPoint)this.serverChannel.LocalAddress;
            this.clientSocket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            this.clientSocket.Connect(address.Address, address.Port);

            this.stream = new NetworkStream(this.clientSocket, true);
        }

        [PerfBenchmark(Description = "Measures how quickly and with how much GC overhead a TcpChannel --> TcpServerChannel connection can decode / encode realistic messages, 100 writes per flush",
            NumberOfIterations = IterationCount, RunMode = RunMode.Iterations)]
        [CounterMeasurement(InboundThroughputCounterName)]
        [GcMeasurement(GcMetric.TotalCollections, GcGeneration.AllGc)]
        [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
        public void TcpInboundOnlyThroughput(BenchmarkContext context)
        {
            for (int i = 0; i < WriteCount; i++)
            {
                this.stream.Write(this.message, 0, this.message.Length);
            }
            this.resetEvent.Wait(this.Timeout);
        }

        [PerfCleanup]
        public void TearDown()
        {
            try
            {
                this.stream.Close();
            }
            finally
            {
                CloseChannel(this.serverChannel);
                Task.WaitAll(this.serverGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)));
            }
        }

        static void CloseChannel(IChannel cc) => cc?.CloseAsync().Wait();
    }
}
