// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Packets
{
    public abstract class PacketWithId : Packet
    {
        public int PacketId { get; set; }
    }
}