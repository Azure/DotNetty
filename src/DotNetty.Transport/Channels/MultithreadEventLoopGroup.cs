// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    /// <summary>
    /// <see cref="IEventLoopGroup"/> backed by a set of <see cref="SingleThreadEventLoop"/> instances.
    /// </summary>
    public sealed class MultithreadEventLoopGroup : AbstractEventExecutorGroup, IEventLoopGroup
    {
        static readonly int DefaultEventLoopThreadCount = Environment.ProcessorCount * 2;
        static readonly Func<IEventLoopGroup, IEventLoop> DefaultEventLoopFactory = group => new SingleThreadEventLoop(group);

        readonly IEventLoop[] eventLoops;
        int requestId;

        public override bool IsShutdown => eventLoops.All(eventLoop => eventLoop.IsShutdown);

        public override bool IsTerminated => eventLoops.All(eventLoop => eventLoop.IsTerminated);

        public override bool IsShuttingDown => eventLoops.All(eventLoop => eventLoop.IsShuttingDown);

        /// <inheritdoc />
        public override Task TerminationCompletion { get; }

        /// <inheritdoc />
        protected override IEnumerable<IEventExecutor> GetItems() => this.eventLoops;

        /// <inheritdoc />
        public new IEnumerable<IEventLoop> Items => this.eventLoops;

        /// <summary>Creates a new instance of <see cref="MultithreadEventLoopGroup"/>.</summary>
        public MultithreadEventLoopGroup()
            : this(DefaultEventLoopFactory, DefaultEventLoopThreadCount)
        {
        }

        /// <summary>Creates a new instance of <see cref="MultithreadEventLoopGroup"/>.</summary>
        public MultithreadEventLoopGroup(int eventLoopCount)
            : this(DefaultEventLoopFactory, eventLoopCount)
        {
        }

        /// <summary>Creates a new instance of <see cref="MultithreadEventLoopGroup"/>.</summary>
        public MultithreadEventLoopGroup(Func<IEventLoopGroup, IEventLoop> eventLoopFactory)
            : this(eventLoopFactory, DefaultEventLoopThreadCount)
        {
        }

        /// <summary>Creates a new instance of <see cref="MultithreadEventLoopGroup"/>.</summary>
        public MultithreadEventLoopGroup(Func<IEventLoopGroup, IEventLoop> eventLoopFactory, int eventLoopCount)
        {
            this.eventLoops = new IEventLoop[eventLoopCount];
            var terminationTasks = new Task[eventLoopCount];
            for (int i = 0; i < eventLoopCount; i++)
            {
                IEventLoop eventLoop;
                bool success = false;
                try
                {
                    eventLoop = eventLoopFactory(this);
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
                        Task.WhenAll(
                                this.eventLoops
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

        /// <inheritdoc />
        IEventLoop IEventLoopGroup.GetNext() => (IEventLoop)this.GetNext();

        /// <inheritdoc />
        public override IEventExecutor GetNext()
        {
            int id = Interlocked.Increment(ref this.requestId);
            return this.eventLoops[Math.Abs(id % this.eventLoops.Length)];
        }

        public Task RegisterAsync(IChannel channel) => ((IEventLoop)this.GetNext()).RegisterAsync(channel);

        /// <inheritdoc cref="IEventExecutorGroup.ShutdownGracefullyAsync()" />
        public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        {
            foreach (IEventLoop eventLoop in this.eventLoops)
            {
                eventLoop.ShutdownGracefullyAsync(quietPeriod, timeout);
            }
            return this.TerminationCompletion;
        }
    }
}