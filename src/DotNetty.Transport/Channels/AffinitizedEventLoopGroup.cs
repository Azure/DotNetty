// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    /// <summary>
    /// <see cref="IEventLoopGroup"/> that works as a wrapper for another <see cref="IEventLoopGroup"/> providing affinity on <see cref="GetNext"/> call.
    /// </summary>
    public class AffinitizedEventLoopGroup : AbstractEventExecutorGroup, IEventLoopGroup
    {
        readonly IEventLoopGroup innerGroup;

        public override bool IsShutdown => this.innerGroup.IsShutdown;

        public override bool IsTerminated => this.innerGroup.IsTerminated;

        public override bool IsShuttingDown => this.innerGroup.IsShuttingDown;

        /// <inheritdoc cref="IEventExecutorGroup"/>
        public override Task TerminationCompletion => this.innerGroup.TerminationCompletion;

        protected override IEnumerable<IEventExecutor> GetItems() => this.innerGroup.Items;

        public new IEnumerable<IEventLoop> Items => ((IEventLoopGroup)this.innerGroup).Items;

        /// <summary>
        /// Creates a new instance of <see cref="AffinitizedEventLoopGroup"/>.
        /// </summary>
        /// <param name="innerGroup"><see cref="IEventLoopGroup"/> serving as an actual provider of <see cref="IEventLoop"/>s.</param>
        public AffinitizedEventLoopGroup(IEventLoopGroup innerGroup)
        {
            this.innerGroup = innerGroup;
        }

        /// <summary>
        /// If running in a context of an existing <see cref="IEventLoop"/>, this <see cref="IEventLoop"/> is returned.
        /// Otherwise, <see cref="IEventLoop"/> is retrieved from underlying <see cref="IEventLoopGroup"/>.
        /// </summary>
        public override IEventExecutor GetNext()
        {
            if (ExecutionEnvironment.TryGetCurrentExecutor(out var executor))
            {
                if (executor is IEventLoop loop && loop.Parent == this.innerGroup)
                {
                    return loop;
                }
            }
            return this.innerGroup.GetNext();
        }

        IEventLoop IEventLoopGroup.GetNext() => (IEventLoop)this.GetNext();

        public Task RegisterAsync(IChannel channel) => ((IEventLoop)this.GetNext()).RegisterAsync(channel);

        /// <inheritdoc cref="IEventExecutorGroup"/>
        public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout) => this.innerGroup.ShutdownGracefullyAsync(quietPeriod, timeout);
    }
}