// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Packets
{
    using System.Collections.Generic;

    public sealed class SubscribePacket : PacketWithId
    {
        public SubscribePacket()
        {
        }

        public SubscribePacket(int packetId, params SubscriptionRequest[] requests)
        {
            this.PacketId = packetId;
            this.Requests = requests;
        }

        public override PacketType PacketType => PacketType.SUBSCRIBE;

        public override QualityOfService QualityOfService => QualityOfService.AtLeastOnce;

        public IReadOnlyList<SubscriptionRequest> Requests { get; set; }
    }
}