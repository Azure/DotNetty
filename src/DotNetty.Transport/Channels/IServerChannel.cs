// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using DotNetty.Transport.Channels.Sockets;

    /// <summary>
    /// A <see cref="IChannel"/> that accepts an incoming connection attempt and creates its child
    /// <see cref="IChannel"/>s by accepting them. <see cref="IServerSocketChannel"/> is a good example.
    /// </summary>
    public interface IServerChannel : IChannel
    {
    }
}