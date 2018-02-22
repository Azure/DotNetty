// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    public interface IChannelUnsafe
    {
        IRecvByteBufAllocatorHandle RecvBufAllocHandle { get; }

        Task RegisterAsync(IEventLoop eventLoop);

        Task DeregisterAsync();

        Task BindAsync(EndPoint localAddress);

        Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress);

        Task DisconnectAsync();

        Task CloseAsync();

        void CloseForcibly();

        void BeginRead();

        ChannelFuture WriteAsync(object message);

        void Flush();

        ChannelOutboundBuffer OutboundBuffer { get; }
    }
}