namespace DotNetty.Codecs.CoapTcp.Tests.util
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Codecs.CoapTcp.util;
    using Xunit;

    public class BytesBuilderTests
    {
        [Theory]
        [InlineData(0, 0x00)]
        [InlineData(1, 0xFE)]
        [InlineData(5, 0x9A)]
        [InlineData(10, 0xF1)]
        public void SkipTest(int numberOfBytesSkipped, byte content)
        {
            byte[] actual = BytesBuilder.Create().Skip(numberOfBytesSkipped).AddByte(content).Build();
            Assert.Equal(content, actual[numberOfBytesSkipped]);
            Assert.Equal(numberOfBytesSkipped + 1, actual.Length);
        }

        [Theory]
        [InlineData(0, new byte[] { 0x00 })]
        [InlineData(254, new byte[] { 0xFE })]
        [InlineData(255, new byte[] { 0x00, 0xFF })]
        [InlineData(258, new byte[] { 0x01, 0x02 })]
        [InlineData(1024, new byte[] { 0x00, 0x00, 0x04, 0x00 })]
        [InlineData(180150009, new byte[] { 0x0A, 0xBC, 0xDE, 0xF9 })]
        public void AddIntTest(int value, params byte[] byteValues)
        {
            byte[] actual = BytesBuilder.Create().AddInt(value, byteValues.Length, IntegerEncoding.NETWORK_ORDER).Build();
            Assert.Equal(byteValues, actual);
        }

        [Theory]
        [InlineData(new byte[]{0x01, 0x02, 0x0F, 0xF1, 0xF2})]
        public void AddByteTest(params byte[] byteValues)
        {
            BytesBuilder testInstance = BytesBuilder.Create();
            for (int i=0; i< byteValues.Length; i++)
            {
                testInstance.AddByte(byteValues[i]);
            }
            byte[] actual = testInstance.Build();
            Assert.Equal(byteValues, actual);
        }

        [Fact]
        public void AddBytesTest()
        {
            byte[] expected = { 0x01, 0x02, 0x0F, 0xF1, 0xF2 };

            BytesBuilder testInstance = BytesBuilder.Create();
            testInstance.AddBytes(new byte[] { 0x01 });
            testInstance.AddBytes(new byte[] { 0x02, 0x0F }, 2);
            testInstance.AddBytes(new byte[] { 0xF1, 0xF2 });

            byte[] actual = testInstance.Build();
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void AddGenericsTest()
        {
            byte[] expected = {0xFA};
            byte[] actual = BytesBuilder.Create().Add(new int[] { 0xF, 0x9 }, new OverrideBytesBuildHelper(expected)).Build();
            Assert.Equal(actual, expected);
        }

        private class OverrideBytesBuildHelper : IBytesBuildHelper<int>
        {
            private byte[] bytes = null;
            public OverrideBytesBuildHelper(byte[] bytes) {
                this.bytes = bytes;
            }
            
            public BytesBuilder build(IEnumerable<int> v, BytesBuilder builder) 
            {
                return builder.AddBytes(bytes);
            }
        };

        [Fact]
        public void SelfExpansionTest()
        {
            BytesBuilder testInstance = BytesBuilder.Create(1, 2);
            testInstance.AddByte(0xAB);
            testInstance.AddBytes(new byte[10]);
            byte[] actual = testInstance.Build();

            Assert.Equal(0xAB, actual[0]);
            Assert.Equal(11, actual.Length);
        }
    }
}
