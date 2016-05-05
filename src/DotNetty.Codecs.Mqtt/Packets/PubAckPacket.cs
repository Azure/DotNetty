// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Packets
{
    public sealed class PubAckPacket : PacketWithId
    {
        public override PacketType PacketType => PacketType.PUBACK;

        public static PubAckPacket InResponseTo(PublishPacket publishPacket)
        {
            return new PubAckPacket
            {
                PacketId = publishPacket.PacketId
            };
        }
    }
}