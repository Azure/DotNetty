// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;

    public sealed class DispatcherEventLoopGroup : AbstractEventExecutorGroup, IEventLoopGroup
    {
        readonly DispatcherEventLoop dispatcherEventLoop;

        public DispatcherEventLoopGroup()
        {
            this.dispatcherEventLoop = new DispatcherEventLoop(this);
        }

        public override bool IsShutdown => this.dispatcherEventLoop.IsShutdown;

        public override bool IsTerminated => this.dispatcherEventLoop.IsTerminated;

        public override bool IsShuttingDown => this.dispatcherEventLoop.IsShuttingDown;

        public override Task TerminationCompletion => this.dispatcherEventLoop.TerminationCompletion;

        internal DispatcherEventLoop Dispatcher => this.dispatcherEventLoop;

        IEventLoop IEventLoopGroup.GetNext() => (IEventLoop)this.GetNext();

        public override IEventExecutor GetNext() => this.dispatcherEventLoop;

        public Task RegisterAsync(IChannel channel) => ((IEventLoop)this.GetNext()).RegisterAsync(channel);

        public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        {
            this.dispatcherEventLoop.ShutdownGracefullyAsync(quietPeriod, timeout);
            return this.TerminationCompletion;
        }
    }
}