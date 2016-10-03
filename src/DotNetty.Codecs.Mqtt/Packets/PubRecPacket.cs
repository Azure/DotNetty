// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Packets
{
    public sealed class PubRecPacket : PacketWithId
    {
        public override PacketType PacketType => PacketType.PUBREC;

        public static PubRecPacket InResponseTo(PublishPacket publishPacket)
        {
            return new PubRecPacket
            {
                PacketId = publishPacket.PacketId
            };
        }
    }
}