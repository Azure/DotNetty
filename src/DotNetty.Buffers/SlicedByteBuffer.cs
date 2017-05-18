// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class SlicedByteBuffer : AbstractDerivedByteBuffer
    {
        readonly IByteBuffer buffer;
        readonly int adjustment;
        readonly int length;

        public SlicedByteBuffer(IByteBuffer buffer, int index, int length)
            : base(length)
        {
            if (index < 0 || index > buffer.Capacity - length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), buffer + ".slice(" + index + ", " + length + ')');
            }

            var slicedByteBuf = buffer as SlicedByteBuffer;
            if (slicedByteBuf != null)
            {
                this.buffer = slicedByteBuf.buffer;
                this.adjustment = slicedByteBuf.adjustment + index;
            }
            else if (buffer is DuplicatedByteBuffer)
            {
                this.buffer = buffer.Unwrap();
                this.adjustment = index;
            }
            else
            {
                this.buffer = buffer;
                this.adjustment = index;
            }
            this.length = length;

            this.SetWriterIndex(length);
        }

        public override IByteBuffer Unwrap() => this.buffer;

        public override IByteBufferAllocator Allocator => this.buffer.Allocator;

        public override ByteOrder Order => this.buffer.Order;

        public override int Capacity => this.length;

        public override IByteBuffer AdjustCapacity(int newCapacity)
        {
            throw new NotSupportedException("sliced buffer");
        }

        public override int IoBufferCount => this.Unwrap().IoBufferCount;

        public override ArraySegment<byte> GetIoBuffer(int index, int length)
        {
            this.CheckIndex(index, length);
            return this.Unwrap().GetIoBuffer(index + this.adjustment, length);
        }

        public override ArraySegment<byte>[] GetIoBuffers(int index, int length)
        {
            this.CheckIndex(index, length);
            return this.Unwrap().GetIoBuffers(index + this.adjustment, length);
        }

        public override bool HasArray => this.buffer.HasArray;

        public override byte[] Array => this.buffer.Array;

        public override int ArrayOffset => this.buffer.ArrayOffset + this.adjustment;

        protected override byte _GetByte(int index) => this.buffer.GetByte(index + this.adjustment);

        protected override short _GetShort(int index) => this.buffer.GetShort(index + this.adjustment);

        protected override int _GetInt(int index) => this.buffer.GetInt(index + this.adjustment);

        protected override long _GetLong(int index) => this.buffer.GetLong(index + this.adjustment);

        protected override int _GetMedium(int index) => this.buffer.GetMedium(index + this.adjustment);

        public override IByteBuffer Duplicate()
        {
            IByteBuffer duplicate = this.buffer.Slice(this.adjustment, this.length);
            duplicate.SetIndex(this.ReaderIndex, this.WriterIndex);
            return duplicate;
        }

        //public IByteBuffer copy(int index, int length)
        //{
        //    CheckIndex(index, length);
        //    return this.buffer.Copy(index + this.adjustment, length);
        //}

        public override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            return this.buffer.Copy(index + this.adjustment, length);
        }

        public override IByteBuffer Slice(int index, int length)
        {
            this.CheckIndex(index, length);
            if (length == 0)
            {
                return Unpooled.Empty;
            }
            return this.buffer.Slice(index + this.adjustment, length);
        }

        public override IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            this.CheckIndex(index, length);
            this.buffer.GetBytes(index + this.adjustment, dst, dstIndex, length);
            return this;
        }

        public override IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            this.CheckIndex(index, length);
            this.buffer.GetBytes(index + this.adjustment, dst, dstIndex, length);
            return this;
        }

        public override IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            this.CheckIndex(index, length);
            this.buffer.GetBytes(index + this.adjustment, destination, length);
            return this;
        }

        protected override void _SetByte(int index, int value) => this.buffer.SetByte(index + this.adjustment, value);

        protected override void _SetShort(int index, int value) => this.buffer.SetShort(index + this.adjustment, value);

        protected override void _SetInt(int index, int value) => this.buffer.SetInt(index + this.adjustment, value);

        protected override void _SetLong(int index, long value) => this.buffer.SetLong(index + this.adjustment, value);

        protected override void _SetMedium(int index, int value) => this.buffer.SetMedium(index + this.adjustment, value);

        public override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            this.CheckIndex(index, length);
            this.buffer.SetBytes(index + this.adjustment, src, srcIndex, length);
            return this;
        }

        public override Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            this.CheckIndex(index, length);
            return this.buffer.SetBytesAsync(index + this.adjustment, src, length, cancellationToken);
        }

        public override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            this.CheckIndex(index, length);
            this.buffer.SetBytes(index + this.adjustment, src, srcIndex, length);
            return this;
        }

        public override IByteBuffer SetZero(int index, int length)
        {
            this.CheckIndex(index, length);
            this.buffer.SetZero(index + this.adjustment, length);
            return this;
        }
    }
}