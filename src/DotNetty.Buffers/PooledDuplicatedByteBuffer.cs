// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;

    sealed class PooledDuplicatedByteBuffer : AbstractPooledDerivedByteBuffer
    {
        static readonly ThreadLocalPool<PooledDuplicatedByteBuffer> Recycler = new ThreadLocalPool<PooledDuplicatedByteBuffer>(handle => new PooledDuplicatedByteBuffer(handle));

        internal static PooledDuplicatedByteBuffer NewInstance(AbstractByteBuffer unwrapped, IByteBuffer wrapped, int readerIndex, int writerIndex)
        {
            PooledDuplicatedByteBuffer duplicate = Recycler.Take();
            duplicate.Init<PooledDuplicatedByteBuffer>(unwrapped, wrapped, readerIndex, writerIndex, unwrapped.MaxCapacity);
            duplicate.MarkReaderIndex();
            duplicate.MarkWriterIndex();

            return duplicate;
        }

        public PooledDuplicatedByteBuffer(ThreadLocalPool.Handle recyclerHandle)
            : base(recyclerHandle)
        {
        }

        public override int Capacity => this.Unwrap().Capacity;

        public override IByteBuffer AdjustCapacity(int newCapacity)
        {
            this.Unwrap().AdjustCapacity(newCapacity);
            return this;
        }

        public override int ArrayOffset => this.Unwrap().ArrayOffset;

        public override ref byte GetPinnableMemoryAddress() => ref this.Unwrap().GetPinnableMemoryAddress();

        public override IntPtr AddressOfPinnedMemory() => this.Unwrap().AddressOfPinnedMemory();

        public override ArraySegment<byte> GetIoBuffer(int index, int length) => this.Unwrap().GetIoBuffer(index, length);

        public override ArraySegment<byte>[] GetIoBuffers(int index, int length) => this.Unwrap().GetIoBuffers(index, length);

        public override IByteBuffer Copy(int index, int length) => this.Unwrap().Copy(index, length);

        public override IByteBuffer RetainedSlice(int index, int length) => PooledSlicedByteBuffer.NewInstance(this.UnwrapCore(), this, index, length);

        public override IByteBuffer Duplicate() => this.Duplicate0().SetIndex(this.ReaderIndex, this.WriterIndex);

        public override IByteBuffer RetainedDuplicate() => NewInstance(this.UnwrapCore(), this, this.ReaderIndex, this.WriterIndex);

        protected internal override byte _GetByte(int index) => this.UnwrapCore()._GetByte(index);

        protected internal override short _GetShort(int index) => this.UnwrapCore()._GetShort(index);

        protected internal override short _GetShortLE(int index) => this.UnwrapCore()._GetShortLE(index);

        protected internal override int _GetUnsignedMedium(int index) => this.UnwrapCore()._GetUnsignedMedium(index);

        protected internal override int _GetUnsignedMediumLE(int index) => this.UnwrapCore()._GetUnsignedMediumLE(index);

        protected internal override int _GetInt(int index) => this.UnwrapCore()._GetInt(index);

        protected internal override int _GetIntLE(int index) => this.UnwrapCore()._GetIntLE(index);

        protected internal override long _GetLong(int index) => this.UnwrapCore()._GetLong(index);

        protected internal override long _GetLongLE(int index) => this.UnwrapCore()._GetLongLE(index);

        public override IByteBuffer GetBytes(int index, IByteBuffer destination, int dstIndex, int length) => this.Unwrap().GetBytes(index, destination, dstIndex, length);

        public override IByteBuffer GetBytes(int index, byte[] destination, int dstIndex, int length) => this.Unwrap().GetBytes(index, destination, dstIndex, length);

        public override IByteBuffer GetBytes(int index, Stream destination, int length) => this.Unwrap().GetBytes(index, destination, length);

        protected internal override void _SetByte(int index, int value) => this.UnwrapCore()._SetByte(index, value);

        protected internal override void _SetShort(int index, int value) => this.UnwrapCore()._SetShort(index, value);

        protected internal override void _SetShortLE(int index, int value) => this.UnwrapCore()._SetShortLE(index, value);

        protected internal override void _SetMedium(int index, int value) => this.UnwrapCore()._SetMedium(index, value);

        protected internal override void _SetMediumLE(int index, int value) => this.UnwrapCore()._SetMediumLE(index, value);

        public override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length) => this.Unwrap().SetBytes(index, src, srcIndex, length);

        public override Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken) => this.Unwrap().SetBytesAsync(index, src, length, cancellationToken);

        public override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length) => this.Unwrap().SetBytes(index, src, srcIndex, length);

        protected internal override void _SetInt(int index, int value) => this.UnwrapCore()._SetInt(index, value);

        protected internal override void _SetIntLE(int index, int value) => this.UnwrapCore()._SetIntLE(index, value);

        protected internal override void _SetLong(int index, long value) => this.UnwrapCore()._SetLong(index, value);

        protected internal override void _SetLongLE(int index, long value) => this.UnwrapCore()._SetLongLE(index, value);

    }
}