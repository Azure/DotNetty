// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Performance.Transport
{
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv;

    public sealed class LibuvPingPongPerfSpecs : AbstractPingPongPerfSpecs<TcpServerChannel, TcpChannel>
    {
        protected override IEventLoopGroup NewServerGroup() => new DispatcherEventLoopGroup();

        protected override IEventLoopGroup NewWorkerGroup(IEventLoopGroup serverGroup) => new WorkerEventLoopGroup((DispatcherEventLoopGroup)serverGroup, 1);

        protected override IEventLoopGroup NewClientGroup() => new EventLoopGroup(1);
    }
}
