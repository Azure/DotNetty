namespace DotNetty.Codecs.CoapTcp.Tests.blbt
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Codecs.CoapTcp.blbt;
    using DotNetty.Codecs.CoapTcp.util;
    using Xunit;

    class BLBTL1CodecTests
    {
        private const int OPTION_COUNT = 3;
        private const int OPTION_PAYLOAD_SIZE = 16;
        private const int PAYLOAD_SIZE = 128;
        private const byte DEFAULT_VERSION = 1;
        private const byte DEFAULT_TYPE = 1;

        [Fact]
        public void EncodeTest()
        {
            byte code = 0x42;
            byte[] token = new byte[4] { 0x0E, 0xE0, 0xAB, 0xCD };
            List<MessageOption> options = GetTestOptions();
            byte[] payload = GetTestPayload();

            Message actualMsg = CreateMessage(code, token, options, payload);
            byte[] actualMsgBytes = GetTestCodec().Encode(actualMsg);

            // validate msg size (which is composed of headers, options + payload
            int expectedOptionBytesSize = 
                MessageOptionTestHelper.estimateOptionByteSize(0, OPTION_PAYLOAD_SIZE) * 
                options.Count + 1;

            int expectedMsgSize = 4 + 1 + 1 + token.Length + expectedOptionBytesSize + PAYLOAD_SIZE;
            Assert.Equal(expectedMsgSize, actualMsgBytes.Length);

            // validate length shim
            int lengthShim = BytesUtil.ToInt(actualMsgBytes, 4, IntegerEncoding.NETWORK_ORDER);
            Assert.Equal(expectedMsgSize - 4, lengthShim);

            // validate ver, t and tkl
            byte expectedMeta = 0x45;
            Assert.Equal(expectedMeta, actualMsgBytes[4]);

            // validate code and token
            Assert.Equal(code, actualMsgBytes[5]);
            Assert.Equal(token[0], actualMsgBytes[6]);
            Assert.Equal(token[1], actualMsgBytes[7]);
            Assert.Equal(token[2], actualMsgBytes[8]);
            Assert.Equal(token[3], actualMsgBytes[9]);

            // skip validate the options but its termination
            Assert.Equal(MessageOption.END_OF_OPTIONS, actualMsgBytes[10 + (2 + OPTION_PAYLOAD_SIZE) * OPTION_COUNT]);

            // validate payload
            int payloadOffset = 4 + 1 + 1 + token.Length + expectedOptionBytesSize;
            byte[] actualPayload = new byte[payload.Length];
            Array.Copy(actualMsgBytes, payloadOffset, actualPayload, 0, payload.Length);
            Assert.Equal(payload, actualPayload);
        }

        [Fact]
        public void DecodeSmallestMessageTest()
        {
            byte[] smallestValidMessage = { 0x00, 0x00, 0x00, 0x03, 0x05, 0x00, 0xFF };

            Message testMsg = GetTestCodec().Decode(smallestValidMessage);

            Assert.Equal(0, testMsg.Code);
            Assert.Equal(1, testMsg.Version);
            Assert.Equal(1, testMsg.Type);
            Assert.Equal(new byte[0], testMsg.Token);
            Assert.Equal(new byte[0], testMsg.Payload);
        }

        [Fact]
        public void DecodeMediumMessageTest()
        {
            byte meta = 0x15;
            byte code = 0x01;
            byte token = 0xAA;
            byte[] smallestValidMessage = { 0x00, 0x00, 0x00, 0x08, meta, code, token, 0x10, 0xEE, 0xFF, 0xAB, 0xCD };

            Message testMsg = GetTestCodec().Decode(smallestValidMessage);

            Assert.Equal(DEFAULT_VERSION, testMsg.Version);
            Assert.Equal(DEFAULT_TYPE, testMsg.Type);
            Assert.Equal(code, testMsg.Code);
            Assert.Equal(new byte[] { token }, testMsg.Token);
            Assert.Equal(new byte[] { 0xAB, 0xCD }, testMsg.Payload);
        }

        [Fact]
        public void EncodeDecodeTest()
        {
            byte code = 0x42;
            byte[] token = new byte[4] { 0x0E, 0xE0, 0xAB, 0xCD };
            List<MessageOption> options = GetTestOptions();
            byte[] payload = GetTestPayload();

            Message originalMessage = CreateMessage(code, token, options, payload);

            byte[] bytes = GetTestCodec().Encode(originalMessage);
            Message recoveredMessage = GetTestCodec().Decode(bytes);

            Assert.Equal(true, originalMessage.Equals(recoveredMessage));
        }

        // supporting methods below

        private List<MessageOption> GetTestOptions(int optionCount = OPTION_COUNT, int payloadSize = OPTION_PAYLOAD_SIZE)
        {
            List<MessageOption> options = new List<MessageOption>();
            for (int i = 0; i < optionCount; i++)
            {
                options.Add(MessageOption.Create(i, payloadSize, GetTestPayload(payloadSize)));
            }
            return options;
        }

        private byte[] GetTestPayload(int payloadSize = PAYLOAD_SIZE)
        {
            byte[] payload = new byte[payloadSize];
            for (int i = 0; i < payloadSize; i++)
            {
                payload[i] = (byte)i;
            }
            return payload;
        }

        private BLBTMessage CreateMessage(byte code, byte[] token, List<MessageOption> options, byte[] payload)
        {
            return BLBTMessage.Create(code, token, options, payload);
        }

        private BLBTL1Codec GetTestCodec()
        {
            return new BLBTL1Codec();
        }
    }
}
