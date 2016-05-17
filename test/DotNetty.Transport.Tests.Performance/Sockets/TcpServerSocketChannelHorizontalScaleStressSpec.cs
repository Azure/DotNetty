namespace DotNetty.Transport.Tests.Performance.Sockets
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Codecs;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using DotNetty.Transport.Tests.Performance.Utilities;
    using NBench;

    public class TcpServerSocketChannelHorizontalScaleSpec
    {
        const string ClientConnectCounterName = "connected clients";

        const string InboundThroughputCounterName = "inbound ops";

        const string OutboundThroughputCounterName = "outbound ops";

        const string ErrorCounterName = "exceptions caught";

        public const int IterationCount = 1; // these are LONG-running benchmarks. stick with a lower iteration count
        static readonly IPEndPoint TEST_ADDRESS = new IPEndPoint(IPAddress.IPv6Loopback, 0);

        // Sleep main thread, then start a new client every 30 ms
        static readonly TimeSpan SleepInterval = TimeSpan.FromMilliseconds(300);

        /// <summary>
        ///     If it takes longer than <see cref="SaturationThreshold" /> to establish 10 connections, we're saturated.
        ///     End the stress test.
        /// </summary>
        static readonly TimeSpan SaturationThreshold = TimeSpan.FromSeconds(15);

        protected readonly ManualResetEventSlim ResetEvent = new ManualResetEventSlim(false);
        ConcurrentBag<IChannel> clientChannels;
        Counter clientConnectedCounter;
        Counter errorCounter;

        Action eventLoop;
        Counter inboundThroughputCounter;
        Counter outboundThroughputCounter;

        IChannel serverChannel;
        CancellationTokenSource shutdownBenchmark;

        IReadFinishedSignal signal;

        protected Bootstrap ClientBootstrap;

        protected IEventLoopGroup ClientGroup;
        protected ServerBootstrap ServerBoostrap;
        protected IEventLoopGroup ServerGroup;
        protected IEventLoopGroup WorkerGroup;

        protected virtual IChannelHandler GetEncoder() => new LengthFieldPrepender(4, false);

        protected virtual IChannelHandler GetDecoder() => new LengthFieldBasedFrameDecoder(int.MaxValue, 0, 4, 0, 4);

        static readonly ThreadLocal<Random> ThreadLocalRandom = new ThreadLocal<Random>(() => new Random());

        [PerfSetup]
        public void SetUp(BenchmarkContext context)
        {
            this.ClientGroup = new MultithreadEventLoopGroup(Environment.ProcessorCount / 2);
            this.ServerGroup = new MultithreadEventLoopGroup(1);
            this.WorkerGroup = new MultithreadEventLoopGroup(Environment.ProcessorCount / 2);

            this.shutdownBenchmark = new CancellationTokenSource();
            this.clientChannels = new ConcurrentBag<IChannel>();

            this.inboundThroughputCounter = context.GetCounter(InboundThroughputCounterName);
            this.outboundThroughputCounter = context.GetCounter(OutboundThroughputCounterName);
            this.clientConnectedCounter = context.GetCounter(ClientConnectCounterName);
            this.errorCounter = context.GetCounter(ErrorCounterName);

            this.signal = new ManualResetEventSlimReadFinishedSignal(this.ResetEvent);

            ServerBootstrap sb = new ServerBootstrap().Group(this.ServerGroup, this.WorkerGroup).Channel<TcpServerSocketChannel>()
                .ChildOption(ChannelOption.TcpNodelay, true)
                .ChildHandler(new ActionChannelInitializer<TcpSocketChannel>(channel =>
                {
                    channel.Pipeline.AddLast(this.GetEncoder())
                        .AddLast(this.GetDecoder())
                        .AddLast(new IntCodec(true))
                        .AddLast(new CounterHandlerInbound(this.inboundThroughputCounter))
                        .AddLast(new CounterHandlerOutbound(this.outboundThroughputCounter))
                        .AddLast(new ErrorCounterHandler(this.errorCounter));
                }));

            this.ClientBootstrap = new Bootstrap().Group(this.ClientGroup)
                .Option(ChannelOption.TcpNodelay, true)
                .Channel<TcpSocketChannel>().Handler(new ActionChannelInitializer<TcpSocketChannel>(
                    channel =>
                    {
                        channel.Pipeline.AddLast(this.GetEncoder())
                            .AddLast(this.GetDecoder())
                            .AddLast(new IntCodec(true))
                            .AddLast(new CounterHandlerInbound(this.inboundThroughputCounter))
                            .AddLast(new CounterHandlerOutbound(this.outboundThroughputCounter))
                            .AddLast(new ErrorCounterHandler(this.errorCounter));
                    }));

            CancellationToken token = this.shutdownBenchmark.Token;
            this.eventLoop = () =>
            {
                while (!token.IsCancellationRequested)
                {
                    foreach (IChannel channel in this.clientChannels)
                    {
                        // unrolling a loop
                        channel.WriteAsync(ThreadLocalRandom.Value.Next());
                        channel.WriteAsync(ThreadLocalRandom.Value.Next());
                        channel.WriteAsync(ThreadLocalRandom.Value.Next());
                        channel.WriteAsync(ThreadLocalRandom.Value.Next());
                        channel.WriteAsync(ThreadLocalRandom.Value.Next());
                        channel.WriteAsync(ThreadLocalRandom.Value.Next());
                        channel.WriteAsync(ThreadLocalRandom.Value.Next());
                        channel.WriteAsync(ThreadLocalRandom.Value.Next());
                        channel.WriteAsync(ThreadLocalRandom.Value.Next());
                        channel.Flush();
                    }

                    // sleep for a tiny bit, then get going again
                    Thread.Sleep(40);
                }
            };

            // start server
            this.serverChannel = sb.BindAsync(TEST_ADDRESS).Result;

            // connect to server with 1 client initially
            this.clientChannels.Add(this.ClientBootstrap.ConnectAsync(this.serverChannel.LocalAddress).Result);
        }

        [PerfBenchmark(Description = "Measures how quickly and with how much GC overhead a TcpSocketChannel --> TcpServerSocketChannel connection can decode / encode realistic messages",
            NumberOfIterations = IterationCount, RunMode = RunMode.Iterations)]
        [CounterMeasurement(InboundThroughputCounterName)]
        [CounterMeasurement(OutboundThroughputCounterName)]
        [CounterMeasurement(ClientConnectCounterName)]
        [CounterMeasurement(ErrorCounterName)]
        [GcMeasurement(GcMetric.TotalCollections, GcGeneration.AllGc)]
        [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
        public void TcpServerSocketChannel_horizontal_scale_stress_test(BenchmarkContext context)
        {
            this.clientConnectedCounter.Increment(); // for the initial client
            TimeSpan totalRunSeconds = TimeSpan.FromSeconds(ThreadLocalRandom.Value.Next(180, 360)); // 3-6 minutes
            Console.WriteLine("Running benchmark for {0} minutes", totalRunSeconds.TotalMinutes);
            DateTime due = DateTime.Now + totalRunSeconds;
            DateTime lastMeasure = due;
            Task task = Task.Factory.StartNew(this.eventLoop); // start writing
            int runCount = 1;
            while (DateTime.Now < due)
            {
                // add a new client
                this.clientChannels.Add(this.ClientBootstrap.ConnectAsync(this.serverChannel.LocalAddress).Result);
                this.clientConnectedCounter.Increment();
                Thread.Sleep(SleepInterval);
                if (++runCount % 10 == 0)
                {
                    Console.WriteLine("{0} minutes remaining [{1} connections active].", (due - DateTime.Now).TotalMinutes, runCount);
                    TimeSpan saturation = DateTime.Now - lastMeasure;
                    if (saturation > SaturationThreshold)
                    {
                        Console.WriteLine("Took {0} to create 10 connections; exceeded pre-defined saturation threshold of {1}. Ending stress test.", saturation, SaturationThreshold);
                        break;
                    }
                    lastMeasure = DateTime.Now;
                }
            }
            this.shutdownBenchmark.Cancel();
        }

        [PerfCleanup]
        public void TearDown()
        {
            this.eventLoop = null;
            var shutdownTasks = new List<Task>();
            foreach (IChannel channel in this.clientChannels)
            {
                shutdownTasks.Add(channel.CloseAsync());
            }
            Task.WaitAll(shutdownTasks.ToArray());
            CloseChannel(this.serverChannel);
            Task.WaitAll(this.ClientGroup.ShutdownGracefullyAsync(), this.ServerGroup.ShutdownGracefullyAsync(), this.WorkerGroup.ShutdownGracefullyAsync());
        }

        static void CloseChannel(IChannel cc)
        {
            cc?.CloseAsync().Wait();
        }
    }
}