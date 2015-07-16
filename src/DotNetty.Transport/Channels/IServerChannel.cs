// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    /// <summary>
    /// A {@link Channel} that accepts an incoming connection attempt and creates
    /// its child {@link Channel}s by accepting them.  {@link ServerSocketChannel} is
    /// a good example.
    /// </summary>
    public interface IServerChannel : IChannel
    {
    }
}