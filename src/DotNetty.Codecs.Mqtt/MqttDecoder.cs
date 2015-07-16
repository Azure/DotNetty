// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Transport.Channels;

    public sealed class MqttDecoder : ReplayingDecoder<MqttDecoder.ParseState>
    {
        public enum ParseState
        {
            FixedHeader,
            VariableHeader,
            Payload,
            BadMessage
        }

        readonly bool isServer;
        readonly int maxMessageSize;
        Packet currentPacket; // todo: as we keep whole message, it might keep references to resources for a while (until extra bytes are read). Resources should be assigned right before checkpoints to avoid holding on to things that will be abandoned with next pass
        int remainingLength;

        public MqttDecoder(bool isServer, int maxMessageSize)
            : base(ParseState.FixedHeader)
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
                    case ParseState.FixedHeader:
                        this.DecodeFixedHeader(input);
                        break;
                    case ParseState.VariableHeader:
                        this.DecodeVariableHeader(input);
                        break;
                    case ParseState.Payload:
                        this.DecodePayload(input);
                        break;
                    case ParseState.BadMessage:
                        // read out data until connection is closed
                        input.SkipBytes(input.ReadableBytes);
                        return;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (!this.ReplayRequested)
                {
                    if (this.remainingLength > 0)
                    {
                        throw new DecoderException(string.Format("Declared remaining length is bigger than packet data size by {0}.", this.remainingLength));
                    }

                    // packet was decoded successfully => put it out
                    Packet packet = this.currentPacket;
                    output.Add(packet);
                    this.currentPacket = null;
                    this.Checkpoint(ParseState.FixedHeader);
                    if (MqttEventSource.Log.IsVerboseEnabled)
                    {
                        MqttEventSource.Log.Verbose("Decoded packet.", packet.ToString());
                    }
                }
            }
            catch (DecoderException ex)
            {
                input.SkipBytes(input.ReadableBytes);
                this.Checkpoint(ParseState.BadMessage);
                if (MqttEventSource.Log.IsErrorEnabled)
                {
                    MqttEventSource.Log.Error("Exception while decoding.", ex);
                }

                this.CloseAsync(context);
            }
        }

        void DecodeFixedHeader(IByteBuffer buffer)
        {
            if (!buffer.IsReadable(2))
            {
                // minimal packet consists of at least 2 bytes
                this.RequestReplay();
                return;
            }

            int signature = buffer.ReadByte();

            BufferReadResult<int> remainingLengthResult = this.DecodeRemainingLength(buffer);
            if (!remainingLengthResult.IsSuccessful)
            {
                return;
            }

            this.remainingLength = remainingLengthResult.Value;

            this.Checkpoint(ParseState.VariableHeader);

            if (Signatures.IsPublish(signature))
            {
                var qualityOfService = (QualityOfService)((signature >> 1) & 0x3); // take bits #1 and #2 ONLY and convert them into QoS value
                if (qualityOfService == QualityOfService.Reserved)
                {
                    throw new DecoderException(string.Format("Unexpected QoS value of {0} for {1} packet.", (int)qualityOfService, PacketType.PUBLISH));
                }

                bool duplicate = (signature & 0x8) == 0x8; // test bit#3
                bool retain = (signature & 0x1) != 0; // test bit#0
                var packet = new PublishPacket(qualityOfService, duplicate, retain);
                this.currentPacket = packet;
                this.DecodePublishVariableHeader(buffer, packet);
            }
            else
            {
                switch (signature) // strict match not only checks for valid message type but also validates values in flags part
                {
                    case Signatures.PubAck:
                        var pubAckPacket = new PubAckPacket();
                        this.currentPacket = pubAckPacket;
                        this.DecodePacketIdVariableHeader(buffer, pubAckPacket, false);
                        break;
                    case Signatures.PubRec:
                        var pubRecPacket = new PubRecPacket();
                        this.currentPacket = pubRecPacket;
                        this.DecodePacketIdVariableHeader(buffer, pubRecPacket, false);
                        break;
                    case Signatures.PubRel:
                        var pubRelPacket = new PubRelPacket();
                        this.currentPacket = pubRelPacket;
                        this.DecodePacketIdVariableHeader(buffer, pubRelPacket, false);
                        break;
                    case Signatures.PubComp:
                        var pubCompPacket = new PubCompPacket();
                        this.currentPacket = pubCompPacket;
                        this.DecodePacketIdVariableHeader(buffer, pubCompPacket, false);
                        break;
                    case Signatures.PingReq:
                        this.ValidateServerPacketExpected(signature);
                        this.currentPacket = PingReqPacket.Instance;
                        break;
                    case Signatures.Subscribe:
                        this.ValidateServerPacketExpected(signature);
                        var subscribePacket = new SubscribePacket();
                        this.currentPacket = subscribePacket;
                        this.DecodePacketIdVariableHeader(buffer, subscribePacket, true);
                        break;
                    case Signatures.Unsubscribe:
                        this.ValidateServerPacketExpected(signature);
                        var unsubscribePacket = new UnsubscribePacket();
                        this.currentPacket = unsubscribePacket;
                        this.DecodePacketIdVariableHeader(buffer, unsubscribePacket, true);
                        break;
                    case Signatures.Connect:
                        this.ValidateServerPacketExpected(signature);
                        this.currentPacket = new ConnectPacket();
                        this.DecodeConnectVariableHeader(buffer);
                        break;
                    case Signatures.Disconnect:
                        this.ValidateServerPacketExpected(signature);
                        this.currentPacket = DisconnectPacket.Instance;
                        break;
                    case Signatures.ConnAck:
                        this.ValidateClientPacketExpected(signature);
                        this.currentPacket = new ConnAckPacket();
                        this.DecodeConnAckVariableHeader(buffer);
                        break;
                    case Signatures.SubAck:
                        this.ValidateClientPacketExpected(signature);
                        var subAckPacket = new SubAckPacket();
                        this.currentPacket = subAckPacket;
                        this.DecodePacketIdVariableHeader(buffer, subAckPacket, true);
                        break;
                    case Signatures.UnsubAck:
                        this.ValidateClientPacketExpected(signature);
                        var unsubAckPacket = new UnsubAckPacket();
                        this.currentPacket = unsubAckPacket;
                        this.DecodePacketIdVariableHeader(buffer, unsubAckPacket, false);
                        break;
                    case Signatures.PingResp:
                        this.ValidateClientPacketExpected(signature);
                        this.currentPacket = PingRespPacket.Instance;
                        break;
                    default:
                        throw new DecoderException(string.Format("First packet byte value of `{0}` is invalid.", signature));
                }
            }
        }

        void DecodeVariableHeader(IByteBuffer buffer)
        {
            switch (this.currentPacket.PacketType)
            {
                case PacketType.CONNECT:
                    this.DecodeConnectVariableHeader(buffer);
                    break;
                case PacketType.CONNACK:
                    this.DecodeConnAckVariableHeader(buffer);
                    break;
                case PacketType.PUBLISH:
                    this.DecodePublishVariableHeader(buffer, (PublishPacket)this.currentPacket);
                    break;
                case PacketType.PUBACK:
                case PacketType.PUBREC:
                case PacketType.PUBREL:
                case PacketType.PUBCOMP:
                case PacketType.UNSUBACK:
                    this.DecodePacketIdVariableHeader(buffer, (PacketWithId)this.currentPacket, false);
                    break;
                case PacketType.SUBSCRIBE:
                case PacketType.SUBACK:
                case PacketType.UNSUBSCRIBE:
                    this.DecodePacketIdVariableHeader(buffer, (PacketWithId)this.currentPacket, true);
                    break;
                case PacketType.PINGREQ:
                case PacketType.PINGRESP:
                case PacketType.DISCONNECT:
                    // Empty variable header
                    if (this.remainingLength > 0)
                    {
                        throw new DecoderException(string.Format("Remaining Length for {0} packet must be 0. Actual value: {1}",
                            this.currentPacket.PacketType, this.remainingLength));
                    }
                    break;
                default:
                    throw new NotSupportedException("Unknown message type: " + this.currentPacket.PacketType);
            }
        }

        void DecodePayload(IByteBuffer buffer)
        {
            switch (this.currentPacket.PacketType)
            {
                case PacketType.SUBSCRIBE:
                    this.DecodeSubscribePayload(buffer);
                    break;
                case PacketType.SUBACK:
                    this.DecodeSubAckPayload(buffer);
                    break;
                case PacketType.UNSUBSCRIBE:
                    this.DecodeUnsubscribePayload(buffer);
                    break;
                case PacketType.PUBLISH:
                    this.DecodePublishPayload(buffer, (PublishPacket)this.currentPacket);
                    break;
                default:
                    throw new InvalidOperationException("Unexpected transition to reading payload for packet of type " + this.currentPacket.PacketType);
            }
        }

        void ValidateServerPacketExpected(int signature)
        {
            if (!this.isServer)
            {
                throw new DecoderException(string.Format("Packet type determined through first packet byte `{0}` is not supported by MQTT client.", signature));
            }
        }

        void ValidateClientPacketExpected(int signature)
        {
            if (this.isServer)
            {
                throw new DecoderException(string.Format("Packet type determined through first packet byte `{0}` is not supported by MQTT server.", signature));
            }
        }

        BufferReadResult<int> DecodeRemainingLength(IByteBuffer buffer)
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
                    this.RequestReplay();
                    return BufferReadResult<int>.NoValue;
                }
                digit = buffer.ReadByte();
                result += (digit & 0x7f) * multiplier;
                multiplier <<= 7;
                read++;
            }
            while ((digit & 0x80) != 0 && read < 4);

            if (read == 4 && (digit & 0x80) != 0)
            {
                throw new DecoderException("Remaining length exceeds 4 bytes in length (" + this.currentPacket.PacketType + ')');
            }

            int completeMessageSize = result + 1 + read;
            if (completeMessageSize > this.maxMessageSize)
            {
                throw new InvalidOperationException("Message is too big: " + completeMessageSize);
            }

            return new BufferReadResult<int>(result, read);
        }

        void DecodeConnectVariableHeader(IByteBuffer buffer)
        {
            var packet = (ConnectPacket)this.currentPacket;
            BufferReadResult<string> protocolResult = this.DecodeString(buffer, this.remainingLength);
            if (!protocolResult.IsSuccessful)
            {
                return;
            }
            if (protocolResult.Value != Util.ProtocolName)
            {
                // todo: this assumes channel is 100% dedicated to MQTT, i.e. MQTT is not detected on the wire, it is assumed
                throw new DecoderException(string.Format("Unexpected protocol name. Expected: {0}. Actual: {1}", Util.ProtocolName, protocolResult.Value));
            }
            packet.ProtocolName = Util.ProtocolName;

            int bytesConsumed = protocolResult.BytesConsumed;

            const int HeaderRemainderLength = 4;
            if (this.remainingLength - bytesConsumed < HeaderRemainderLength)
            {
                throw new DecoderException(string.Format("Remaining length value is not big enough to fit variable header. Expected: {0} or more. Actual: {1}",
                    (bytesConsumed + HeaderRemainderLength), this.remainingLength));
            }

            if (!buffer.IsReadable(HeaderRemainderLength))
            {
                this.RequestReplay();
                return;
            }

            packet.ProtocolVersion = buffer.ReadByte();
            bytesConsumed += 1;

            //if (protocolLevel != ProtocolLevel) // todo: move to logic: need to respond with CONNACK 0x01
            //{
            //    throw new DecoderException(string.Format("Unexpected protocol level. Expected: {0}. Actual: {1}", ProtocolLevel, protocolLevel));
            //}

            int connectFlags = buffer.ReadByte();
            bytesConsumed += 1;

            packet.CleanSession = (connectFlags & 0x02) == 0x02;

            bool hasWill = (connectFlags & 0x04) == 0x04;
            if (hasWill)
            {
                packet.WillRetain = (connectFlags & 0x20) == 0x20;
                packet.WillQualityOfService = (QualityOfService)((connectFlags & 0x18) >> 3);
                if (packet.WillQualityOfService == QualityOfService.Reserved)
                {
                    throw new DecoderException(string.Format("[MQTT-3.1.2-14] Unexpected Will QoS value of {0}.", (int)packet.WillQualityOfService));
                }
            }
            else if ((connectFlags & 0x38) != 0) // bits 3,4,5 [MQTT-3.1.2-11]
            {
                throw new DecoderException("[MQTT-3.1.2-11]");
            }

            bool hasUsername = (connectFlags & 0x80) == 0x80;
            bool hasPassword = (connectFlags & 0x40) == 0x40;
            if (packet.HasPassword && !packet.HasUsername)
            {
                throw new DecoderException("[MQTT-3.1.2-22]");
            }
            if ((connectFlags & 0x1) != 0) // [MQTT-3.1.2-3]
            {
                throw new DecoderException("[MQTT-3.1.2-3]");
            }

            BufferReadResult<int> keepAliveResult = this.DecodeShort(buffer);
            bytesConsumed += keepAliveResult.BytesConsumed;
            packet.KeepAliveInSeconds = keepAliveResult.Value;

            this.remainingLength -= bytesConsumed;

            this.DecodeConnectPayload(buffer, hasWill, hasUsername, hasPassword);
        }

        void DecodeConnAckVariableHeader(IByteBuffer buffer)
        {
            if (!buffer.IsReadable(2))
            {
                this.RequestReplay();
                return;
            }

            var packet = (ConnAckPacket)this.currentPacket;

            int ackData = buffer.ReadUnsignedShort();
            packet.SessionPresent = ((ackData >> 8) & 0x1) != 0;
            packet.ReturnCode = (ConnectReturnCode)(ackData & 0xFF);
            this.remainingLength -= 2;
        }

        void DecodePublishVariableHeader(IByteBuffer buffer, PublishPacket packet)
        {
            BufferReadResult<string> topicNameResult = this.DecodeString(buffer, this.remainingLength, 1);
            if (!topicNameResult.IsSuccessful)
            {
                return;
            }
            string topicName = topicNameResult.Value;
            Util.ValidateTopicName(topicName);
            int bytesConsumed = topicNameResult.BytesConsumed;

            packet.TopicName = topicName;
            if (this.currentPacket.QualityOfService > QualityOfService.AtMostOnce)
            {
                BufferReadResult<int> packetIdResult = this.DecodePacketId(buffer, this.remainingLength - bytesConsumed);
                if (!packetIdResult.IsSuccessful)
                {
                    return;
                }
                packet.PacketId = packetIdResult.Value;
                bytesConsumed += packetIdResult.BytesConsumed;
            }

            this.remainingLength -= bytesConsumed;
            this.Checkpoint(ParseState.Payload);

            this.DecodePublishPayload(buffer, packet); // fall-through
        }

        void DecodePacketIdVariableHeader(IByteBuffer buffer, PacketWithId packet, bool expectPayload)
        {
            BufferReadResult<int> packetIdResult = this.DecodePacketId(buffer, this.remainingLength);
            if (!packetIdResult.IsSuccessful)
            {
                return;
            }

            packet.PacketId = packetIdResult.Value;

            this.remainingLength -= packetIdResult.BytesConsumed;

            if (expectPayload)
            {
                this.Checkpoint(ParseState.Payload);

                this.DecodePayload(buffer); // fall-through
            }
        }

        BufferReadResult<int> DecodePacketId(IByteBuffer buffer, int currentRemainingLength)
        {
            if (currentRemainingLength < 2)
            {
                throw new DecoderException(string.Format("Remaining Length is not big enough to accomodate at least 2 bytes (for packet identifier). Available value: {0}",
                    this.remainingLength));
            }

            BufferReadResult<int> packetIdResult = this.DecodeShort(buffer);
            if (packetIdResult.IsSuccessful)
            {
                Util.ValidatePacketId(packetIdResult.Value);
            }

            return packetIdResult;
        }

        void DecodeConnectPayload(IByteBuffer buffer, bool hasWill, bool hasUsername, bool hasPassword)
        {
            var packet = (ConnectPacket)this.currentPacket;

            BufferReadResult<string> clientIdResult = this.DecodeString(buffer, this.remainingLength);
            if (!clientIdResult.IsSuccessful)
            {
                return;
            }
            string clientId = clientIdResult.Value;
            Util.ValidateClientId(clientId);
            packet.ClientId = clientId;
            int bytesConsumed = clientIdResult.BytesConsumed;

            if (hasWill)
            {
                BufferReadResult<string> willTopicResult = this.DecodeString(buffer, this.remainingLength - bytesConsumed);
                if (!willTopicResult.IsSuccessful)
                {
                    return;
                }
                packet.WillTopic = willTopicResult.Value;
                BufferReadResult<int> willMessageLengthResult = this.DecodeShort(buffer);
                if (!willMessageLengthResult.IsSuccessful)
                {
                    return;
                }
                int willMessageLength = willMessageLengthResult.Value;
                if (!buffer.IsReadable(willMessageLength))
                {
                    this.RequestReplay();
                    return;
                }
                packet.WillMessage = buffer.ReadBytes(willMessageLength);
                bytesConsumed += willTopicResult.BytesConsumed + willMessageLengthResult.BytesConsumed + willMessageLength;
            }

            if (hasUsername)
            {
                BufferReadResult<string> usernameResult = this.DecodeString(buffer, this.remainingLength - bytesConsumed);
                if (!usernameResult.IsSuccessful)
                {
                    return;
                }
                packet.Username = usernameResult.Value;
                bytesConsumed += usernameResult.BytesConsumed;
            }

            if (hasPassword)
            {
                BufferReadResult<string> passwordResult = this.DecodeString(buffer, this.remainingLength - bytesConsumed);
                if (!passwordResult.IsSuccessful)
                {
                    return;
                }
                packet.Password = passwordResult.Value;
                bytesConsumed += passwordResult.BytesConsumed;
            }

            this.remainingLength -= bytesConsumed;
        }

        void DecodeSubscribePayload(IByteBuffer buffer)
        {
            var subscribeTopics = new List<SubscriptionRequest>();
            int bytesConsumed = 0;
            while (bytesConsumed < this.remainingLength)
            {
                BufferReadResult<string> topicFilterResult = this.DecodeString(buffer, this.remainingLength - bytesConsumed);
                if (!topicFilterResult.IsSuccessful)
                {
                    return;
                }
                string topicFilter = topicFilterResult.Value;
                ValidateTopicFilter(topicFilter);
                bytesConsumed += topicFilterResult.BytesConsumed;

                if (!buffer.IsReadable())
                {
                    this.RequestReplay();
                    return;
                }
                int qos = buffer.ReadByte();
                if (qos >= (int)QualityOfService.Reserved)
                {
                    throw new DecoderException(string.Format("[MQTT-3.8.3-4] Requested QoS is not a valid value: {0}.", qos));
                }
                bytesConsumed++;

                subscribeTopics.Add(new SubscriptionRequest(topicFilter, (QualityOfService)qos));
            }

            if (subscribeTopics.Count == 0)
            {
                throw new DecoderException("[MQTT-3.8.3-3]");
            }

            var packet = (SubscribePacket)this.currentPacket;
            packet.Requests = subscribeTopics;

            this.remainingLength = 0;
        }

        static void ValidateTopicFilter(string topicFilter)
        {
            int length = topicFilter.Length;
            if (length == 0)
            {
                throw new DecoderException("[MQTT-4.7.3-1]");
            }

            for (int i = 0; i < length - 1; i++)
            {
                char c = topicFilter[i];
                if (c == '+')
                {
                    if ((i > 0 && topicFilter[i - 1] != '/') || (i < length - 1 && topicFilter[i + 1] != '/'))
                    {
                        throw new DecoderException("[MQTT-4.7.1-3]");
                    }
                }
                if (c == '#')
                {
                    if (i < length - 1 || (i > 0 && topicFilter[i - 1] != '/'))
                    {
                        throw new DecoderException("[MQTT-4.7.1-2]");
                    }
                }
            }
        }

        void DecodeSubAckPayload(IByteBuffer buffer)
        {
            int length = this.remainingLength;
            if (!buffer.IsReadable(length))
            {
                this.RequestReplay();
                return;
            }

            var packet = (SubAckPacket)this.currentPacket;
            var codes = new QualityOfService[length];
            for (int i = 0; i < length; i++)
            {
                codes[i] = (QualityOfService)buffer.ReadByte();
            }
            packet.ReturnCodes = codes;

            this.remainingLength = 0;
        }

        void DecodeUnsubscribePayload(IByteBuffer buffer)
        {
            var unsubscribeTopics = new List<string>();
            int bytesConsumed = 0;
            while (bytesConsumed < this.remainingLength)
            {
                BufferReadResult<string> topicFilterResult = this.DecodeString(buffer, this.remainingLength - bytesConsumed);
                if (!topicFilterResult.IsSuccessful)
                {
                    return;
                }
                string topicFilter = topicFilterResult.Value;
                ValidateTopicFilter(topicFilter);
                bytesConsumed += topicFilterResult.BytesConsumed;
                unsubscribeTopics.Add(topicFilter);
            }

            if (unsubscribeTopics.Count == 0)
            {
                throw new DecoderException("[MQTT-3.10.3-2]");
            }

            var packet = (UnsubscribePacket)this.currentPacket;
            packet.TopicFilters = unsubscribeTopics;

            this.remainingLength = 0;
        }

        void DecodePublishPayload(IByteBuffer buffer, PublishPacket packet)
        {
            if (!buffer.IsReadable(this.remainingLength))
            {
                // buffering whole packet payload in memory
                this.RequestReplay();
                return;
            }

            IByteBuffer payload = buffer.ReadSlice(this.remainingLength);
            payload.Retain();
            packet.Payload = payload;
            this.remainingLength = 0;
        }

        BufferReadResult<int> DecodeShort(IByteBuffer buffer)
        {
            if (!buffer.IsReadable(2))
            {
                this.RequestReplay();
                return BufferReadResult<int>.NoValue;
            }

            byte msb = buffer.ReadByte();
            byte lsb = buffer.ReadByte();
            return new BufferReadResult<int>(msb << 8 | lsb, 2);
        }

        BufferReadResult<string> DecodeString(IByteBuffer buffer, int currentRemainingLength, int minBytes = 0, int maxBytes = int.MaxValue)
        {
            BufferReadResult<int> sizeResult = this.DecodeShort(buffer);
            if (!sizeResult.IsSuccessful)
            {
                return BufferReadResult<string>.NoValue;
            }

            int size = sizeResult.Value;
            int bytesConsumed = sizeResult.BytesConsumed;
            if (size + bytesConsumed > currentRemainingLength)
            {
                throw new DecoderException(string.Format("String value is longer than might fit in current Remaining Length of {0}. Available bytes: {1}",
                    currentRemainingLength - bytesConsumed, size));
            }
            if (size < minBytes)
            {
                throw new DecoderException(string.Format("String value is shorter than minimum allowed {0}. Advertised length: {1}", minBytes, size));
            }
            if (size > maxBytes)
            {
                throw new DecoderException(string.Format("String value is longer than maximum allowed {0}. Advertised length: {1}", maxBytes, size));
            }

            // todo: review why they did it
            //if (size < minBytes || size > maxBytes)
            //{
            //    buffer.skipBytes(size);
            //    numberOfBytesConsumed += size;
            //    return new Result<String>(null, numberOfBytesConsumed);
            //}

            if (!buffer.IsReadable(size))
            {
                this.RequestReplay();
                return BufferReadResult<string>.NoValue;
            }

            string value = Encoding.UTF8.GetString(buffer.Array, buffer.ArrayOffset + buffer.ReaderIndex, size);
            // todo: enforce string definition by MQTT spec
            buffer.SetReaderIndex(buffer.ReaderIndex + size);
            bytesConsumed += size;
            return new BufferReadResult<string>(value, bytesConsumed);
        }

        struct BufferReadResult<T>
        {
            public readonly T Value;
            public readonly int BytesConsumed;

            public static BufferReadResult<T> NoValue
            {
                get { return new BufferReadResult<T>(default(T), 0); }
            }

            public BufferReadResult(T value, int bytesConsumed)
            {
                this.Value = value;
                this.BytesConsumed = bytesConsumed;
            }

            public bool IsSuccessful
            {
                get { return this.BytesConsumed > 0; }
            }
        }
    }
}