﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ForCanBeConvertedToForeach
namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    public sealed class EventLoopGroup : IEventLoopGroup
    {
        static readonly int DefaultEventLoopCount = Environment.ProcessorCount;
        readonly EventLoop[] eventLoops;
        int requestId;

        public EventLoopGroup() : this(DefaultEventLoopCount)
        {
        }

        public EventLoopGroup(int eventLoopCount)
        {
            this.eventLoops = new EventLoop[eventLoopCount];
            var terminationTasks = new Task[eventLoopCount];
            for (int i = 0; i < eventLoopCount; i++)
            {
                EventLoop eventLoop;
                bool success = false;
                try
                {
                    eventLoop = new EventLoop(this, $"{nameof(EventLoopGroup)}-{i}");
                    success = true;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("failed to create a child event loop.", ex);
                }
                finally
                {
                    if (!success)
                    {
                        Task.WhenAll(this.eventLoops
                                .Take(i)
                                .Select(loop => loop.ShutdownGracefullyAsync()))
                            .Wait();
                    }
                }

                this.eventLoops[i] = eventLoop;
                terminationTasks[i] = eventLoop.TerminationCompletion;
            }
            this.TerminationCompletion = Task.WhenAll(terminationTasks);
        }

        public Task TerminationCompletion { get; }

        IEventExecutor IEventExecutorGroup.GetNext() => this.GetNext();

        public IEventLoop GetNext()
        {
            // Attempt to select event loop based on thread first
            int threadId = XThread.CurrentThread.Id;
            int i;
            for (i = 0; i < this.eventLoops.Length; i++)
            {
                if (this.eventLoops[i].LoopThreadId == threadId)
                {
                    return this.eventLoops[i];
                }
            }

            // Default select, this means libuv handles not created yet,
            // the chosen loop will be used to create handles from.
            i = Interlocked.Increment(ref this.requestId);
            return this.eventLoops[Math.Abs(i % this.eventLoops.Length)];
        }

        public Task RegisterAsync(IChannel channel)
        {
            if (!(channel is NativeChannel nativeChannel))
            {
                throw new ArgumentException($"{nameof(channel)} must be of {typeof(NativeChannel)}");
            }

            // The handle loop must be the same as the loop of the
            // handle was created from.
            NativeHandle handle = nativeChannel.GetHandle();
            IntPtr loopHandle = handle.LoopHandle();
            for (int i = 0; i < this.eventLoops.Length; i++)
            {
                if (this.eventLoops[i].UnsafeLoop.Handle == loopHandle)
                {
                    return this.eventLoops[i].RegisterAsync(nativeChannel);
                }
            }

            throw new InvalidOperationException($"Loop {loopHandle} does not exist");
        }

        public Task ShutdownGracefullyAsync()
        {
            foreach (EventLoop eventLoop in this.eventLoops)
            {
                eventLoop.ShutdownGracefullyAsync();
            }
            return this.TerminationCompletion;
        }

        public Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        {
            foreach (EventLoop eventLoop in this.eventLoops)
            {
                eventLoop.ShutdownGracefullyAsync(quietPeriod, timeout);
            }
            return this.TerminationCompletion;
        }
    }
}
