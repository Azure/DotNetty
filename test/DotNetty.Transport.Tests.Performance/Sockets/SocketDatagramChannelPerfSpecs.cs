// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Performance.Sockets
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
    using DotNetty.Transport.Channels.Sockets;
    using NBench;

    public class SocketDatagramChannelPerfSpecs
    {
        const string SocketDatagramChannelReads = "SocketDatagramChannel reads";
        const string SocketDatagramChannelWrites = "SocketDatagramChannel writes";

        const int MessageSize = 64;
        const int MessageCount = 1000;
        const int DefaultTimeOutInMilliseconds = 1000;

        class InboundCounter : ChannelHandlerAdapter
        {
            readonly ManualResetEventSlim resetEvent;
            readonly int messageCount;
            readonly Counter reads;
            int count;

            public InboundCounter(int messageCount, Counter throughput)
            {
                this.resetEvent = new ManualResetEventSlim(false);
                this.messageCount = messageCount;
                this.reads = throughput;
                this.count = 0;
            }

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                try
                {
                    this.reads.Increment();
                    this.count++;
                }
                finally
                {
                    ReferenceCountUtil.Release(message);
                    if (this.count >= this.messageCount)
                    {
                        this.resetEvent.Set();
                    }
                }
            }

            public bool WaitForResult()
            {
                bool result = false;

                try
                {
                    if (this.resetEvent.Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds)))
                    {
                        result = this.count >= this.messageCount;
                    }
                }
                finally
                {
                    this.resetEvent.Reset();
                    this.count = 0;
                }

                return result;
            }
        }

        class OutboundCounter : ChannelHandlerAdapter
        {
            readonly Counter writes;

            public OutboundCounter(Counter writes)
            {
                this.writes = writes;
            }

            public override ChannelFuture WriteAsync(IChannelHandlerContext context, object message)
            {
                this.writes.Increment();
                return context.WriteAsync(message);
            } 
        }

        IEventLoopGroup serverGroup;
        Bootstrap serverBootstrap;
        IChannel serverChannel;

        IEventLoopGroup clientGroup;
        Bootstrap clientBootstrap;
        IChannel clientChannel;

        IByteBufferAllocator serverBufferAllocator;
        IByteBufferAllocator clientBufferAllocator;

        InboundCounter inboundCounter;
        OutboundCounter outboundCounter;
        Counter datagramChannelReads;
        Counter datagramChannelWrites;

        IPEndPoint serverEndPoint;
        byte[] message;

        static readonly IPAddress Localhost;

        static SocketDatagramChannelPerfSpecs()
        {
            Localhost = IPAddress.IPv6Loopback;
        }

        [PerfSetup]
        public void SetUp(BenchmarkContext context)
        {
            TaskScheduler.UnobservedTaskException += (sender, args) => Console.WriteLine(args.Exception);

            this.message = new byte[MessageSize];
            for (int i = 0; i < this.message.Length; i++)
            {
                this.message[i] = (byte)(i % 2);
            }

            this.datagramChannelReads = context.GetCounter(SocketDatagramChannelReads);

            this.inboundCounter = new InboundCounter(MessageCount, this.datagramChannelReads);
            this.serverBufferAllocator = new PooledByteBufferAllocator();
            this.serverGroup = new MultithreadEventLoopGroup(1);
            this.serverBootstrap = new Bootstrap();
            this.serverBootstrap
                .Group(this.serverGroup)
                .Channel<SocketDatagramChannel>()
                .Option(ChannelOption.Allocator, this.serverBufferAllocator)
                .Option(ChannelOption.SoBroadcast, true)
                .Option(ChannelOption.IpMulticastLoopDisabled, false)
                .Handler(new ActionChannelInitializer<IChannel>(channel =>
                {
                    channel.Pipeline.AddLast(this.inboundCounter);
                }));
            Task<IChannel> task = this.serverBootstrap.BindAsync(Localhost, IPEndPoint.MinPort);
            if (!task.Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds * 5)))
            {
                Console.WriteLine("Server start TIMED OUT");
            }

            this.serverChannel = (SocketDatagramChannel)task.Result;
            var endPoint = (IPEndPoint)this.serverChannel.LocalAddress;
            this.serverEndPoint = new IPEndPoint(Localhost, endPoint.Port);
            this.datagramChannelWrites = context.GetCounter(SocketDatagramChannelWrites);
            this.outboundCounter = new OutboundCounter(this.datagramChannelWrites);

            this.clientGroup = new MultithreadEventLoopGroup(1);
            this.clientBufferAllocator = new PooledByteBufferAllocator();
            this.clientBootstrap = new Bootstrap();
            this.clientBootstrap
                .Group(this.clientGroup)
                .Channel<SocketDatagramChannel>()
                .Option(ChannelOption.Allocator, this.clientBufferAllocator)
                .Option(ChannelOption.SoBroadcast, true)
                .Option(ChannelOption.IpMulticastLoopDisabled, false)
                .Handler(new ActionChannelInitializer<IChannel>(channel =>
                {
                    channel.Pipeline.AddLast(this.outboundCounter);
                }));

            this.clientBootstrap.RemoteAddress(this.serverEndPoint);
            task = (Task<IChannel>)this.clientBootstrap.RegisterAsync();
            if (!task.Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds * 5)))
            {
                Console.WriteLine("Register client channel TIMED OUT");
            }

            this.clientChannel = task.Result;
        }

        [PerfBenchmark(Description = "Measures average SocketDatagramChannel overhead of broadcasting 1000 64 byte messages from client to server.", 
            RunMode = RunMode.Iterations, NumberOfIterations = 20)]
        [CounterMeasurement(SocketDatagramChannelReads)]
        [CounterMeasurement(SocketDatagramChannelWrites)]
        [GcMeasurement(GcMetric.TotalCollections, GcGeneration.AllGc)]
        [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
        public void SocketDatagramChannelBroadcast(BenchmarkContext context)
        {
            this.clientChannel.EventLoop.Execute(() =>
            {
                for (int i = 0; i < MessageCount; i++)
                {
                    this.clientChannel.WriteAsync(
                        new DatagramPacket(Unpooled.WrappedBuffer(this.message), this.serverEndPoint));
                }
                this.clientChannel.Flush();
            });

            if (!this.inboundCounter.WaitForResult())
            {
                Console.WriteLine("*** TIMED OUT ***");
            }
        }

        [PerfCleanup]
        public void TearDown()
        {
            try
            {
                Task.WaitAll(
                    this.clientChannel.CloseAsync(),
                    this.serverChannel.CloseAsync());
            }
            finally
            {
                Task.WaitAll(
                    this.clientGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
                    this.serverGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)));
            }
        }
    }
}
