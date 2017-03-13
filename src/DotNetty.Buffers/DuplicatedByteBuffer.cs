// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Dervied buffer that forwards requests to the original underlying buffer
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

        public override int Capacity => this.buffer.Capacity;

        public override IByteBuffer AdjustCapacity(int newCapacity) => this.buffer.AdjustCapacity(newCapacity);

        public override IByteBufferAllocator Allocator => this.buffer.Allocator;

        public override byte GetByte(int index) => this._GetByte(index);

        protected override byte _GetByte(int index) => this.buffer.GetByte(index);

        public override short GetShort(int index) => this._GetShort(index);

        protected override short _GetShort(int index) => this.buffer.GetShort(index);

        public override int GetInt(int index) => this._GetInt(index);

        protected override int _GetInt(int index) => this.buffer.GetInt(index);

        public override int GetMedium(int index) => this._GetMedium(index);

        protected override int _GetMedium(int index) => this.buffer.GetMedium(index);

        public override long GetLong(int index) => this._GetLong(index);

        protected override long _GetLong(int index) => this.buffer.GetLong(index);

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

        protected override void _SetByte(int index, int value) => this.buffer.SetByte(index, value);

        public override IByteBuffer SetShort(int index, int value)
        {
            this._SetShort(index, value);
            return this;
        }

        protected override void _SetShort(int index, int value) => this.buffer.SetShort(index, value);

        public override IByteBuffer SetInt(int index, int value)
        {
            this._SetInt(index, value);
            return this;
        }

        protected override void _SetInt(int index, int value) => this.buffer.SetInt(index, value);

        public override IByteBuffer SetMedium(int index, int value)
        {
            this._SetMedium(index, value);
            return this;
        }

        protected override void _SetMedium(int index, int value) => this.buffer.SetMedium(index, value);

        public override IByteBuffer SetLong(int index, long value)
        {
            this._SetLong(index, value);
            return this;
        }

        protected override void _SetLong(int index, long value) => this.buffer.SetLong(index, value);

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

        public override Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken) => this.buffer.SetBytesAsync(index, src, length, cancellationToken);

        public override IByteBuffer SetZero(int index, int length)
        {
            this.buffer.SetZero(index, length);
            return this;
        }

        public override int IoBufferCount => this.Unwrap().IoBufferCount;

        public override ArraySegment<byte>[] GetIoBuffers(int index, int length) => this.Unwrap().GetIoBuffers(index, length);

        public override bool HasArray => this.buffer.HasArray;

        public override byte[] Array => this.buffer.Array;

        public override IByteBuffer Copy(int index, int length) => this.buffer.Copy(index, length);

        public override int ArrayOffset => this.buffer.ArrayOffset;

        public override IByteBuffer Unwrap() => this.buffer;
    }
}