using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNetty.Codecs.CoapTcp.blbt
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Codecs.CoapTcp.util;

    class MessageOptionHelper
    {
        public static IBytesBuildHelper<MessageOption> GetOptionBytesBuildHelper()
        {
            return new OptionBytesBuildHelper();
        }

        /// <summary>
        /// ReadOptions reads through a sequence of bytes (through BytesReader)
        /// and constructs a list of options.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static List<MessageOption> ReadOptions(BytesReader reader)
        {
            // read options
            List<MessageOption> options = new List<MessageOption>();
            int currentOptionNumber = 0;
            while (true)
            {
                byte optionHeaderCode = reader.ReadByte();
                if (optionHeaderCode == MessageOption.END_OF_OPTIONS)
                {
                    break;
                }

                // retrieve option number
                byte deltaCode = (byte)(optionHeaderCode & 0xF);
                int numberOfExtraBytesForDelta = VariableLengthIntegerCodec.ExtraBytesForFourBitCode(deltaCode);
                int delta = numberOfExtraBytesForDelta == 0 ? deltaCode : reader.ReadInt(numberOfExtraBytesForDelta, IntegerEncoding.NETWORK_ORDER);
                int number = delta + VariableLengthIntegerCodec.OffsetForFourBitCode(deltaCode) + currentOptionNumber;

                // retrieve option payload length
                byte lengthCode = (byte)(optionHeaderCode >> 4);
                int numberOfExtraBytesForLength = VariableLengthIntegerCodec.ExtraBytesForFourBitCode(lengthCode);
                int length = numberOfExtraBytesForLength == 0 ? lengthCode : reader.ReadInt(numberOfExtraBytesForLength, IntegerEncoding.NETWORK_ORDER);
                length = length + VariableLengthIntegerCodec.OffsetForFourBitCode(lengthCode);

                // retrieve payload
                byte[] payload = reader.ReadBytes(length);

                options.Add(MessageOption.Create(number, length, payload));
                currentOptionNumber = number;
            }
            return options;
        }

        /// <summary>
        /// OptionBytesBuildHelper implements IBytesBuildHelper<MessageOption> 
        /// to build bytes based on a collection of options
        /// </summary>
        private class OptionBytesBuildHelper : IBytesBuildHelper<MessageOption>
        {
            public OptionBytesBuildHelper()
            {
            }

            public BytesBuilder build(IEnumerable<MessageOption> options, BytesBuilder builder)
            {
                int currentOptionNumber = 0;
                foreach (MessageOption option in options)
                {
                    build(option, currentOptionNumber, builder);
                    currentOptionNumber = option.OptionNumber;
                }
                builder.AddByte(MessageOption.END_OF_OPTIONS);
                return builder;
            }

            private BytesBuilder build(MessageOption option, int currentOptionNumber, BytesBuilder builder)
            {
                Tuple<byte, int, int> encodedDelta = VariableLengthIntegerCodec.Encode(option.OptionNumber - currentOptionNumber);
                Tuple<byte, int, int> encodedLength = VariableLengthIntegerCodec.Encode(option.OptionLength);

                // the first byte is composed of 4-bit delta
                byte optionHeader = (byte)(encodedDelta.Item1 + (encodedLength.Item1 << 4));

                builder.AddByte(optionHeader)
                    .AddInt(encodedDelta.Item2, encodedDelta.Item3, IntegerEncoding.NETWORK_ORDER)
                    .AddInt(encodedLength.Item2, encodedLength.Item3, IntegerEncoding.NETWORK_ORDER)
                    .AddBytes(option.Payload, option.Payload.Length);

                return builder;
            }
        }
    }
}
