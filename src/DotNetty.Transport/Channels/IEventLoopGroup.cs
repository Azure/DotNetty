// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    /// <inheritdoc />
    /// <summary>
    /// <see cref="IEventExecutorGroup" /> specialized for handling <see cref="IEventLoop" />s.
    /// </summary>
    public interface IEventLoopGroup : IEventExecutorGroup
    {
        /// <summary>
        /// Returns list of owned event loops.
        /// </summary>
        new IEnumerable<IEventLoop> Items { get; }

        /// <summary>
        /// Returns one of owned event loops.
        /// </summary>
        new IEventLoop GetNext();

        /// <summary>
        /// Register the <see cref="IChannel"/> for this event loop.
        /// </summary>
        /// <param name="channel">The <see cref="IChannel"/> to register.</param>
        /// <returns>The register task.</returns>
        Task RegisterAsync(IChannel channel);
    }
}