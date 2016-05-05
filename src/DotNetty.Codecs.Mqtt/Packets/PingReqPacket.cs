// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Packets
{
    public sealed class PingReqPacket : Packet
    {
        public static readonly PingReqPacket Instance = new PingReqPacket();

        PingReqPacket()
        {
        }

        public override PacketType PacketType => PacketType.PINGREQ;
    }
}