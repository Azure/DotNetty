// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    /// <summary>
    /// <see cref="IEventLoopGroup"/> that works as a wrapper for another <see cref="IEventLoopGroup"/> providing affinity on <see cref="GetNext"/> call.
    /// </summary>
    public class AffinitizedEventLoopGroup : IEventLoopGroup
    {
        readonly IEventLoopGroup innerGroup;

        /// <summary>
        /// Creates a new instance of <see cref="AffinitizedEventLoopGroup"/>.
        /// </summary>
        /// <param name="innerGroup"><see cref="IEventLoopGroup"/> serving as an actual provider of <see cref="IEventLoop"/>s.</param>
        public AffinitizedEventLoopGroup(IEventLoopGroup innerGroup)
        {
            this.innerGroup = innerGroup;
        }

        /// <inheritdoc cref="IEventLoopGroup"/>
        public Task TerminationCompletion => this.innerGroup.TerminationCompletion;

        IEventExecutor IEventExecutorGroup.GetNext() => this.GetNext();

        /// <summary>
        /// If running in a context of an existing <see cref="IEventLoop"/>, this <see cref="IEventLoop"/> is returned.
        /// Otherwise, <see cref="IEventLoop"/> is retrieved from underlying <see cref="IEventLoopGroup"/>.
        /// </summary>
        public IEventLoop GetNext()
        {
            IEventExecutor executor;
            if (ExecutionEnvironment.TryGetCurrentExecutor(out executor))
            {
                var loop = executor as IEventLoop;
                if (loop != null && loop.Parent == this.innerGroup)
                {
                    return loop;
                }
            }
            return this.innerGroup.GetNext();
        }

        /// <inheritdoc cref="IEventLoopGroup"/>
        public Task ShutdownGracefullyAsync() => this.innerGroup.ShutdownGracefullyAsync();

        /// <inheritdoc cref="IEventLoopGroup"/>
        public Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout) => this.innerGroup.ShutdownGracefullyAsync(quietPeriod, timeout);
    }
}