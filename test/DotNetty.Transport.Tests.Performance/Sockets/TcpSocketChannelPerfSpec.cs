// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Performance.Sockets
{
    using System;
    using System.IO;
    using System.Net;
    using System.Reflection;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Handlers.Tls;
    using DotNetty.Tests.Common;
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
        public const int IterationCount = 3;
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

        protected virtual IChannelHandler GetEncoder() => new LengthFieldPrepender(4, false);

        protected virtual IChannelHandler GetDecoder() => new LengthFieldBasedFrameDecoder(int.MaxValue, 0, 4, 0, 4);

        [PerfSetup]
        public void SetUp(BenchmarkContext context)
        {
            TaskScheduler.UnobservedTaskException += (sender, args) => Console.WriteLine(args.Exception);

            this.ClientGroup = new MultithreadEventLoopGroup(1);
            this.ServerGroup = new MultithreadEventLoopGroup(1);
            this.WorkerGroup = new MultithreadEventLoopGroup();

            this.message = Encoding.UTF8.GetBytes("ABC");

            this.inboundThroughputCounter = context.GetCounter(InboundThroughputCounterName);
            this.outboundThroughputCounter = context.GetCounter(OutboundThroughputCounterName);
            var counterHandler = new CounterHandlerInbound(this.inboundThroughputCounter);
            this.signal = new ManualResetEventSlimReadFinishedSignal(this.ResetEvent);

            // reserve up to 10mb of 16kb buffers on both client and server; we're only sending about 700k worth of messages
            this.serverBufferAllocator = new PooledByteBufferAllocator();
            this.clientBufferAllocator = new PooledByteBufferAllocator();

            Assembly assembly = typeof(TcpChannelPerfSpecs).Assembly;
            var tlsCertificate = TestResourceHelper.GetTestCertificate();
            string targetHost = tlsCertificate.GetNameInfo(X509NameType.DnsName, false);

            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.ServerGroup, this.WorkerGroup)
                .Channel<TcpServerSocketChannel>()
                .ChildOption(ChannelOption.Allocator, this.serverBufferAllocator)
                .ChildHandler(new ActionChannelInitializer<TcpSocketChannel>(channel =>
                {
                    channel.Pipeline
                        //.AddLast(TlsHandler.Server(tlsCertificate))
                        .AddLast(this.GetEncoder())
                        .AddLast(this.GetDecoder())
                        .AddLast(counterHandler)
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
                            //.AddLast(TlsHandler.Client(targetHost, null, (sender, certificate, chain, errors) => true))
                            .AddLast(this.GetEncoder())
                            .AddLast(this.GetDecoder())
                            .AddLast(new CounterHandlerOutbound(this.outboundThroughputCounter));
                    }));

            // start server
            this.serverChannel = sb.BindAsync(TEST_ADDRESS).Result;

            // connect to server
            this.clientChannel = cb.ConnectAsync(this.serverChannel.LocalAddress).Result;
        }

        [PerfBenchmark(Description = "Measures how quickly and with how much GC overhead a TcpSocketChannel --> TcpServerSocketChannel connection can decode / encode realistic messages, 10 writes per flush",
            NumberOfIterations = IterationCount, RunMode = RunMode.Iterations)]
        [CounterMeasurement(InboundThroughputCounterName)]
        [CounterMeasurement(OutboundThroughputCounterName)]
        [GcMeasurement(GcMetric.TotalCollections, GcGeneration.AllGc)]
        [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
        public void TcpChannel_Duplex_Throughput_10_messages_per_flush(BenchmarkContext context)
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

            if (!this.ResetEvent.Wait(this.Timeout))
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
                this.ClientGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
                this.ServerGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
                this.WorkerGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)));
        }

        static void CloseChannel(IChannel cc) => cc?.CloseAsync().Wait();
    }
}