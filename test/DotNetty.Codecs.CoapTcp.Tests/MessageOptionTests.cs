namespace DotNetty.Codecs.CoapTcp.Tests
{
    using System.Collections.Generic;
    using DotNetty.Codecs.CoapTcp.util;
    using Xunit;

    public class MessageOptionTests
    {
        [Theory]
        [InlineData(1,100)]
        public void OptionNameTest(int start, int end)
        {
            for (int optionNumber = start; optionNumber <= end; optionNumber++)
            {
                MessageOption.Name name = MessageOption.GetName(optionNumber);
                if (0 != name)
                {
                    Assert.Equal(optionNumber, (int)name);
                }
            }
        }

        [Theory]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(12)]
        [InlineData(14)]
        [InlineData(17)]
        [InlineData(60)]
        public void UintOptionDataTypeTest(int optionNumber)
        {
            MessageOption.DataType type = MessageOption.GetType(optionNumber);
            Assert.Equal(type, MessageOption.DataType.UINT);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(8)]
        [InlineData(11)]
        [InlineData(15)]
        [InlineData(20)]
        [InlineData(35)]
        [InlineData(39)]
        public void StringOptionDataTypeTest(int optionNumber)
        {
            MessageOption.DataType type = MessageOption.GetType(optionNumber);
            Assert.Equal(type, MessageOption.DataType.STRING);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        public void OpaqueOptionDataTypeTest(int optionNumber)
        {
            MessageOption.DataType type = MessageOption.GetType(optionNumber);
            Assert.Equal(type, MessageOption.DataType.OPAQUE);
        }

        [Theory]
        [InlineData(5)]
        public void EmptyOptionDataTypeTest(int optionNumber)
        {
            MessageOption.DataType type = MessageOption.GetType(optionNumber);
            Assert.Equal(type, MessageOption.DataType.EMPTY);
        }
    }
}
