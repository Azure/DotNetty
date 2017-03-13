// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Tests.Protobuf
{
    using System.Collections.Generic;
    using System.Linq;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Protobuf;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class ProtobufVarint32LengthFieldPrependerTests
    {
        [Theory]
        [InlineData(1, 10)]
        [InlineData(2, 266)]
        [InlineData(3, 0x4000)]
        [InlineData(4, 0x200000)]
        public void ComputeRawVriant32Size(int expectedSize, int number)
        {
            int result = ProtobufVarint32LengthFieldPrepender.ComputeRawVarint32Size(number);
            Assert.Equal(expectedSize, result);
        }

        static IEnumerable<object[]> GetVarint32Data()
        {
            // Size1Varint
            int size = 1;
            int number = 10;

            var data = new byte[size + number];
            //0000 1010
            data[0] = 0x0A;
            for (int i = size; i < number + size; ++i)
            {
                data[i] = 1;
            }

            yield return new object[]
            {
                size,
                data
            };

            // Size2Varint
            size = 2;
            number = 266;

            data = new byte[size + number];
            /**
             * 8    A    0    2
             * 1000 1010 0000 0010
             * 0000 1010 0000 0010
             * 0000 0010 0000 1010
             *  000 0010  000 1010
             *
             *  0000 0001 0000 1010
             *  0    1    0    A
             * 266
             */

            data[0] = 0x8A & 0xFF;
            data[1] = 0x02;
            for (int i = size; i < number + size; ++i)
            {
                data[i] = 1;
            }
            yield return new object[]
            {
                size,
                data
            };

            // Size3Varint
            size = 3;
            number = 0x4000;

            data = new byte[size + number];
            /**
             * 8    0    8    0    0    1
             * 1000 0000 1000 0000 0000 0001
             * 0000 0000 0000 0000 0000 0001
             * 0000 0001 0000 0000 0000 0000
             *  000 0001  000 0000  000 0000
             *
             *    0 0000 0100 0000 0000 0000
             *    0    0    4    0    0    0
             *
             */

            data[0] = 0x80 & 0xFF;
            data[1] = 0x80 & 0xFF;
            data[2] = 0x01;
            for (int i = size; i < number + size; ++i)
            {
                data[i] = 1;
            }
            yield return new object[]
            {
                size,
                data
            };

            // Size4Varint
            size = 4;
            number = 0x200000;

            data = new byte[size + number];
            /**
             * 8    0    8    0    8    0    0    1
             * 1000 0000 1000 0000 1000 0000 0000 0001
             * 0000 0000 0000 0000 0000 0000 0000 0001
             * 0000 0001 0000 0000 0000 0000 0000 0000
             *  000 0001  000 0000  000 0000  000 0000
             *
             *    0000 0010 0000 0000 0000 0000 0000
             *    0    2    0    0    0    0    0
             *
             */

            data[0] = 0x80 & 0xFF;
            data[1] = 0x80 & 0xFF;
            data[2] = 0x80 & 0xFF;
            data[3] = 0x01;
            for (int i = size; i < number + size; ++i)
            {
                data[i] = 1;
            }
            yield return new object[]
            {
                size,
                data
            };

            // Tiny
            size = 1;
            data = new byte[] { 4, 1, 1, 1, 1 };
            yield return new object[]
            {
                size,
                data
            };

            // Regular
            size = 2;
            data = new byte[2048];
            for (int i = 2; i < 2048; i++)
            {
                data[i] = 1;
            }
            data[0] = -2 + 256;
            data[1] = 15;
            yield return new object[]
            {
                size,
                data
            };
        }

        // aliasing actual test cases as indexes to mitigate xunit discovery issues

        static readonly object[][] EncodeVarint32SizeCases = GetVarint32Data().ToArray();

        static IEnumerable<object[]> GetVarint32DataAliases()
        {
            return Enumerable.Range(0, EncodeVarint32SizeCases.Length).Select(i => new object[] { i });
        }

        [Theory]
        //[MemberData(nameof(GetVarint32Data))]
        [MemberData(nameof(GetVarint32DataAliases))]
        public void EncodeVarint32Size(int index) // int size, byte[] data)
        {
            int size = (int)EncodeVarint32SizeCases[index][0];
            byte[] data = (byte[])EncodeVarint32SizeCases[index][1];
            IByteBuffer written = null;
            try
            {
                var channel = new EmbeddedChannel(new ProtobufVarint32LengthFieldPrepender());
                IByteBuffer content = Unpooled.WrappedBuffer(data, size, data.Length - size);
                Assert.True(channel.WriteOutbound(content));
                written = channel.ReadOutbound<IByteBuffer>();
                Assert.NotNull(written);
                byte[] output = TestUtil.GetReadableBytes(written);

                Assert.Equal(data.Length, output.Length);
                Assert.True(output.SequenceEqual(data));

                Assert.False(channel.Finish());
            }
            finally
            {
                written?.Release();
            }
        }
    }
}
