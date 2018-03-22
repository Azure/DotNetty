// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Internal;

    sealed class PooledHeapByteBuffer : PooledByteBuffer<byte[]>
    {
        static readonly ThreadLocalPool<PooledHeapByteBuffer> Recycler = new ThreadLocalPool<PooledHeapByteBuffer>(handle => new PooledHeapByteBuffer(handle, 0));

        internal static PooledHeapByteBuffer NewInstance(int maxCapacity)
        {
            PooledHeapByteBuffer buf = Recycler.Take();
            buf.Reuse(maxCapacity);
            return buf;
        }

        internal PooledHeapByteBuffer(ThreadLocalPool.Handle recyclerHandle, int maxCapacity)
            : base(recyclerHandle, maxCapacity)
        {
        }

        public override bool IsDirect => false;

        protected internal override byte _GetByte(int index) => HeapByteBufferUtil.GetByte(this.Memory, this.Idx(index));

        protected internal override short _GetShort(int index) => HeapByteBufferUtil.GetShort(this.Memory, this.Idx(index));

        protected internal override short _GetShortLE(int index) => HeapByteBufferUtil.GetShortLE(this.Memory, this.Idx(index));

        protected internal override int _GetUnsignedMedium(int index) => HeapByteBufferUtil.GetUnsignedMedium(this.Memory, this.Idx(index));

        protected internal override int _GetUnsignedMediumLE(int index) => HeapByteBufferUtil.GetUnsignedMediumLE(this.Memory, this.Idx(index));

        protected internal override int _GetInt(int index) => HeapByteBufferUtil.GetInt(this.Memory, this.Idx(index));

        protected internal override int _GetIntLE(int index) => HeapByteBufferUtil.GetIntLE(this.Memory, this.Idx(index));

        protected internal override long _GetLong(int index) => HeapByteBufferUtil.GetLong(this.Memory, this.Idx(index));

        protected internal override long _GetLongLE(int index) => HeapByteBufferUtil.GetLongLE(this.Memory, this.Idx(index));

        public override IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            this.CheckDstIndex(index, length, dstIndex, dst.Capacity);
            if (dst.HasArray)
            {
                this.GetBytes(index, dst.Array, dst.ArrayOffset + dstIndex, length);
            }
            else
            {
                dst.SetBytes(dstIndex, this.Memory, this.Idx(index), length);
            }
            return this;
        }

        public override IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            this.CheckDstIndex(index, length, dstIndex, dst.Length);
            PlatformDependent.CopyMemory(this.Memory, this.Idx(index), dst, dstIndex, length);
            return this;
        }

        public override IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            this.CheckIndex(index, length);
            destination.Write(this.Memory, this.Idx(index), length);
            return this;
        }

        protected internal override void _SetByte(int index, int value) => HeapByteBufferUtil.SetByte(this.Memory, this.Idx(index), value);

        protected internal override void _SetShort(int index, int value) => HeapByteBufferUtil.SetShort(this.Memory, this.Idx(index), value);

        protected internal override void _SetShortLE(int index, int value) => HeapByteBufferUtil.SetShortLE(this.Memory, this.Idx(index), value);

        protected internal override void _SetMedium(int index, int value) => HeapByteBufferUtil.SetMedium(this.Memory, this.Idx(index), value);

        protected internal override void _SetMediumLE(int index, int value) => HeapByteBufferUtil.SetMediumLE(this.Memory, this.Idx(index), value);

        protected internal override void _SetInt(int index, int value) => HeapByteBufferUtil.SetInt(this.Memory, this.Idx(index), value);

        protected internal override void _SetIntLE(int index, int value) => HeapByteBufferUtil.SetIntLE(this.Memory, this.Idx(index), value);

        protected internal override void _SetLong(int index, long value) => HeapByteBufferUtil.SetLong(this.Memory, this.Idx(index), value);

        protected internal override void _SetLongLE(int index, long value) => HeapByteBufferUtil.SetLongLE(this.Memory, this.Idx(index), value);

        public override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            this.CheckSrcIndex(index, length, srcIndex, src.Capacity);
            if (src.HasArray)
            {
                this.SetBytes(index, src.Array, src.ArrayOffset + srcIndex, length);
            }
            else
            {
                src.GetBytes(srcIndex, this.Memory, this.Idx(index), length);
            }
            return this;
        }

        public override async Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            int readTotal = 0;
            int read;
            int offset = this.ArrayOffset + index;
            do
            {
                read = await src.ReadAsync(this.Array, offset + readTotal, length - readTotal, cancellationToken);
                readTotal += read;
            }
            while (read > 0 && readTotal < length);

            return readTotal;
        }

        public override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            this.CheckSrcIndex(index, length, srcIndex, src.Length);
            PlatformDependent.CopyMemory(src, srcIndex, this.Memory, this.Idx(index), length);
            return this;
        }

        public override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            IByteBuffer copy = this.Allocator.HeapBuffer(length, this.MaxCapacity);
            copy.WriteBytes(this.Memory, this.Idx(index), length);
            return copy;
        }


        public override IByteBuffer SetZero(int index, int length)
        {
            this.CheckIndex(index, length);
            PlatformDependent.Clear(this.Memory, this.Idx(index), length);
            return this;
        }

        public override int IoBufferCount => 1;

        public override ArraySegment<byte> GetIoBuffer(int index, int length)
        {
            this.CheckIndex(index, length);
            index = index + this.Offset;
            return new ArraySegment<byte>(this.Memory, index, length);
        }

        public override ArraySegment<byte>[] GetIoBuffers(int index, int length) => new[] { this.GetIoBuffer(index, length) };

        public override bool HasArray => true;

        public override byte[] Array
        {
            get
            {
                this.EnsureAccessible();
                return this.Memory;
            }
        }

        public override int ArrayOffset => this.Offset;

        public override bool HasMemoryAddress => true;

        public override ref byte GetPinnableMemoryAddress()
        {
            this.EnsureAccessible();
            return ref this.Memory[this.Offset];
        }

        public override IntPtr AddressOfPinnedMemory() => IntPtr.Zero;
    }
}