// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt
{
    using System.Runtime.CompilerServices;
    using DotNetty.Codecs.Mqtt.Packets;

    static class Signatures
    {
        const byte QoS1Signature = (int)QualityOfService.AtLeastOnce << 1;

        // most often used (anticipated) come first

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPublish(int signature)
        {
            const byte TypeOnlyMask = 0xf << 4;
            return (signature & TypeOnlyMask) == ((int)PacketType.PUBLISH << 4);
        }

        public const byte PubAck = (int)PacketType.PUBACK << 4;
        public const byte PubRec = (int)PacketType.PUBREC << 4;
        public const byte PubRel = ((int)PacketType.PUBREL << 4) | QoS1Signature;
        public const byte PubComp = (int)PacketType.PUBCOMP << 4;
        public const byte Connect = (int)PacketType.CONNECT << 4;
        public const byte ConnAck = (int)PacketType.CONNACK << 4;
        public const byte Subscribe = ((int)PacketType.SUBSCRIBE << 4) | QoS1Signature;
        public const byte SubAck = (int)PacketType.SUBACK << 4;
        public const byte PingReq = (int)PacketType.PINGREQ << 4;
        public const byte PingResp = (int)PacketType.PINGRESP << 4;
        public const byte Disconnect = (int)PacketType.DISCONNECT << 4;
        public const byte Unsubscribe = ((int)PacketType.UNSUBSCRIBE << 4) | QoS1Signature;
        public const byte UnsubAck = (int)PacketType.UNSUBACK << 4;
    }
}