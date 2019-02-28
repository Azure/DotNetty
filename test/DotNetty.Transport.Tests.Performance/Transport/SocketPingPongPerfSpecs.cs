// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Performance.Transport
{
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;

    public sealed class SocketPingPongPerfSpecs : AbstractPingPongPerfSpecs<TcpServerSocketChannel, TcpSocketChannel>
    {
        protected override IEventLoopGroup NewServerGroup() => new MultithreadEventLoopGroup(1);

        protected override IEventLoopGroup NewWorkerGroup(IEventLoopGroup serverGroup) => new MultithreadEventLoopGroup(1);

        protected override IEventLoopGroup NewClientGroup() => new MultithreadEventLoopGroup(1);
    }
}
