// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Packets
{
    public sealed class PubCompPacket : PacketWithId
    {
        public override PacketType PacketType => PacketType.PUBCOMP;

        public static PubCompPacket InResponseTo(PubRelPacket publishPacket)
        {
            return new PubCompPacket
            {
                PacketId = publishPacket.PacketId
            };
        }
    }
}