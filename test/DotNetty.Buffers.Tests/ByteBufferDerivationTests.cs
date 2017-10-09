// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using Xunit;

    public class ByteBufferDerivationTests
    {
        [Fact]
        public void Slice()
        {
            IByteBuffer buf = Unpooled.Buffer(8).SetIndex(1, 7);
            IByteBuffer slice = buf.Slice(1, 7);

            Assert.IsAssignableFrom<AbstractUnpooledSlicedByteBuffer>(slice);
            Assert.Same(slice.Unwrap(), buf);
            Assert.Equal(0, slice.ReaderIndex);
            Assert.Equal(7, slice.WriterIndex);
            Assert.Equal(7, slice.Capacity);
            Assert.Equal(7, slice.MaxCapacity);

            slice.SetIndex(1, 6);
            Assert.Equal(1, buf.ReaderIndex);
            Assert.Equal(7, buf.WriterIndex);
        }

        [Fact]
        public void SliceOfSlice()
        {
            IByteBuffer buf = Unpooled.Buffer(8);
            IByteBuffer slice = buf.Slice(1, 7);
            IByteBuffer slice2 = slice.Slice(0, 6);

            Assert.NotSame(slice2, slice);
            Assert.IsAssignableFrom<AbstractUnpooledSlicedByteBuffer>(slice2);
            Assert.Same(slice2.Unwrap(), buf);
            Assert.Equal(6, slice2.WriterIndex);
            Assert.Equal(6, slice2.Capacity);
        }

        [Fact]
        public void Duplicate()
        {
            IByteBuffer buf = Unpooled.Buffer(8).SetIndex(1, 7);
            IByteBuffer dup = buf.Duplicate();

            Assert.IsAssignableFrom<UnpooledDuplicatedByteBuffer>(dup);
            Assert.Same(dup.Unwrap(), buf);
            Assert.Equal(dup.ReaderIndex, buf.ReaderIndex);
            Assert.Equal(dup.WriterIndex, buf.WriterIndex);
            Assert.Equal(dup.Capacity, buf.Capacity);
            Assert.Equal(dup.MaxCapacity, buf.MaxCapacity);

            dup.SetIndex(2, 6);
            Assert.Equal(1, buf.ReaderIndex);
            Assert.Equal(7, buf.WriterIndex);
        }

        [Fact]
        public void DuplicateOfDuplicate()
        {
            IByteBuffer buf = Unpooled.Buffer(8).SetIndex(1, 7);
            IByteBuffer dup = buf.Duplicate().SetIndex(2, 6);
            IByteBuffer dup2 = dup.Duplicate();

            Assert.NotSame(dup2, dup);
            Assert.IsAssignableFrom<UnpooledDuplicatedByteBuffer>(dup2);
            Assert.Same(dup2.Unwrap(), buf);
            Assert.Equal(dup2.ReaderIndex, dup.ReaderIndex);
            Assert.Equal(dup2.WriterIndex, dup.WriterIndex);
            Assert.Equal(dup2.Capacity, dup.Capacity);
            Assert.Equal(dup2.MaxCapacity, dup.MaxCapacity);
        }
    }
}