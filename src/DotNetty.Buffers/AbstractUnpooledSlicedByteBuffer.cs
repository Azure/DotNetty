// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;

    abstract class AbstractUnpooledSlicedByteBuffer : AbstractDerivedByteBuffer
    {
        readonly IByteBuffer buffer;
        readonly int adjustment;

        protected AbstractUnpooledSlicedByteBuffer(IByteBuffer buffer, int index, int length)
            : base(length)
        {
            CheckSliceOutOfBounds(index, length, buffer);

            if (buffer is AbstractUnpooledSlicedByteBuffer byteBuffer)
            {
                this.buffer = byteBuffer.buffer;
                this.adjustment = byteBuffer.adjustment + index;
            }
            else if (buffer is UnpooledDuplicatedByteBuffer)
            {
                this.buffer = buffer.Unwrap();
                this.adjustment = index;
            }
            else
            {
                this.buffer = buffer;
                this.adjustment = index;
            }

            this.SetWriterIndex0(length);
        }

        internal int Length => this.Capacity;

        public override IByteBuffer Unwrap() => this.buffer;

        public override IByteBufferAllocator Allocator => this.Unwrap().Allocator;

        public override bool IsDirect => this.Unwrap().IsDirect;

        public override IByteBuffer AdjustCapacity(int newCapacity) => throw new NotSupportedException("sliced buffer");

        public override bool HasArray => this.Unwrap().HasArray;

        public override byte[] Array => this.Unwrap().Array;

        public override int ArrayOffset => this.Idx(this.Unwrap().ArrayOffset);

        public override bool HasMemoryAddress => this.Unwrap().HasMemoryAddress;

        public override ref byte GetPinnableMemoryAddress() => ref Unsafe.Add(ref this.Unwrap().GetPinnableMemoryAddress(), this.adjustment);

        public override IntPtr AddressOfPinnedMemory()
        {
            IntPtr ptr = this.Unwrap().AddressOfPinnedMemory();
            if (ptr == IntPtr.Zero)
            {
                return ptr;
            }
            return ptr + this.adjustment;
        }

        public override byte GetByte(int index)
        {
            this.CheckIndex0(index, 1);
            return this.Unwrap().GetByte(this.Idx(index));
        }

        protected internal override byte _GetByte(int index) => this.Unwrap().GetByte(this.Idx(index));

        public override short GetShort(int index)
        {
            this.CheckIndex0(index, 2);
            return this.Unwrap().GetShort(this.Idx(index));
        }

        protected internal override short _GetShort(int index) => this.Unwrap().GetShort(this.Idx(index));

        public override short GetShortLE(int index)
        {
            this.CheckIndex0(index, 2);
            return this.Unwrap().GetShortLE(this.Idx(index));
        }

        protected internal override short _GetShortLE(int index) => this.Unwrap().GetShortLE(this.Idx(index));

        public override int GetUnsignedMedium(int index)
        {
            this.CheckIndex0(index, 3);
            return this.Unwrap().GetUnsignedMedium(this.Idx(index));
        }

        protected internal override int _GetUnsignedMedium(int index) => this.Unwrap().GetUnsignedMedium(this.Idx(index));

        public override int GetUnsignedMediumLE(int index)
        {
            this.CheckIndex0(index, 3);
            return this.Unwrap().GetUnsignedMediumLE(this.Idx(index));
        }

        protected internal override int _GetUnsignedMediumLE(int index) => this.Unwrap().GetUnsignedMediumLE(this.Idx(index));

        public override int GetInt(int index)
        {
            this.CheckIndex0(index, 4);
            return this.Unwrap().GetInt(this.Idx(index));
        }

        protected internal override int _GetInt(int index) => this.Unwrap().GetInt(this.Idx(index));

        public override int GetIntLE(int index)
        {
            this.CheckIndex0(index, 4);
            return this.Unwrap().GetIntLE(this.Idx(index));
        }

        protected internal override int _GetIntLE(int index) => this.Unwrap().GetIntLE(this.Idx(index));

        public override long GetLong(int index)
        {
            this.CheckIndex0(index, 8);
            return this.Unwrap().GetLong(this.Idx(index));
        }

        protected internal override long _GetLong(int index) => this.Unwrap().GetLong(this.Idx(index));

        public override long GetLongLE(int index)
        {
            this.CheckIndex0(index, 8);
            return this.Unwrap().GetLongLE(this.Idx(index));
        }

        protected internal override long _GetLongLE(int index) => this.Unwrap().GetLongLE(this.Idx(index));

        public override IByteBuffer Duplicate() => this.Unwrap().Duplicate().SetIndex(this.Idx(this.ReaderIndex), this.Idx(this.WriterIndex));

        public override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex0(index, length);
            return this.Unwrap().Copy(this.Idx(index), length);
        }

        public override IByteBuffer Slice(int index, int length)
        {
            this.CheckIndex0(index, length);
            return this.Unwrap().Slice(this.Idx(index), length);
        }

        public override IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            this.CheckIndex0(index, length);
            this.Unwrap().GetBytes(this.Idx(index), dst, dstIndex, length);
            return this;
        }

        public override IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            this.CheckIndex0(index, length);
            this.Unwrap().GetBytes(this.Idx(index), dst, dstIndex, length);
            return this;
        }

        public override IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            this.CheckIndex0(index, length);
            this.Unwrap().GetBytes(this.Idx(index), destination, length);
            return this;
        }

        public override IByteBuffer SetByte(int index, int value)
        {
            this.CheckIndex0(index, 1);
            this.Unwrap().SetByte(this.Idx(index), value);
            return this;
        }

        protected internal override void _SetByte(int index, int value) => this.Unwrap().SetByte(this.Idx(index), value);

        public override IByteBuffer SetShort(int index, int value)
        {
            this.CheckIndex0(index, 2);
            this.Unwrap().SetShort(this.Idx(index), value);
            return this;
        }

        protected internal override void _SetShort(int index, int value) => this.Unwrap().SetShort(this.Idx(index), value);

        public override IByteBuffer SetShortLE(int index, int value)
        {
            this.CheckIndex0(index, 2);
            this.Unwrap().SetShortLE(this.Idx(index), value);
            return this;
        }

        protected internal override void _SetShortLE(int index, int value) => this.Unwrap().SetShortLE(this.Idx(index), value);

        public override IByteBuffer SetMedium(int index, int value)
        {
            this.CheckIndex0(index, 3);
            this.Unwrap().SetMedium(this.Idx(index), value);
            return this;
        }

        protected internal override void _SetMedium(int index, int value) => this.Unwrap().SetMedium(this.Idx(index), value);

        public override IByteBuffer SetMediumLE(int index, int value)
        {
            this.CheckIndex0(index, 3);
            this.Unwrap().SetMediumLE(this.Idx(index), value);
            return this;
        }

        protected internal override void _SetMediumLE(int index, int value) => this.Unwrap().SetMediumLE(this.Idx(index), value);

        public override IByteBuffer SetInt(int index, int value)
        {
            this.CheckIndex0(index, 4);
            this.Unwrap().SetInt(this.Idx(index), value);
            return this;
        }

        protected internal override void _SetInt(int index, int value) => this.Unwrap().SetInt(this.Idx(index), value);

        public override IByteBuffer SetIntLE(int index, int value)
        {
            this.CheckIndex0(index, 4);
            this.Unwrap().SetIntLE(this.Idx(index), value);
            return this;
        }

        protected internal override void _SetIntLE(int index, int value) => this.Unwrap().SetIntLE(this.Idx(index), value);

        public override IByteBuffer SetLong(int index, long value)
        {
            this.CheckIndex0(index, 8);
            this.Unwrap().SetLong(this.Idx(index), value);
            return this;
        }

        protected internal override void _SetLong(int index, long value) => this.Unwrap().SetLong(this.Idx(index), value);

        public override IByteBuffer SetLongLE(int index, long value)
        {
            this.CheckIndex0(index, 8);
            this.Unwrap().SetLongLE(this.Idx(index), value);
            return this;
        }

        protected internal override void _SetLongLE(int index, long value) => this.Unwrap().SetLongLE(this.Idx(index), value);

        public override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            this.CheckIndex0(index, length);
            this.Unwrap().SetBytes(this.Idx(index), src, srcIndex, length);
            return this;
        }

        public override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            this.CheckIndex0(index, length);
            this.Unwrap().SetBytes(this.Idx(index), src, srcIndex, length);
            return this;
        }

        public override Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            this.CheckIndex0(index, length);
            return this.Unwrap().SetBytesAsync(index + this.adjustment, src, length, cancellationToken);
        }

        public override int IoBufferCount => this.Unwrap().IoBufferCount;

        public override ArraySegment<byte> GetIoBuffer(int index, int length)
        {
            this.CheckIndex0(index, length);
            return this.Unwrap().GetIoBuffer(index + this.adjustment, length);
        }

        public override ArraySegment<byte>[] GetIoBuffers(int index, int length)
        {
            this.CheckIndex0(index, length);
            return this.Unwrap().GetIoBuffers(index + this.adjustment, length);
        }

        public override int ForEachByte(int index, int length, IByteProcessor processor)
        {
            this.CheckIndex0(index, length);
            int ret = this.Unwrap().ForEachByte(this.Idx(index), length, processor);
            if (ret >= this.adjustment)
            {
                return ret - this.adjustment;
            }
            else
            {
                return -1;
            }
        }

        public override int ForEachByteDesc(int index, int length, IByteProcessor processor)
        {
            this.CheckIndex0(index, length);
            int ret = this.Unwrap().ForEachByteDesc(this.Idx(index), length, processor);
            if (ret >= this.adjustment)
            {
                return ret - this.adjustment;
            }
            else
            {
                return -1;
            }
        }

        // Returns the index with the needed adjustment.
        protected int Idx(int index) => index + this.adjustment;
        
        internal static void CheckSliceOutOfBounds(int index, int length, IByteBuffer buffer)
        {
            if (MathUtil.IsOutOfBounds(index, length, buffer.Capacity))
            {
                throw new IndexOutOfRangeException($"{buffer}.Slice({index}, {length})");
            }
        }
    }
}
