// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public sealed class AbstractReferenceCountedByteBufferTests
    {
        [Fact]
        public void RetainOverflow()
        {
            AbstractReferenceCountedByteBuffer referenceCounted = NewReferenceCounted();
            referenceCounted.SetReferenceCount(int.MaxValue);
            Assert.Equal(int.MaxValue, referenceCounted.ReferenceCount);
            Assert.Throws<IllegalReferenceCountException>(() => referenceCounted.Retain());
        }

        [Fact]
        public void RetainOverflow2()
        {
            AbstractReferenceCountedByteBuffer referenceCounted = NewReferenceCounted();
            Assert.Equal(1, referenceCounted.ReferenceCount);
            Assert.Throws<IllegalReferenceCountException>(() => referenceCounted.Retain(int.MaxValue));
        }

        [Fact]
        public void ReleaseOverflow()
        {
            AbstractReferenceCountedByteBuffer referenceCounted = NewReferenceCounted();
            referenceCounted.SetReferenceCount(0);
            Assert.Equal(0, referenceCounted.ReferenceCount);
            Assert.Throws<IllegalReferenceCountException>(() => referenceCounted.Release(int.MaxValue));
        }

        [Fact]
        public void RetainResurrect()
        {
            AbstractReferenceCountedByteBuffer referenceCounted = NewReferenceCounted();
            Assert.True(referenceCounted.Release());
            Assert.Equal(0, referenceCounted.ReferenceCount);
            Assert.Throws<IllegalReferenceCountException>(() => referenceCounted.Retain());
        }

        [Fact]
        public void RetainResurrect2()
        {
            AbstractReferenceCountedByteBuffer referenceCounted = NewReferenceCounted();
            Assert.True(referenceCounted.Release());
            Assert.Equal(0, referenceCounted.ReferenceCount);
            Assert.Throws<IllegalReferenceCountException>(() => referenceCounted.Retain(2));
        }

        static AbstractReferenceCountedByteBuffer NewReferenceCounted() => new ReferenceCountedByteBuffer();

        sealed class ReferenceCountedByteBuffer : AbstractReferenceCountedByteBuffer
        {
            public ReferenceCountedByteBuffer()
                : base(int.MaxValue)
            {
            }

            public override int Capacity => throw new NotSupportedException();

            public override IByteBuffer AdjustCapacity(int newCapacity) => throw new NotSupportedException();

            public override IByteBufferAllocator Allocator => throw new NotSupportedException();

            protected internal override byte _GetByte(int index) => throw new NotSupportedException();

            protected internal override short _GetShort(int index) => throw new NotSupportedException();

            protected internal override short _GetShortLE(int index) => throw new NotSupportedException();

            protected internal override int _GetUnsignedMedium(int index) => throw new NotSupportedException();

            protected internal override int _GetUnsignedMediumLE(int index) => throw new NotSupportedException();

            protected internal override int _GetInt(int index) => throw new NotSupportedException();

            protected internal override int _GetIntLE(int index) => throw new NotSupportedException();

            protected internal override long _GetLong(int index) => throw new NotSupportedException();

            protected internal override long _GetLongLE(int index) => throw new NotSupportedException();

            public override IByteBuffer GetBytes(int index, IByteBuffer destination, int dstIndex, int length) => throw new NotSupportedException();

            public override IByteBuffer GetBytes(int index, Stream destination, int length) => throw new NotSupportedException();

            protected internal override void _SetByte(int index, int value) => throw new NotSupportedException();

            protected internal override void _SetShort(int index, int value) => throw new NotSupportedException();

            protected internal override void _SetShortLE(int index, int value) => throw new NotSupportedException();

            protected internal override void _SetMedium(int index, int value) => throw new NotSupportedException();

            protected internal override void _SetMediumLE(int index, int value) => throw new NotSupportedException();

            protected internal override void _SetInt(int index, int value) => throw new NotSupportedException();

            protected internal override void _SetIntLE(int index, int value) => throw new NotSupportedException();

            protected internal override void _SetLong(int index, long value) => throw new NotSupportedException();

            protected internal override void _SetLongLE(int index, long value) => throw new NotSupportedException();

            public override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length) => throw new NotSupportedException();

            public override Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken) => throw new NotSupportedException();

            public override int IoBufferCount => throw new NotSupportedException();

            public override ArraySegment<byte> GetIoBuffer(int index, int length) => throw new NotSupportedException();

            public override ArraySegment<byte>[] GetIoBuffers(int index, int length) => throw new NotSupportedException();

            public override bool HasArray => throw new NotSupportedException();

            public override byte[] Array => throw new NotSupportedException();

            public override int ArrayOffset => throw new NotSupportedException();

            public override bool HasMemoryAddress => throw new NotSupportedException();

            public override IntPtr AddressOfPinnedMemory() => throw new NotSupportedException();

            public override ref byte GetPinnableMemoryAddress() => throw new NotSupportedException();

            public override IByteBuffer Unwrap() => throw new NotSupportedException();

            public override bool IsDirect => throw new NotSupportedException();

            public override IByteBuffer Copy(int index, int length) => throw new NotSupportedException();

            public override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length) => throw new NotSupportedException();

            public override IByteBuffer GetBytes(int index, byte[] destination, int dstIndex, int length) => throw new NotSupportedException();

            protected internal override void Deallocate()
            {
                // NOOP
            }
        }
    }
}
