// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using Xunit;

    public abstract class AbstractPooledByteBufTest : AbstractByteBufTest
    {
        protected abstract IByteBuffer Alloc(int length);

        protected override IByteBuffer NewBuffer(int length)
        {
            IByteBuffer buffer = this.Alloc(length);
            Assert.Equal(0, buffer.WriterIndex);
            Assert.Equal(0, buffer.ReaderIndex);
            return buffer;
        }

        [Fact]
        public void TestDiscardMarks() => this.TestDiscardMarks(4);

        [Fact]
        public void TestDiscardMarksUnpooled() => this.TestDiscardMarks(32 * 1024 * 1024);

        void TestDiscardMarks(int capacity)
        {
            IByteBuffer buf = this.NewBuffer(capacity);
            buf.WriteShort(1);

            buf.SkipBytes(1);

            buf.MarkReaderIndex();
            buf.MarkWriterIndex();
            Assert.True(buf.Release());

            IByteBuffer buf2 = this.NewBuffer(capacity);

            Assert.Same(UnwrapIfNeeded(buf), UnwrapIfNeeded(buf2));

            buf2.WriteShort(1);

            buf2.ResetReaderIndex();
            buf2.ResetWriterIndex();

            Assert.Equal(0, buf2.ReaderIndex);
            Assert.Equal(0, buf2.WriterIndex);
            Assert.True(buf2.Release());
        }

        static IByteBuffer UnwrapIfNeeded(IByteBuffer buf)
        {
            if (buf is AdvancedLeakAwareByteBuffer || buf is SimpleLeakAwareByteBuffer)
            {
                return buf.Unwrap();
            }
            return buf;
        }
    }
}