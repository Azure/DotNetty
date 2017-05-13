// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    sealed class PooledHeapByteBuffer : PooledByteBuffer<byte[]>
    {
        static readonly ThreadLocalPool<PooledHeapByteBuffer> Recycler = new ThreadLocalPool<PooledHeapByteBuffer>(handle => new PooledHeapByteBuffer(handle, 0));

        internal static PooledHeapByteBuffer NewInstance(int maxCapacity)
        {
            PooledHeapByteBuffer buf = Recycler.Take();
            buf.SetReferenceCount(1); // todo: reuse method?
            buf.MaxCapacity = maxCapacity;
            buf.SetIndex(0, 0);
            buf.DiscardMarkers();
            return buf;
        }

        PooledHeapByteBuffer(ThreadLocalPool.Handle recyclerHandle, int maxCapacity)
            : base(recyclerHandle, maxCapacity)
        {
        }

        protected override byte _GetByte(int index) => this.Memory[this.Idx(index)];

        protected override short _GetShort(int index)
        {
            index = this.Idx(index);
            return (short)(this.Memory[index] << 8 | this.Memory[index + 1] & 0xFF);
        }

        protected override int _GetInt(int index)
        {
            index = this.Idx(index);
            return (this.Memory[index] & 0xff) << 24 |
                (this.Memory[index + 1] & 0xff) << 16 |
                (this.Memory[index + 2] & 0xff) << 8 |
                this.Memory[index + 3] & 0xff;
        }

        protected override long _GetLong(int index)
        {
            index = this.Idx(index);
            return ((long)this.Memory[index] & 0xff) << 56 |
                ((long)this.Memory[index + 1] & 0xff) << 48 |
                ((long)this.Memory[index + 2] & 0xff) << 40 |
                ((long)this.Memory[index + 3] & 0xff) << 32 |
                ((long)this.Memory[index + 4] & 0xff) << 24 |
                ((long)this.Memory[index + 5] & 0xff) << 16 |
                ((long)this.Memory[index + 6] & 0xff) << 8 |
                (long)this.Memory[index + 7] & 0xff;
        }

        protected override int _GetMedium(int index)
        {
            index = this.Idx(index);
            return (sbyte)this.Memory[index] << 16 |
                   this.Memory[index + 1] << 8 |
                   this.Memory[index + 2];
        }

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
            System.Array.Copy(this.Memory, this.Idx(index), dst, dstIndex, length);
            return this;
        }

        public override IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            this.CheckIndex(index, length);
            destination.Write(this.Memory, this.Idx(index), length);
            return this;
        }

        protected override void _SetByte(int index, int value) => this.Memory[this.Idx(index)] = (byte)value;

        protected override void _SetShort(int index, int value)
        {
            index = this.Idx(index);
            this.Memory[index] = (byte)value.RightUShift(8);
            this.Memory[index + 1] = (byte)value;
        }

        protected override void _SetInt(int index, int value)
        {
            index = this.Idx(index);
            this.Memory[index] = (byte)value.RightUShift(24);
            this.Memory[index + 1] = (byte)value.RightUShift(16);
            this.Memory[index + 2] = (byte)value.RightUShift(8);
            this.Memory[index + 3] = (byte)value;
        }

        protected override void _SetMedium(int index, int value)
        {
            index = this.Idx(index);
            this.Memory[index] = (byte)value.RightUShift(16);
            this.Memory[index + 1] = (byte)value.RightUShift(8);
            this.Memory[index + 2] = (byte)value;
        }

        protected override void _SetLong(int index, long value)
        {
            index = this.Idx(index);
            this.Memory[index] = (byte)value.RightUShift(56);
            this.Memory[index + 1] = (byte)value.RightUShift(48);
            this.Memory[index + 2] = (byte)value.RightUShift(40);
            this.Memory[index + 3] = (byte)value.RightUShift(32);
            this.Memory[index + 4] = (byte)value.RightUShift(24);
            this.Memory[index + 5] = (byte)value.RightUShift(16);
            this.Memory[index + 6] = (byte)value.RightUShift(8);
            this.Memory[index + 7] = (byte)value;
        }

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
            System.Array.Copy(src, srcIndex, this.Memory, this.Idx(index), length);
            return this;
        }

        public override IByteBuffer SetZero(int index, int length)
        {
            this.CheckIndex(index, length);
            System.Array.Clear(this.Memory, this.Idx(index), length);
            return this;
        }

        public override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            IByteBuffer copy = this.Allocator.Buffer(length, this.MaxCapacity);
            copy.WriteBytes(this.Memory, this.Idx(index), length);
            return copy;
        }

        //public int nioBufferCount()
        //{
        //    return 1;
        //}

        //public ByteBuffer[] nioBuffers(int index, int length)
        //{
        //    return new ByteBuffer[] { this.nioBuffer(index, length) };
        //}

        //public ByteBuffer nioBuffer(int index, int length)
        //{
        //    checkIndex(index, length);
        //    index = idx(index);
        //    ByteBuffer buf = ByteBuffer.wrap(this.memory, index, length);
        //    return buf.slice();
        //}

        //public ByteBuffer internalNioBuffer(int index, int length)
        //{
        //    checkIndex(index, length);
        //    index = idx(index);
        //    return (ByteBuffer)internalNioBuffer().clear().position(index).limit(index + length);
        //}

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

        //protected ByteBuffer newInternalNioBuffer(byte[] memory)
        //{
        //    return ByteBuffer.wrap(memory);
        //}
    }
}