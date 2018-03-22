// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Performance.Transport
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using NBench;

    public abstract class AbstractPumpPerfSpecs<TServer, TClient>
        where TServer : IServerChannel, new()
        where TClient : IChannel, new()
    {
        const int Duration = 5000; // 5 seconds
        const int BufferSize = 8192;

        readonly Stopwatch stopwatch;

        IEventLoopGroup serverGroup;
        IEventLoopGroup workerGroup;

        BlackholeServerHandler serverHandler;
        NetworkStream stream;

        protected AbstractPumpPerfSpecs()
        {
            this.stopwatch = new Stopwatch();
        }

        [PerfSetup]
        public void SetUp(BenchmarkContext context)
        {
            this.serverGroup = this.NewServerGroup();
            this.workerGroup = this.NewWorkerGroup(this.serverGroup);

            var address = new IPEndPoint(IPAddress.IPv6Loopback, 0);

            // Start server
            this.serverHandler = new BlackholeServerHandler();
            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.serverGroup, this.workerGroup)
                .Channel<TServer>()
                .ChildHandler(new ActionChannelInitializer<TClient>(channel =>
                {
                    channel.Pipeline.AddLast(this.serverHandler);
                }));

            IChannel server = sb.BindAsync(address).Result;
            var endPoint = (IPEndPoint)server.LocalAddress;

            // Connect to server
            var clientSocket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            clientSocket.Connect(endPoint.Address, endPoint.Port);
            this.stream = new NetworkStream(clientSocket, true);
        }

        [PerfBenchmark(Description = "Measure number of 8192 byte writes from client to server in 5 seconds.",
            NumberOfIterations = 1, RunMode = RunMode.Iterations)]
        [GcMeasurement(GcMetric.TotalCollections, GcGeneration.AllGc)]
        [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
        public void Pump(BenchmarkContext context)
        {
            var bytes = new byte[BufferSize];
            TimeSpan duration = TimeSpan.FromMilliseconds(Duration);
            this.stopwatch.Start();
            while (this.stopwatch.Elapsed < duration)
            {
                this.stream.Write(bytes, 0, BufferSize);
            }
            this.stopwatch.Stop();
            long diff = this.stopwatch.ElapsedMilliseconds;
            double result = ToGigaBytes(this.serverHandler.TotalBytes, diff);
            
            Console.WriteLine($"{this.GetType().Name} throughput : {result:#,##0.00} gbit/s");
        }

        static double ToGigaBytes(long total, long interval)
        {
            double bits = total * 8;

            bits /= 1024;
            bits /= 1024;
            bits /= 1024;

            double duration = interval / 1000d;
            return bits / duration;
        }

        protected abstract IEventLoopGroup NewServerGroup();

        protected abstract IEventLoopGroup NewWorkerGroup(IEventLoopGroup serverGroup);

        sealed class BlackholeServerHandler : ChannelHandlerAdapter
        {
            long bytes;

            public long TotalBytes => this.bytes;

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                if (message is IByteBuffer buffer)
                {
                    Interlocked.Add(ref this.bytes, buffer.ReadableBytes);
                }
                else
                {
                    Console.WriteLine($"{nameof(BlackholeServerHandler)} Unexpected message {message}");
                }
                ReferenceCountUtil.Release(message);
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) =>
                Console.WriteLine($"{nameof(BlackholeServerHandler)} Error {exception}");
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
                Task.WaitAll(
                    this.serverGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
                    this.workerGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)));
            }
        }
    }
}
