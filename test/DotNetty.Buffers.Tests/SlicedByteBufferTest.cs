// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using DotNetty.Common.Internal;
    using Xunit;

    public class SlicedByteBufferTest : AbstractByteBufferTests
    {
        protected sealed override IByteBuffer NewBuffer(int length, int maxCapacity)
        {
            this.AssumedMaxCapacity = maxCapacity == int.MaxValue;

            int offset = length == 0 ? 0 : PlatformDependent.GetThreadLocalRandom().Next(length);
            IByteBuffer buffer = Unpooled.Buffer(length * 2);
            IByteBuffer slice = this.NewSlice(buffer, offset, length);
            Assert.Equal(0, slice.ReaderIndex);
            Assert.Equal(length, slice.WriterIndex);

            return slice;
        }

        protected virtual IByteBuffer NewSlice(IByteBuffer buffer, int offset, int length) => buffer.Slice(offset, length);

        [Fact]
        public override void ReadBytes()
        {
            // ignore for SlicedByteBuf
        }

        [Fact]
        public override void ForEachByteDesc2()
        {
            // Ignore for SlicedByteBuf
        }

        [Fact]
        public override void ForEachByte2()
        {
            // Ignore for SlicedByteBuf
        }

        [Fact]
        public override void DuplicateCapacityChange()
        {
            // Sliced ByteBuf objects don't allow the capacity to change. So this test would fail and shouldn't be run
        }

        [Fact]
        public override void RetainedDuplicateCapacityChange()
        {
            // Sliced ByteBuf objects don't allow the capacity to change. So this test would fail and shouldn't be run
        }

        [Fact]
        public void ReaderIndexAndMarks()
        {
            IByteBuffer wrapped = Unpooled.Buffer(16);
            try
            {
                wrapped.SetWriterIndex(14);
                wrapped.SetReaderIndex(2);
                wrapped.MarkWriterIndex();
                wrapped.MarkReaderIndex();
                IByteBuffer slice = wrapped.Slice(4, 4);
                Assert.Equal(0, slice.ReaderIndex);
                Assert.Equal(4, slice.WriterIndex);

                slice.SetReaderIndex(slice.ReaderIndex + 1);
                slice.ResetReaderIndex();
                Assert.Equal(0, slice.ReaderIndex);

                slice.SetWriterIndex(slice.WriterIndex - 1);
                slice.ResetWriterIndex();
                Assert.Equal(0, slice.WriterIndex);
            }
            finally
            {
                wrapped.Release();
            }
        }

        [Fact]
        public void SliceEmptyNotLeak()
        {
            var buffer = (IByteBuffer)Unpooled.Buffer(8).Retain();
            Assert.Equal(2, buffer.ReferenceCount);

            IByteBuffer slice1 = buffer.Slice();
            Assert.Equal(2, slice1.ReferenceCount);

            IByteBuffer slice2 = slice1.Slice();
            Assert.Equal(2, slice2.ReferenceCount);

            Assert.False(slice2.Release());
            Assert.Equal(1, buffer.ReferenceCount);
            Assert.Equal(1, slice1.ReferenceCount);
            Assert.Equal(1, slice2.ReferenceCount);

            Assert.True(slice2.Release());

            Assert.Equal(0, buffer.ReferenceCount);
            Assert.Equal(0, slice1.ReferenceCount);
            Assert.Equal(0, slice2.ReferenceCount);
        }

        [Fact]
        public override void WriteUsAsciiCharSequenceExpand() => Assert.Throws<IndexOutOfRangeException>(() => base.WriteUsAsciiCharSequenceExpand());

        [Fact]
        public override void WriteUtf8CharSequenceExpand() => Assert.Throws<IndexOutOfRangeException>(() => base.WriteUtf8CharSequenceExpand());

        [Fact]
        public override void WriteUtf16CharSequenceExpand() => Assert.Throws<IndexOutOfRangeException>(() => base.WriteUtf16CharSequenceExpand());

        [Fact]
        public void EnsureWritableWithEnoughSpaceShouldNotThrow()
        {
            IByteBuffer slice = this.NewBuffer(10);
            IByteBuffer unwrapped = slice.Unwrap();
            unwrapped.SetWriterIndex(unwrapped.WriterIndex + 5);
            slice.SetWriterIndex(slice.ReaderIndex);

            // Run ensureWritable and verify this doesn't change any indexes.
            int originalWriterIndex = slice.WriterIndex;
            int originalReadableBytes = slice.ReadableBytes;
            slice.EnsureWritable(originalWriterIndex - slice.WriterIndex);
            Assert.Equal(originalWriterIndex, slice.WriterIndex);
            Assert.Equal(originalReadableBytes, slice.ReadableBytes);
            slice.Release();
        }

        [Fact]
        public void EnsureWritableWithNotEnoughSpaceShouldThrow()
        {
            IByteBuffer slice = this.NewBuffer(10);
            IByteBuffer unwrapped = slice.Unwrap();
            unwrapped.SetWriterIndex(unwrapped.WriterIndex + 5);
            Assert.Throws<IndexOutOfRangeException>(() => slice.EnsureWritable(1));
            slice.Release();
        }
    }
}
