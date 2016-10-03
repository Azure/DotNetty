// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Packets
{
    public sealed class DisconnectPacket : Packet
    {
        public static readonly DisconnectPacket Instance = new DisconnectPacket();

        DisconnectPacket()
        {
        }

        public override PacketType PacketType => PacketType.DISCONNECT;
    }
}