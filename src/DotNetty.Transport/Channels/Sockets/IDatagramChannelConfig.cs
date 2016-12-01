// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System.Net;
    using System.Net.NetworkInformation;

    public interface IDatagramChannelConfig : IChannelConfiguration
    {
        int SendBufferSize { get; set; }

        int ReceiveBufferSize { get; set; }

        int TrafficClass { get; set; }

        bool ReuseAddress { get; set; }

        bool Broadcast { get; set; }

        bool LoopbackModeDisabled { get; set; }

        short TimeToLive { get; set; }

        EndPoint Interface { get; set; }

        NetworkInterface NetworkInterface { get; set; }
    }
}