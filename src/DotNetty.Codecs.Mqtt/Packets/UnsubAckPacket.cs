// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Packets
{
    public sealed class UnsubAckPacket : PacketWithId
    {
        public override PacketType PacketType => PacketType.UNSUBACK;

        public static UnsubAckPacket InResponseTo(UnsubscribePacket unsubscribePacket)
        {
            return new UnsubAckPacket
            {
                PacketId = unsubscribePacket.PacketId
            };
        }
    }
}