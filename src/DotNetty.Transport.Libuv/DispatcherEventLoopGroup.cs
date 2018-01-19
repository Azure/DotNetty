// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;

    public sealed class DispatcherEventLoopGroup : IEventLoopGroup
    {
        readonly DispatcherEventLoop dispatcherEventLoop;

        public DispatcherEventLoopGroup()
        {
            this.dispatcherEventLoop = new DispatcherEventLoop(this);
        }

        public Task TerminationCompletion => this.dispatcherEventLoop.TerminationCompletion;

        internal DispatcherEventLoop Dispatcher => this.dispatcherEventLoop;

        IEventExecutor IEventExecutorGroup.GetNext() => this.GetNext();

        public Task RegisterAsync(IChannel channel) => this.GetNext().RegisterAsync(channel);

        public IEventLoop GetNext() => this.dispatcherEventLoop;

        public Task ShutdownGracefullyAsync()
        {
            this.dispatcherEventLoop.ShutdownGracefullyAsync();
            return this.TerminationCompletion;
        }

        public Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        {
            this.dispatcherEventLoop.ShutdownGracefullyAsync(quietPeriod, timeout);
            return this.TerminationCompletion;
        }
    }
}
