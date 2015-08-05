// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Buffers;

    public interface IChannel
    {
        IChannelId Id { get; }

        IByteBufferAllocator Allocator { get; }

        IEventLoop EventLoop { get; }

        IChannel Parent { get; }

        bool DisconnectSupported { get; }

        bool Open { get; }

        bool Active { get; }

        bool Registered { get; }

        EndPoint LocalAddress { get; }

        EndPoint RemoteAddress { get; }

        bool IsWritable { get; }

        IChannelUnsafe Unsafe { get; }

        IChannelPipeline Pipeline { get; }

        IChannelConfiguration Configuration { get; }

        Task CloseCompletion { get; }

        Task DeregisterAsync();

        Task BindAsync(EndPoint localAddress);

        Task ConnectAsync(EndPoint remoteAddress);

        Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress);

        Task DisconnectAsync();

        Task CloseAsync();

        // todo: make these available through separate interface to hide them from public API on channel

        IChannel Read();

        Task WriteAsync(object message);

        IChannel Flush();

        Task WriteAndFlushAsync(object message);
    }
}