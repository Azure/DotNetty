// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Transport.Channels;
    using Moq;
    using Xunit;

    public class MqttCodecTests
    {
        static readonly IByteBufferAllocator Allocator = new UnpooledByteBufferAllocator();

        readonly MqttDecoder serverDecoder;
        readonly MqttDecoder clientDecoder;
        readonly Mock<IChannelHandlerContext> contextMock;

        public MqttCodecTests()
        {
            this.serverDecoder = new MqttDecoder(true, 256 * 1024);
            this.clientDecoder = new MqttDecoder(false, 256 * 1024);
            this.contextMock = new Mock<IChannelHandlerContext>(MockBehavior.Strict);
            this.contextMock.Setup(x => x.Removed).Returns(false);
            this.contextMock.Setup(x => x.Allocator).Returns(UnpooledByteBufferAllocator.Default);
        }

        [Theory]
        [InlineData("a", true, 0, null, null, "will/topic/name", new byte[] { 5, 3, 255, 6, 5 }, QualityOfService.ExactlyOnce, true)]
        [InlineData("11a_2", false, 1, "user1", null, "will", new byte[0], QualityOfService.AtLeastOnce, false)]
        [InlineData("abc/ж", false, 10, "", "pwd", null, null, null, false)]
        [InlineData("", true, 1000, "имя", "密碼", null, null, null, false)]
        public void TestConnectMessage(string clientId, bool cleanSession, int keepAlive, string userName,
            string password, string willTopicName, byte[] willMessage, QualityOfService? willQos, bool willRetain)
        {
            var packet = new ConnectPacket();
            packet.ClientId = clientId;
            packet.CleanSession = cleanSession;
            packet.KeepAliveInSeconds = keepAlive;
            if (userName != null)
            {
                packet.Username = userName;
                if (password != null)
                {
                    packet.Password = password;
                }
            }
            if (willTopicName != null)
            {
                packet.WillTopicName = willTopicName;
                packet.WillMessage = Unpooled.WrappedBuffer(willMessage);
                packet.WillQualityOfService = willQos ?? QualityOfService.AtMostOnce;
                packet.WillRetain = willRetain;
            }

            ConnectPacket recoded = this.RecodePacket(packet, true, true);

            this.contextMock.Verify(x => x.FireChannelRead(It.IsAny<ConnectPacket>()), Times.Once);
            Assert.Equal(packet.ClientId, recoded.ClientId);
            Assert.Equal(packet.CleanSession, recoded.CleanSession);
            Assert.Equal(packet.KeepAliveInSeconds, recoded.KeepAliveInSeconds);
            Assert.Equal(packet.HasUsername, recoded.HasUsername);
            if (packet.HasUsername)
            {
                Assert.Equal(packet.Username, recoded.Username);
            }
            Assert.Equal(packet.HasPassword, recoded.HasPassword);
            if (packet.HasPassword)
            {
                Assert.Equal(packet.Password, recoded.Password);
            }
            if (packet.HasWill)
            {
                Assert.Equal(packet.WillTopicName, recoded.WillTopicName);
                Assert.True(ByteBufferUtil.Equals(Unpooled.WrappedBuffer(willMessage), recoded.WillMessage));
                Assert.Equal(packet.WillQualityOfService, recoded.WillQualityOfService);
                Assert.Equal(packet.WillRetain, recoded.WillRetain);
            }
        }

        [Theory]
        [InlineData(false, ConnectReturnCode.Accepted)]
        [InlineData(true, ConnectReturnCode.Accepted)]
        [InlineData(false, ConnectReturnCode.RefusedUnacceptableProtocolVersion)]
        [InlineData(false, ConnectReturnCode.RefusedIdentifierRejected)]
        [InlineData(false, ConnectReturnCode.RefusedServerUnavailable)]
        [InlineData(false, ConnectReturnCode.RefusedBadUsernameOrPassword)]
        [InlineData(false, ConnectReturnCode.RefusedNotAuthorized)]
        public void TestConnAckMessage(bool sessionPresent, ConnectReturnCode returnCode)
        {
            var packet = new ConnAckPacket();
            packet.SessionPresent = sessionPresent;
            packet.ReturnCode = returnCode;

            ConnAckPacket recoded = this.RecodePacket(packet, false, true);

            this.contextMock.Verify(x => x.FireChannelRead(It.IsAny<ConnAckPacket>()), Times.Once);
            Assert.Equal(packet.SessionPresent, recoded.SessionPresent);
            Assert.Equal(packet.ReturnCode, recoded.ReturnCode);
        }

        [Theory]
        [InlineData(1, new[] { "+", "+/+", "//", "/#", "+//+" }, new[] { QualityOfService.ExactlyOnce, QualityOfService.AtLeastOnce, QualityOfService.AtMostOnce, QualityOfService.ExactlyOnce, QualityOfService.AtMostOnce })]
        [InlineData(ushort.MaxValue, new[] { "a" }, new[] { QualityOfService.AtLeastOnce })]
        public void TestSubscribeMessage(int packetId, string[] topicFilters, QualityOfService[] requestedQosValues)
        {
            var packet = new SubscribePacket(packetId, topicFilters.Zip(requestedQosValues, (topic, qos) => new SubscriptionRequest(topic, qos)).ToArray());

            SubscribePacket recoded = this.RecodePacket(packet, true, true);

            this.contextMock.Verify(x => x.FireChannelRead(It.IsAny<SubscribePacket>()), Times.Once);
            Assert.Equal(packet.Requests, recoded.Requests, EqualityComparer<SubscriptionRequest>.Default);
            Assert.Equal(packet.PacketId, recoded.PacketId);
        }

        [Theory]
        [InlineData(1, new[] { QualityOfService.ExactlyOnce, QualityOfService.AtLeastOnce, QualityOfService.AtMostOnce, QualityOfService.Failure, QualityOfService.AtMostOnce })]
        [InlineData(ushort.MaxValue, new[] { QualityOfService.AtLeastOnce })]
        public void TestSubAckMessage(int packetId, QualityOfService[] qosValues)
        {
            var packet = new SubAckPacket();
            packet.PacketId = packetId;
            packet.ReturnCodes = qosValues;

            SubAckPacket recoded = this.RecodePacket(packet, false, true);

            this.contextMock.Verify(x => x.FireChannelRead(It.IsAny<SubAckPacket>()), Times.Once);
            Assert.Equal(packet.ReturnCodes, recoded.ReturnCodes);
            Assert.Equal(packet.PacketId, recoded.PacketId);
        }

        [Theory]
        [InlineData(1, new[] { "+", "+/+", "//", "/#", "+//+" })]
        [InlineData(ushort.MaxValue, new[] { "a" })]
        public void TestUnsubscribeMessage(int packetId, string[] topicFilters)
        {
            var packet = new UnsubscribePacket(packetId, topicFilters);

            UnsubscribePacket recoded = this.RecodePacket(packet, true, true);

            this.contextMock.Verify(x => x.FireChannelRead(It.IsAny<UnsubscribePacket>()), Times.Once);
            Assert.Equal(packet.TopicFilters, recoded.TopicFilters);
            Assert.Equal(packet.PacketId, recoded.PacketId);
        }

        [Theory]
        [InlineData(QualityOfService.AtMostOnce, false, false, 1, "a", null)]
        [InlineData(QualityOfService.ExactlyOnce, true, false, ushort.MaxValue, "/", new byte[0])]
        [InlineData(QualityOfService.AtLeastOnce, false, true, 129, "a/b", new byte[] { 1, 2, 3 })]
        [InlineData(QualityOfService.ExactlyOnce, true, true, ushort.MaxValue - 1, "topic/name/that/is/longer/than/256/characters/topic/name/that/is/longer/than/256/characters/topic/name/that/is/longer/than/256/characters/topic/name/that/is/longer/than/256/characters/topic/name/that/is/longer/than/256/characters/topic/name/that/is/longer/than/256/characters/", new byte[] { 1 })]
        public void TestPublishMessage(QualityOfService qos, bool dup, bool retain, int packetId, string topicName, byte[] payload)
        {
            var packet = new PublishPacket(qos, dup, retain);
            packet.TopicName = topicName;
            if (qos > QualityOfService.AtMostOnce)
            {
                packet.PacketId = packetId;
            }
            packet.Payload = payload == null ? null : Unpooled.WrappedBuffer(payload);

            PublishPacket recoded = this.RecodePacket(packet, false, true);

            this.contextMock.Verify(x => x.FireChannelRead(It.IsAny<PublishPacket>()), Times.Once);
            Assert.Equal(packet.TopicName, recoded.TopicName);
            if (packet.QualityOfService > QualityOfService.AtMostOnce)
            {
                Assert.Equal(packet.PacketId, recoded.PacketId);
            }
            Assert.True(ByteBufferUtil.Equals(payload == null ? Unpooled.Empty : Unpooled.WrappedBuffer(payload), recoded.Payload));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(127)]
        [InlineData(128)]
        [InlineData(256)]
        [InlineData(257)]
        [InlineData(ushort.MaxValue)]
        public void TestPacketIdOnlyResponseMessages(int packetId)
        {
            this.TestPublishResponseMessage<PubAckPacket>(packetId, true);
            this.TestPublishResponseMessage<PubAckPacket>(packetId, false);
            this.TestPublishResponseMessage<PubRecPacket>(packetId, true);
            this.TestPublishResponseMessage<PubRecPacket>(packetId, false);
            this.TestPublishResponseMessage<PubRelPacket>(packetId, true);
            this.TestPublishResponseMessage<PubRelPacket>(packetId, false);
            this.TestPublishResponseMessage<PubCompPacket>(packetId, true);
            this.TestPublishResponseMessage<PubCompPacket>(packetId, false);
            this.TestPublishResponseMessage<UnsubAckPacket>(packetId, false);
        }

        void TestPublishResponseMessage<T>(int packetId, bool useServer)
            where T : PacketWithId, new()
        {
            var packet = new T
            {
                PacketId = packetId
            };

            T recoded = this.RecodePacket(packet, useServer, true);

            this.contextMock.Verify(x => x.FireChannelRead(It.IsAny<T>()), Times.Once);
            this.contextMock.ResetCalls();
            Assert.Equal(packet.PacketId, recoded.PacketId);
        }

        [Fact]
        public void TestEmptyPacketMessages()
        {
            this.TestEmptyPacketMessage(PingReqPacket.Instance, true);
            this.TestEmptyPacketMessage(PingRespPacket.Instance, false);
            this.TestEmptyPacketMessage(DisconnectPacket.Instance, true);
        }

        void TestEmptyPacketMessage<T>(T packet, bool useServer)
            where T : Packet
        {
            T recoded = this.RecodePacket(packet, useServer, true);

            this.contextMock.Verify(x => x.FireChannelRead(It.IsAny<T>()), Times.Once);
        }

        T RecodePacket<T>(T packet, bool useServer, bool explodeForDecode)
            where T : Packet
        {
            var output = new List<object>();
            MqttEncoder.DoEncode(Allocator, packet, output);

            T observedPacket = null;
            this.contextMock.Setup(x => x.FireChannelRead(It.IsAny<T>()))
                .Callback((object message) => observedPacket = Assert.IsAssignableFrom<T>(message))
                .Returns(this.contextMock.Object);

            foreach (IByteBuffer message in output)
            {
                MqttDecoder mqttDecoder = useServer ? this.serverDecoder : this.clientDecoder;
                if (explodeForDecode)
                {
                    while (message.IsReadable())
                    {
                        IByteBuffer finalBuffer = message.ReadBytes(1);
                        mqttDecoder.ChannelRead(this.contextMock.Object, finalBuffer);
                    }
                }
                else
                {
                    mqttDecoder.ChannelRead(this.contextMock.Object, message);
                }
            }
            return observedPacket;
        }
    }
}