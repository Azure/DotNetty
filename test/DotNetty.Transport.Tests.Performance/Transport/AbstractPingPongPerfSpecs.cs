// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Performance.Transport
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using NBench;

    public abstract class AbstractPingPongPerfSpecs<TServer, TClient>
        where TServer : IServerChannel, new()
        where TClient : IChannel, new()
    {
        const string RoundTripCounterName = "Round trips";
        const int Duration = 5000; // 5 seconds

        IEventLoopGroup serverGroup;
        IEventLoopGroup workerGroup;
        IEventLoopGroup clientGroup;

        EchoClientHandler clientHandler;
        Counter roundTripCounter;
        IChannel client;

        [PerfSetup]
        public void SetUp(BenchmarkContext context)
        {
            this.serverGroup = this.NewServerGroup();
            this.workerGroup = this.NewWorkerGroup(this.serverGroup);
            this.clientGroup = this.NewClientGroup();

            this.roundTripCounter = context.GetCounter(RoundTripCounterName);
            var address = new IPEndPoint(IPAddress.IPv6Loopback, 0);

            // Start server
            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.serverGroup, this.workerGroup)
                .Channel<TServer>()
                .ChildHandler(new ActionChannelInitializer<TClient>(channel =>
                {
                    channel.Pipeline.AddLast(new EchoServerHandler());
                }));

            IChannel server = sb.BindAsync(address).Result;
            var endPoint = (IPEndPoint)server.LocalAddress;

            // Connect to server
            this.clientHandler = new EchoClientHandler(this.roundTripCounter, TimeSpan.FromMilliseconds(Duration));
            Bootstrap cb = new Bootstrap()
                .Group(this.clientGroup)
                .Channel<TClient>()
                .Handler(new ActionChannelInitializer<TClient>(channel =>
                {
                    channel.Pipeline.AddLast(this.clientHandler);
                }));
            this.client = cb.ConnectAsync(endPoint).Result;
        }

        protected abstract IEventLoopGroup NewServerGroup();

        protected abstract IEventLoopGroup NewWorkerGroup(IEventLoopGroup serverGroup);

        protected abstract IEventLoopGroup NewClientGroup();

        [PerfBenchmark(Description = "Measure number of round trips from client to server in 5 seconds.",
            NumberOfIterations = 1, RunMode = RunMode.Iterations)]
        [CounterMeasurement(RoundTripCounterName)]
        public void RoundTrip(BenchmarkContext context)
        {
            this.clientHandler.Start();
            this.client.WriteAndFlushAsync(Unpooled.WrappedBuffer(Encoding.ASCII.GetBytes("PING")));
            this.clientHandler.Completion.Wait(TimeSpan.FromSeconds(10));
        }

        sealed class EchoClientHandler : ChannelHandlerAdapter
        {
            readonly Counter counter;
            readonly TimeSpan duration;
            readonly Stopwatch stopwatch;
            readonly TaskCompletionSource completion;

            public EchoClientHandler(Counter counter, TimeSpan duration)
            {
                this.counter = counter;
                this.duration = duration;
                this.stopwatch = new Stopwatch();
                this.completion = new TaskCompletionSource();
            }

            public void Start() => this.stopwatch.Start();

            public Task Completion => this.completion.Task;

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                if (message is IByteBuffer buffer)
                {
                    this.counter.Increment();
                    if (this.stopwatch.Elapsed < this.duration)
                    {
                        context.WriteAndFlushAsync(buffer);
                    }
                    else
                    {
                        this.stopwatch.Stop();
                        this.completion.TryComplete();
                    }
                }
                else
                {
                    Console.WriteLine($"{nameof(EchoClientHandler)} Unexpected message {message}");
                }
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) =>
                Console.WriteLine($"{nameof(EchoServerHandler)} Error {exception}");
        }

        sealed class EchoServerHandler : ChannelHandlerAdapter
        {
            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                if (message is IByteBuffer buffer)
                {
                    context.WriteAndFlushAsync(buffer);
                }
                else
                {
                    Console.WriteLine($"{nameof(EchoServerHandler)} Unexpected message {message}");
                }
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) =>
                Console.WriteLine($"{nameof(EchoServerHandler)} Error {exception}");
        }

        [PerfCleanup]
        public void TearDown()
        {
            Task.WaitAll(
                this.clientGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
                this.serverGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
                this.workerGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)));
        }
    }
}
