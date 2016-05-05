// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Packets
{
    public sealed class PingRespPacket : Packet
    {
        public static readonly PingRespPacket Instance = new PingRespPacket();

        PingRespPacket()
        {
        }

        public override PacketType PacketType => PacketType.PINGRESP;
    }
}