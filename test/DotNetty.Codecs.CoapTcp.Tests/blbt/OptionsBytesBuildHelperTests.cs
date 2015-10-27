namespace DotNetty.Codecs.CoapTcp.Tests.blbt
{
    using DotNetty.Codecs.CoapTcp.blbt;
    using DotNetty.Codecs.CoapTcp.util;
    using Xunit;
    
    public class OptionsBytesBuildHelperTests
    {
        private byte OPTION_TERMINATION = 0xFF;

        [Fact]
        public void ZeroOptionTest()
        {
            BytesBuilder builder = BytesBuilder.Create();
            byte[] bytes = MessageOptionHelper.GetOptionBytesBuildHelper().build(new MessageOption[] {}, builder).Build();

            Assert.Equal(new byte[1] { OPTION_TERMINATION }, bytes);
        }

        [Fact]
        public void OneOptionTest()
        {
            MessageOption option = MessageOption.Create(0, 0, new byte[0]);

            BytesBuilder builder = BytesBuilder.Create();
            byte[] bytes = MessageOptionHelper.GetOptionBytesBuildHelper().build(new MessageOption[] { option }, builder).Build();

            Assert.Equal(new byte[2] { 0, OPTION_TERMINATION }, bytes);
        }

        [Fact]
        public void SmallDeltasZeroPayloadTest()
        {
            MessageOption option00 = MessageOption.Create(0, 0, new byte[0]);
            MessageOption option12a = MessageOption.Create(7, 0, new byte[0]);
            MessageOption option12b = MessageOption.Create(12, 0, new byte[0]);
            MessageOption option13 = MessageOption.Create(13, 0, new byte[0]);
            MessageOption option15 = MessageOption.Create(15, 0, new byte[0]);

            MessageOption[] options = new MessageOption[5] { option00, option12a, option12b, option13, option15 };

            BytesBuilder builder = BytesBuilder.Create();
            byte[] bytes = MessageOptionHelper.GetOptionBytesBuildHelper().build(options, builder).Build();

            Assert.Equal(new byte[] { 0, 0x7, 0x5, 0x1, 0x2, OPTION_TERMINATION }, bytes);
        }

        [Fact]
        public void BigDeltasZeroPayloadTest()
        {
            MessageOption option00 = MessageOption.Create(0, 0, new byte[0]);
            MessageOption option13 = MessageOption.Create(13, 0, new byte[0]);
            MessageOption option269 = MessageOption.Create(282, 0, new byte[0]);

            MessageOption[] options = new MessageOption[] { option00, option13, option269 };

            BytesBuilder builder = BytesBuilder.Create();
            byte[] bytes = MessageOptionHelper.GetOptionBytesBuildHelper().build(options, builder).Build();

            Assert.Equal(new byte[] { 0, 0x0d, 0x0, 0x0e, 0x0, 0x0, OPTION_TERMINATION }, bytes);
        }

        [Fact]
        public void PayloadTest()
        {
            MessageOption option00 = MessageOption.Create(0, 0, new byte[0]);
            MessageOption option13 = MessageOption.Create(13, 13, new byte[13]);
            MessageOption option269a = MessageOption.Create(282, 269, new byte[269]);
            MessageOption option269b = MessageOption.Create(282, 269, new byte[269]);

            MessageOption[] options = new MessageOption[] { option00, option13, option269a, option269b };

            BytesBuilder builder = BytesBuilder.Create();
            byte[] bytes = MessageOptionHelper.GetOptionBytesBuildHelper().build(options, builder).Build();

            // first option (0,0)
            Assert.Equal(0x00, bytes[0]);
            
            // second option (d,d)
            Assert.Equal(0xdd, bytes[1]);
            Assert.Equal(0x00, bytes[2]);
            Assert.Equal(0x00, bytes[3]);

            // third option (e,e)
            Assert.Equal(0xee, bytes[17]);
            Assert.Equal(0x00, bytes[18]);
            Assert.Equal(0x00, bytes[19]);
            Assert.Equal(0x00, bytes[20]);
            Assert.Equal(0x00, bytes[21]);

            // fourth option
            Assert.Equal(0xe0, bytes[291]);
            Assert.Equal(0x00, bytes[292]);
            Assert.Equal(0x00, bytes[293]);

            // end
            Assert.Equal(OPTION_TERMINATION, bytes[563]);
        }
    }
}
