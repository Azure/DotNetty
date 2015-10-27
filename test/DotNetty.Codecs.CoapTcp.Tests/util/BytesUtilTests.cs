namespace DotNetty.Codecs.CoapTcp.Tests.util
{
    using System.Text;
    using DotNetty.Codecs.CoapTcp.util;
    using Xunit;

    public class BytesUtilTests
    {
        [Fact]
        public void FourBytesToIntTest()
        {
            byte[] bytes = new byte[4];
            bytes[0] = 0x01;
            bytes[1] = 0x04;
            bytes[2] = 0x12;
            bytes[3] = 0xFF;

            int result = BytesUtil.ToInt(bytes, 4, IntegerEncoding.NETWORK_ORDER);
            Assert.Equal(result, 0x010412FF);
        }

        [Fact]
        public void TwoBytesToIntTest()
        {
            byte[] bytes = new byte[2];
            bytes[0] = 0x12;
            bytes[1] = 0xF1;

            int result = BytesUtil.ToInt(bytes, 2, IntegerEncoding.NETWORK_ORDER);
            Assert.Equal(result, 0x000012F1);
        }

        [Fact]
        public void OneByteToIntTest()
        {
            byte[] bytes = new byte[1];
            bytes[0] = 0x4F;

            int result = BytesUtil.ToInt(bytes, 1, IntegerEncoding.NETWORK_ORDER);
            Assert.Equal(result, 0x0000004F);
        }

        [Fact]
        public void ToUTF8StringTest()
        {
            byte[] bytes = new byte[6];
            bytes[0] = (byte)'T';
            bytes[1] = (byte)'e';
            bytes[2] = (byte)'s';
            bytes[3] = (byte)'t';
            bytes[4] = (byte)'!';
            bytes[5] = (byte)0x00;

            string result = BytesUtil.ToUTF8String(bytes, 6);

            Assert.Equal(result, "Test!\0");
        }

        [Fact]
        public void ToStringTest()
        {
            byte[] bytes = new byte[6];
            bytes[0] = (byte)'T';
            bytes[1] = (byte)'e';
            bytes[2] = (byte)'s';
            bytes[3] = (byte)'t';
            bytes[4] = (byte)'!';
            bytes[5] = (byte)0x00;

            string result = BytesUtil.ToString(bytes, 6, Encoding.UTF8);

            Assert.Equal(result, "Test!\0");
        }
    }
}
