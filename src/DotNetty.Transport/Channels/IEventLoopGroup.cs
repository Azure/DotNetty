// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using DotNetty.Common.Concurrency;

    /// <summary>
    /// <see cref="IEventExecutorGroup"/> specialized for handling <see cref="IEventLoop"/>s.
    /// </summary>
    public interface IEventLoopGroup : IEventExecutorGroup
    {
        /// <summary>
        /// Returns <see cref="IEventLoop"/>.
        /// </summary>
        new IEventLoop GetNext();
    }
}