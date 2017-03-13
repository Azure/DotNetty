// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Transport.Channels;

    public sealed class MqttDecoder : ReplayingDecoder<MqttDecoder.ParseState>
    {
        public enum ParseState
        {
            Ready,
            Failed
        }

        readonly bool isServer;
        readonly int maxMessageSize;

        public MqttDecoder(bool isServer, int maxMessageSize)
            : base(ParseState.Ready)
        {
            this.isServer = isServer;
            this.maxMessageSize = maxMessageSize;
        }

        protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            try
            {
                switch (this.State)
                {
                    case ParseState.Ready:
                        Packet packet;

                        if (!this.TryDecodePacket(input, context, out packet))
                        {
                            this.RequestReplay();
                            return;
                        }

                        output.Add(packet);
                        this.Checkpoint();
                        break;
                    case ParseState.Failed:
                        // read out data until connection is closed
                        input.SkipBytes(input.ReadableBytes);
                        return;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (DecoderException)
            {
                input.SkipBytes(input.ReadableBytes);
                this.Checkpoint(ParseState.Failed);
                throw;
            }
        }

        bool TryDecodePacket(IByteBuffer buffer, IChannelHandlerContext context, out Packet packet)
        {
            if (!buffer.IsReadable(2)) // packet consists of at least 2 bytes
            {
                packet = null;
                return false;
            }

            int signature = buffer.ReadByte();

            int remainingLength;
            if (!this.TryDecodeRemainingLength(buffer, out remainingLength) || !buffer.IsReadable(remainingLength))
            {
                packet = null;
                return false;
            }

            packet = this.DecodePacketInternal(buffer, signature, ref remainingLength, context);

            if (remainingLength > 0)
            {
                throw new DecoderException($"Declared remaining length is bigger than packet data size by {remainingLength}.");
            }

            return true;
        }

        Packet DecodePacketInternal(IByteBuffer buffer, int packetSignature, ref int remainingLength, IChannelHandlerContext context)
        {
            if (Signatures.IsPublish(packetSignature))
            {
                var qualityOfService = (QualityOfService)((packetSignature >> 1) & 0x3); // take bits #1 and #2 ONLY and convert them into QoS value
                if (qualityOfService == QualityOfService.Reserved)
                {
                    throw new DecoderException($"Unexpected QoS value of {(int)qualityOfService} for {PacketType.PUBLISH} packet.");
                }

                bool duplicate = (packetSignature & 0x8) == 0x8; // test bit#3
                bool retain = (packetSignature & 0x1) != 0; // test bit#0
                var packet = new PublishPacket(qualityOfService, duplicate, retain);
                DecodePublishPacket(buffer, packet, ref remainingLength);
                return packet;
            }

            switch (packetSignature) // strict match checks for valid message type + correct values in flags part
            {
                case Signatures.PubAck:
                    var pubAckPacket = new PubAckPacket();
                    DecodePacketIdVariableHeader(buffer, pubAckPacket, ref remainingLength);
                    return pubAckPacket;
                case Signatures.PubRec:
                    var pubRecPacket = new PubRecPacket();
                    DecodePacketIdVariableHeader(buffer, pubRecPacket, ref remainingLength);
                    return pubRecPacket;
                case Signatures.PubRel:
                    var pubRelPacket = new PubRelPacket();
                    DecodePacketIdVariableHeader(buffer, pubRelPacket, ref remainingLength);
                    return pubRelPacket;
                case Signatures.PubComp:
                    var pubCompPacket = new PubCompPacket();
                    DecodePacketIdVariableHeader(buffer, pubCompPacket, ref remainingLength);
                    return pubCompPacket;
                case Signatures.PingReq:
                    this.ValidateServerPacketExpected(packetSignature);
                    return PingReqPacket.Instance;
                case Signatures.Subscribe:
                    this.ValidateServerPacketExpected(packetSignature);
                    var subscribePacket = new SubscribePacket();
                    DecodePacketIdVariableHeader(buffer, subscribePacket, ref remainingLength);
                    DecodeSubscribePayload(buffer, subscribePacket, ref remainingLength);
                    return subscribePacket;
                case Signatures.Unsubscribe:
                    this.ValidateServerPacketExpected(packetSignature);
                    var unsubscribePacket = new UnsubscribePacket();
                    DecodePacketIdVariableHeader(buffer, unsubscribePacket, ref remainingLength);
                    DecodeUnsubscribePayload(buffer, unsubscribePacket, ref remainingLength);
                    return unsubscribePacket;
                case Signatures.Connect:
                    this.ValidateServerPacketExpected(packetSignature);
                    var connectPacket = new ConnectPacket();
                    DecodeConnectPacket(buffer, connectPacket, ref remainingLength, context);
                    return connectPacket;
                case Signatures.Disconnect:
                    this.ValidateServerPacketExpected(packetSignature);
                    return DisconnectPacket.Instance;
                case Signatures.ConnAck:
                    this.ValidateClientPacketExpected(packetSignature);
                    var connAckPacket = new ConnAckPacket();
                    DecodeConnAckPacket(buffer, connAckPacket, ref remainingLength);
                    return connAckPacket;
                case Signatures.SubAck:
                    this.ValidateClientPacketExpected(packetSignature);
                    var subAckPacket = new SubAckPacket();
                    DecodePacketIdVariableHeader(buffer, subAckPacket, ref remainingLength);
                    DecodeSubAckPayload(buffer, subAckPacket, ref remainingLength);
                    return subAckPacket;
                case Signatures.UnsubAck:
                    this.ValidateClientPacketExpected(packetSignature);
                    var unsubAckPacket = new UnsubAckPacket();
                    DecodePacketIdVariableHeader(buffer, unsubAckPacket, ref remainingLength);
                    return unsubAckPacket;
                case Signatures.PingResp:
                    this.ValidateClientPacketExpected(packetSignature);
                    return PingRespPacket.Instance;
                default:
                    throw new DecoderException($"First packet byte value of `{packetSignature}` is invalid.");
            }
        }

        void ValidateServerPacketExpected(int signature)
        {
            if (!this.isServer)
            {
                throw new DecoderException($"Packet type determined through first packet byte `{signature}` is not supported by MQTT client.");
            }
        }

        void ValidateClientPacketExpected(int signature)
        {
            if (this.isServer)
            {
                throw new DecoderException($"Packet type determined through first packet byte `{signature}` is not supported by MQTT server.");
            }
        }

        bool TryDecodeRemainingLength(IByteBuffer buffer, out int value)
        {
            int readable = buffer.ReadableBytes;

            int result = 0;
            int multiplier = 1;
            byte digit;
            int read = 0;
            do
            {
                if (readable < read + 1)
                {
                    value = default(int);
                    return false;
                }
                digit = buffer.ReadByte();
                result += (digit & 0x7f) * multiplier;
                multiplier <<= 7;
                read++;
            }
            while ((digit & 0x80) != 0 && read < 4);

            if (read == 4 && (digit & 0x80) != 0)
            {
                throw new DecoderException("Remaining length exceeds 4 bytes in length");
            }

            int completeMessageSize = result + 1 + read;
            if (completeMessageSize > this.maxMessageSize)
            {
                throw new DecoderException("Message is too big: " + completeMessageSize);
            }

            value = result;
            return true;
        }

        static void DecodeConnectPacket(IByteBuffer buffer, ConnectPacket packet, ref int remainingLength, IChannelHandlerContext context)
        {
            string protocolName = DecodeString(buffer, ref remainingLength);
            if (!Util.ProtocolName.Equals(protocolName, StringComparison.Ordinal))
            {
                throw new DecoderException($"Unexpected protocol name. Expected: {Util.ProtocolName}. Actual: {protocolName}");
            }
            packet.ProtocolName = Util.ProtocolName;

            DecreaseRemainingLength(ref remainingLength, 1);
            packet.ProtocolLevel = buffer.ReadByte();

            if (packet.ProtocolLevel != Util.ProtocolLevel)
            {
                var connAckPacket = new ConnAckPacket();
                connAckPacket.ReturnCode = ConnectReturnCode.RefusedUnacceptableProtocolVersion;
                context.WriteAndFlushAsync(connAckPacket);
                throw new DecoderException($"Unexpected protocol level. Expected: {Util.ProtocolLevel}. Actual: {packet.ProtocolLevel}");
            }

            DecreaseRemainingLength(ref remainingLength, 1);
            int connectFlags = buffer.ReadByte();

            packet.CleanSession = (connectFlags & 0x02) == 0x02;

            bool hasWill = (connectFlags & 0x04) == 0x04;
            if (hasWill)
            {
                packet.HasWill = true;
                packet.WillRetain = (connectFlags & 0x20) == 0x20;
                packet.WillQualityOfService = (QualityOfService)((connectFlags & 0x18) >> 3);
                if (packet.WillQualityOfService == QualityOfService.Reserved)
                {
                    throw new DecoderException($"[MQTT-3.1.2-14] Unexpected Will QoS value of {(int)packet.WillQualityOfService}.");
                }
                packet.WillTopicName = string.Empty;
            }
            else if ((connectFlags & 0x38) != 0) // bits 3,4,5 [MQTT-3.1.2-11]
            {
                throw new DecoderException("[MQTT-3.1.2-11]");
            }

            packet.HasUsername = (connectFlags & 0x80) == 0x80;
            packet.HasPassword = (connectFlags & 0x40) == 0x40;
            if (packet.HasPassword && !packet.HasUsername)
            {
                throw new DecoderException("[MQTT-3.1.2-22]");
            }
            if ((connectFlags & 0x1) != 0) // [MQTT-3.1.2-3]
            {
                throw new DecoderException("[MQTT-3.1.2-3]");
            }

            packet.KeepAliveInSeconds = DecodeUnsignedShort(buffer, ref remainingLength);

            string clientId = DecodeString(buffer, ref remainingLength);
            Util.ValidateClientId(clientId);
            packet.ClientId = clientId;

            if (hasWill)
            {
                packet.WillTopicName = DecodeString(buffer, ref remainingLength);
                int willMessageLength = DecodeUnsignedShort(buffer, ref remainingLength);
                DecreaseRemainingLength(ref remainingLength, willMessageLength);
                packet.WillMessage = buffer.ReadBytes(willMessageLength);
            }

            if (packet.HasUsername)
            {
                packet.Username = DecodeString(buffer, ref remainingLength);
            }

            if (packet.HasPassword)
            {
                packet.Password = DecodeString(buffer, ref remainingLength);
            }
        }

        static void DecodeConnAckPacket(IByteBuffer buffer, ConnAckPacket packet, ref int remainingLength)
        {
            int ackData = DecodeUnsignedShort(buffer, ref remainingLength);
            packet.SessionPresent = ((ackData >> 8) & 0x1) != 0;
            packet.ReturnCode = (ConnectReturnCode)(ackData & 0xFF);
        }

        static void DecodePublishPacket(IByteBuffer buffer, PublishPacket packet, ref int remainingLength)
        {
            string topicName = DecodeString(buffer, ref remainingLength, 1);
            Util.ValidateTopicName(topicName);

            packet.TopicName = topicName;
            if (packet.QualityOfService > QualityOfService.AtMostOnce)
            {
                DecodePacketIdVariableHeader(buffer, packet, ref remainingLength);
            }

            IByteBuffer payload;
            if (remainingLength > 0)
            {
                payload = buffer.ReadSlice(remainingLength);
                payload.Retain();
                remainingLength = 0;
            }
            else
            {
                payload = Unpooled.Empty;
            }
            packet.Payload = payload;
        }

        static void DecodePacketIdVariableHeader(IByteBuffer buffer, PacketWithId packet, ref int remainingLength)
        {
            int packetId = packet.PacketId = DecodeUnsignedShort(buffer, ref remainingLength);
            if (packetId == 0)
            {
                throw new DecoderException("[MQTT-2.3.1-1]");
            }
        }

        static void DecodeSubscribePayload(IByteBuffer buffer, SubscribePacket packet, ref int remainingLength)
        {
            var subscribeTopics = new List<SubscriptionRequest>();
            while (remainingLength > 0)
            {
                string topicFilter = DecodeString(buffer, ref remainingLength);
                ValidateTopicFilter(topicFilter);

                DecreaseRemainingLength(ref remainingLength, 1);
                int qos = buffer.ReadByte();
                if (qos >= (int)QualityOfService.Reserved)
                {
                    throw new DecoderException($"[MQTT-3.8.3-4]. Invalid QoS value: {qos}.");
                }

                subscribeTopics.Add(new SubscriptionRequest(topicFilter, (QualityOfService)qos));
            }

            if (subscribeTopics.Count == 0)
            {
                throw new DecoderException("[MQTT-3.8.3-3]");
            }

            packet.Requests = subscribeTopics;
        }

        static void ValidateTopicFilter(string topicFilter)
        {
            int length = topicFilter.Length;
            if (length == 0)
            {
                throw new DecoderException("[MQTT-4.7.3-1]");
            }

            for (int i = 0; i < length; i++)
            {
                char c = topicFilter[i];
                switch (c)
                {
                    case '+':
                        if ((i > 0 && topicFilter[i - 1] != '/') || (i < length - 1 && topicFilter[i + 1] != '/'))
                        {
                            throw new DecoderException($"[MQTT-4.7.1-3]. Invalid topic filter: {topicFilter}");
                        }
                        break;
                    case '#':
                        if (i < length - 1 || (i > 0 && topicFilter[i - 1] != '/'))
                        {
                            throw new DecoderException($"[MQTT-4.7.1-2]. Invalid topic filter: {topicFilter}");
                        }
                        break;
                }
            }
        }

        static void DecodeSubAckPayload(IByteBuffer buffer, SubAckPacket packet, ref int remainingLength)
        {
            var returnCodes = new QualityOfService[remainingLength];
            for (int i = 0; i < remainingLength; i++)
            {
                var returnCode = (QualityOfService)buffer.ReadByte();
                if (returnCode > QualityOfService.ExactlyOnce && returnCode != QualityOfService.Failure)
                {
                    throw new DecoderException($"[MQTT-3.9.3-2]. Invalid return code: {returnCode}");
                }
                returnCodes[i] = returnCode;
            }
            packet.ReturnCodes = returnCodes;

            remainingLength = 0;
        }

        static void DecodeUnsubscribePayload(IByteBuffer buffer, UnsubscribePacket packet, ref int remainingLength)
        {
            var unsubscribeTopics = new List<string>();
            while (remainingLength > 0)
            {
                string topicFilter = DecodeString(buffer, ref remainingLength);
                ValidateTopicFilter(topicFilter);
                unsubscribeTopics.Add(topicFilter);
            }

            if (unsubscribeTopics.Count == 0)
            {
                throw new DecoderException("[MQTT-3.10.3-2]");
            }

            packet.TopicFilters = unsubscribeTopics;

            remainingLength = 0;
        }

        static int DecodeUnsignedShort(IByteBuffer buffer, ref int remainingLength)
        {
            DecreaseRemainingLength(ref remainingLength, 2);
            return buffer.ReadUnsignedShort();
        }

        static string DecodeString(IByteBuffer buffer, ref int remainingLength) => DecodeString(buffer, ref remainingLength, 0, int.MaxValue);

        static string DecodeString(IByteBuffer buffer, ref int remainingLength, int minBytes) => DecodeString(buffer, ref remainingLength, minBytes, int.MaxValue);

        static string DecodeString(IByteBuffer buffer, ref int remainingLength, int minBytes, int maxBytes)
        {
            int size = DecodeUnsignedShort(buffer, ref remainingLength);

            if (size < minBytes)
            {
                throw new DecoderException($"String value is shorter than minimum allowed {minBytes}. Advertised length: {size}");
            }
            if (size > maxBytes)
            {
                throw new DecoderException($"String value is longer than maximum allowed {maxBytes}. Advertised length: {size}");
            }

            if (size == 0)
            {
                return string.Empty;
            }

            DecreaseRemainingLength(ref remainingLength, size);

            string value = buffer.ToString(buffer.ReaderIndex, size, Encoding.UTF8);
            // todo: enforce string definition by MQTT spec
            buffer.SetReaderIndex(buffer.ReaderIndex + size);
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // we don't care about the method being on exception's stack so it's OK to inline
        static void DecreaseRemainingLength(ref int remainingLength, int minExpectedLength)
        {
            if (remainingLength < minExpectedLength)
            {
                throw new DecoderException($"Current Remaining Length of {remainingLength} is smaller than expected {minExpectedLength}.");
            }
            remainingLength -= minExpectedLength;
        }
    }
}