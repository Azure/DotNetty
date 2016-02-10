// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    public interface ISocketChannelConfiguration : IChannelConfiguration
    {
        bool AllowHalfClosure { get; set; }

        int ReceiveBufferSize { get; set; }

        int SendBufferSize { get; set; }

        int Linger { get; set; }

        bool KeepAlive { get; set; }

        bool ReuseAddress { get; set; }

        bool TcpNoDelay { get; set; }
    }
}