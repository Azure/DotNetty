namespace DotNetty.Codecs.CoapTcp.Tests.util
{
    using System.Collections.Generic;
    using DotNetty.Codecs.CoapTcp.util;
    using Xunit;

    public class BytesReaderTests
    {
        [Fact]
        public void SeqReadTest()
        {
            byte[] bytes = { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x88, 0xFF };
            BytesReader reader = BytesReader.Create(bytes, 0);

            byte first = reader.ReadByte();
            byte[] second = reader.ReadBytes(2);
            int third = reader.ReadInt(4, IntegerEncoding.NETWORK_ORDER);
            int fourth = reader.ReadInt(2, IntegerEncoding.NETWORK_ORDER);
            int index = reader.GetNumBytesRead();

            Assert.Equal(0x01, first);
            Assert.Equal(new byte[]{ 0x02, 0x03}, second);
            Assert.Equal(0x04050607, third);
            Assert.Equal(0x88FF, fourth);
            Assert.Equal(bytes.Length, index);
        }
    }
}
