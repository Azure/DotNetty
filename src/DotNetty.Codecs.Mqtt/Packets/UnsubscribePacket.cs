// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Packets
{
    using System.Collections.Generic;

    public sealed class UnsubscribePacket : PacketWithId
    {
        public UnsubscribePacket()
        {
        }

        public UnsubscribePacket(int packetId, params string[] topicFilters)
        {
            this.PacketId = packetId;
            this.TopicFilters = topicFilters;
        }

        public override PacketType PacketType
        {
            get { return PacketType.UNSUBSCRIBE; }
        }

        public override QualityOfService QualityOfService
        {
            get { return QualityOfService.AtLeastOnce; }
        }

        public IEnumerable<string> TopicFilters { get; set; }
    }
}