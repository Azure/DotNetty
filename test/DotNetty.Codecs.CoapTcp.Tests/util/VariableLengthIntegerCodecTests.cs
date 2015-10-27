namespace DotNetty.Codecs.CoapTcp.Tests.util
{
    using System;
    using DotNetty.Codecs.CoapTcp.util;
    using Xunit;

    public class VariableLengthIntegerCodecTests
    {
        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 0)]
        [InlineData(2, 0)]
        [InlineData(3, 0)]
        [InlineData(4, 0)]
        [InlineData(5, 0)]
        [InlineData(6, 0)]
        [InlineData(7, 0)]
        [InlineData(8, 0)]
        [InlineData(9, 0)]
        [InlineData(10, 0)]
        [InlineData(11, 0)]
        [InlineData(12, 0)]
        [InlineData(13, 1)]
        [InlineData(14, 2)]
        [InlineData(15, 4)]
        public void ExtraNumberOfBytesForCodeTest(byte code, int numberOfBytes)
        {
            int actual = VariableLengthIntegerCodec.ExtraBytesForFourBitCode(code);
            Assert.Equal(numberOfBytes, actual);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 0)]
        [InlineData(2, 0)]
        [InlineData(3, 0)]
        [InlineData(4, 0)]
        [InlineData(5, 0)]
        [InlineData(6, 0)]
        [InlineData(7, 0)]
        [InlineData(8, 0)]
        [InlineData(9, 0)]
        [InlineData(10, 0)]
        [InlineData(11, 0)]
        [InlineData(12, 0)]
        [InlineData(13, 13)]
        [InlineData(14, 269)]
        [InlineData(15, 65805)]
        public void OffsetForFourBitCodeTest(byte code, int offset)
        {
            int actual = VariableLengthIntegerCodec.OffsetForFourBitCode(code);
            Assert.Equal(offset, actual);
        }

        [Theory]
        [InlineData(0, 0, 0, 0)]
        [InlineData(12, 12, 0, 0)]
        [InlineData(13, 13, 0, 1)]
        [InlineData(14, 13, 1, 1)]
        [InlineData(100, 13, 87, 1)]
        [InlineData(268, 13, 255, 1)]
        [InlineData(269, 14, 0, 2)]
        [InlineData(270, 14, 1, 2)]
        [InlineData(1000, 14, 731, 2)]
        [InlineData(65803, 14, 65534, 2)]
        [InlineData(65804, 14, 65535, 2)]
        [InlineData(65805, 15, 0, 4)]
        [InlineData(65806, 15, 1, 4)]
        [InlineData(100000, 15, 34195, 4)]
        public void EncodeTest(int intValue, byte code, int value, int numberOfExtraBytes)
        {
            Tuple<byte, int, int> actual = VariableLengthIntegerCodec.Encode(intValue);
            Assert.Equal(code, actual.Item1);
            Assert.Equal(value, actual.Item2);
            Assert.Equal(numberOfExtraBytes, actual.Item3);
        }
    }
}
