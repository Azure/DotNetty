namespace DotNetty.Codecs.CoapTcp.Tests.blbt
{
    using System.Collections.Generic;
    using DotNetty.Codecs.CoapTcp.blbt;
    using Xunit;

    public class BLBTMessageTests
    {
        private const int OPTION_COUNT = 3;
        private const int OPTION_PAYLOAD_SIZE = 16;
        private const int PAYLOAD_SIZE = 128;

        [Fact]
        public void GetTest()
        {
            byte code = 0x40;
            byte[] token = new byte[2] { 0x0F, 0xF0 };
            List<MessageOption> options = GetTestOptions();
            byte[] payload = GetTestPayload();

            Message actualMsg = BLBTMessage.Create(code, token, options, payload);

            Assert.Equal(BLBTMessage.DEFAULT_VERSION, actualMsg.Version);
            Assert.Equal(BLBTMessage.DEFAULT_TYPE, actualMsg.Type);
            Assert.Equal(code, actualMsg.Code);
            Assert.Equal(token, actualMsg.Token);
            Assert.Equal(options, actualMsg.Options);
            Assert.Equal(payload, actualMsg.Payload);
        }

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
    }
}
