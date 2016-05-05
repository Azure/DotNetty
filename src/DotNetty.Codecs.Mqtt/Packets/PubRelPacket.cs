// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Packets
{
    public sealed class PubRelPacket : PacketWithId
    {
        public override PacketType PacketType => PacketType.PUBREL;

        public override QualityOfService QualityOfService => QualityOfService.AtLeastOnce;

        public static PubRelPacket InResponseTo(PubRecPacket publishPacket)
        {
            return new PubRelPacket
            {
                PacketId = publishPacket.PacketId
            };
        }
    }
}