// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Dervied buffer that forwards requests to the original underlying buffer
    /// </summary>
    public sealed class DuplicatedByteBuffer : AbstractDerivedByteBuffer
    {
        readonly IByteBuffer buffer;

        public DuplicatedByteBuffer(IByteBuffer source)
            : base(source.MaxCapacity)
        {
            var asDuplicate = source as DuplicatedByteBuffer;
            this.buffer = asDuplicate != null ? asDuplicate.buffer : source;
            this.SetIndex(source.ReaderIndex, source.WriterIndex);
        }

        public override int Capacity
        {
            get { return this.buffer.Capacity; }
        }

        public override IByteBuffer AdjustCapacity(int newCapacity)
        {
            return this.buffer.AdjustCapacity(newCapacity);
        }

        public override IByteBufferAllocator Allocator
        {
            get { return this.buffer.Allocator; }
        }

        public override byte GetByte(int index)
        {
            return this._GetByte(index);
        }

        protected override byte _GetByte(int index)
        {
            return this.buffer.GetByte(index);
        }

        public override short GetShort(int index)
        {
            return this._GetShort(index);
        }

        protected override short _GetShort(int index)
        {
            return this.buffer.GetShort(index);
        }

        public override int GetInt(int index)
        {
            return this._GetInt(index);
        }

        protected override int _GetInt(int index)
        {
            return this.buffer.GetInt(index);
        }

        public override long GetLong(int index)
        {
            return this._GetLong(index);
        }

        protected override long _GetLong(int index)
        {
            return this.buffer.GetLong(index);
        }

        public override IByteBuffer GetBytes(int index, IByteBuffer destination, int dstIndex, int length)
        {
            this.buffer.GetBytes(index, destination, dstIndex, length);
            return this;
        }

        public override IByteBuffer GetBytes(int index, byte[] destination, int dstIndex, int length)
        {
            this.buffer.GetBytes(index, destination, dstIndex, length);
            return this;
        }

        public override IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            this.buffer.GetBytes(index, destination, length);
            return this;
        }

        public override IByteBuffer GetBytes(int index, IByteBuffer destination)
        {
            this.buffer.GetBytes(index, destination);
            return this;
        }

        public override IByteBuffer SetByte(int index, int value)
        {
            this._SetByte(index, value);
            return this;
        }

        protected override void _SetByte(int index, int value)
        {
            this.buffer.SetByte(index, value);
        }

        public override IByteBuffer SetShort(int index, int value)
        {
            this._SetShort(index, value);
            return this;
        }

        protected override void _SetShort(int index, int value)
        {
            this.buffer.SetShort(index, value);
        }

        public override IByteBuffer SetInt(int index, int value)
        {
            this._SetInt(index, value);
            return this;
        }

        protected override void _SetInt(int index, int value)
        {
            this.buffer.SetInt(index, value);
        }

        public override IByteBuffer SetLong(int index, long value)
        {
            this._SetLong(index, value);
            return this;
        }

        protected override void _SetLong(int index, long value)
        {
            this.buffer.SetLong(index, value);
        }

        public override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            this.buffer.SetBytes(index, src, srcIndex, length);
            return this;
        }

        public override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            this.buffer.SetBytes(index, src, srcIndex, length);
            return this;
        }

        public override Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            return this.buffer.SetBytesAsync(index, src, length, cancellationToken);
        }

        public override bool HasArray
        {
            get { return this.buffer.HasArray; }
        }

        public override byte[] Array
        {
            get { return this.buffer.Array; }
        }

        public override IByteBuffer Copy(int index, int length)
        {
            return this.buffer.Copy(index, length);
        }

        public override int ArrayOffset
        {
            get { return this.buffer.ArrayOffset; }
        }

        public override IByteBuffer Unwrap()
        {
            return this.buffer;
        }
    }
}