// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using Xunit;

    public class DuplicatedByteBufferTests : AbstractByteBufferTests
    {
        protected override IByteBuffer NewBuffer(int length, int maxCapacity)
        {
            IByteBuffer wrapped = Unpooled.Buffer(length, maxCapacity);
            IByteBuffer buffer = new UnpooledDuplicatedByteBuffer((AbstractByteBuffer)wrapped);
            Assert.Equal(wrapped.WriterIndex, buffer.WriterIndex);
            Assert.Equal(wrapped.ReaderIndex, buffer.ReaderIndex);
            return buffer;
        }

        // See https://github.com/netty/netty/issues/1800
        [Fact]
        public void IncreaseCapacityWrapped()
        {
            IByteBuffer buffer = this.NewBuffer(8);
            IByteBuffer wrapped = buffer.Unwrap();
            wrapped.WriteByte(0);
            wrapped.SetReaderIndex(wrapped.ReaderIndex + 1);
            buffer.SetWriterIndex(buffer.WriterIndex + 1);
            wrapped.AdjustCapacity(wrapped.Capacity * 2);

            Assert.Equal((byte)0, buffer.ReadByte());
        }

        [Fact]
        public void MarksInitialized()
        {
            IByteBuffer wrapped = Unpooled.Buffer(8);
            try
            {
                wrapped.SetWriterIndex(6);
                wrapped.SetReaderIndex(1);
                IByteBuffer duplicate = new UnpooledDuplicatedByteBuffer((AbstractByteBuffer)wrapped);

                // Test writer mark
                duplicate.SetWriterIndex(duplicate.WriterIndex + 1);
                duplicate.ResetWriterIndex();
                Assert.Equal(wrapped.WriterIndex, duplicate.WriterIndex);

                // Test reader mark
                duplicate.SetReaderIndex(duplicate.ReaderIndex + 1);
                duplicate.ResetReaderIndex();
                Assert.Equal(wrapped.ReaderIndex, duplicate.ReaderIndex);
            }
            finally
            {
                wrapped.Release();
            }
        }
    }
}
