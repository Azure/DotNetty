// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using DotNetty.Common.Utilities;
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public class UnpooledHeapByteBuffer : AbstractReferenceCountedByteBuffer
    {
        readonly IByteBufferAllocator allocator;
        byte[] array;

        /// <summary>
        ///     Creates a new heap buffer with a newly allocated byte array.
        ///     @param initialCapacity the initial capacity of the underlying byte array
        ///     @param maxCapacity the max capacity of the underlying byte array
        /// </summary>
        public UnpooledHeapByteBuffer(IByteBufferAllocator allocator, int initialCapacity, int maxCapacity)
            : this(allocator, new byte[initialCapacity], 0, 0, maxCapacity)
        {
        }

        /// <summary>
        ///     Creates a new heap buffer with an existing byte array.
        ///     @param initialArray the initial underlying byte array
        ///     @param maxCapacity the max capacity of the underlying byte array
        /// </summary>
        public UnpooledHeapByteBuffer(IByteBufferAllocator allocator, byte[] initialArray, int maxCapacity)
            : this(allocator, initialArray, 0, initialArray.Length, maxCapacity)
        {
        }

        public UnpooledHeapByteBuffer(
            IByteBufferAllocator allocator, byte[] initialArray, int readerIndex, int writerIndex, int maxCapacity)
            : base(maxCapacity)
        {
            Contract.Requires(allocator != null);
            Contract.Requires(initialArray != null);
            Contract.Requires(initialArray.Length <= maxCapacity);

            this.allocator = allocator;
            this.SetArray(initialArray);
            this.SetIndex(readerIndex, writerIndex);
        }

        protected void SetArray(byte[] initialArray) => this.array = initialArray;

        public override IByteBufferAllocator Allocator => this.allocator;

        public override ByteOrder Order => ByteOrder.BigEndian;

        public override int Capacity
        {
            get
            {
                this.EnsureAccessible();
                return this.array.Length;
            }
        }

        public override IByteBuffer AdjustCapacity(int newCapacity)
        {
            this.CheckNewCapacity(newCapacity);

            int oldCapacity = this.array.Length;
            if (newCapacity > oldCapacity)
            {
                var newArray = new byte[newCapacity];
                System.Array.Copy(this.array, 0, newArray, 0, this.array.Length);
                this.SetArray(newArray);
            }
            else if (newCapacity < oldCapacity)
            {
                var newArray = new byte[newCapacity];
                int readerIndex = this.ReaderIndex;
                if (readerIndex < newCapacity)
                {
                    int writerIndex = this.WriterIndex;
                    if (writerIndex > newCapacity)
                    {
                        this.SetWriterIndex(writerIndex = newCapacity);
                    }
                    System.Array.Copy(this.array, readerIndex, newArray, readerIndex, writerIndex - readerIndex);
                }
                else
                {
                    this.SetIndex(newCapacity, newCapacity);
                }
                this.SetArray(newArray);
            }
            return this;
        }

        public override int IoBufferCount => 1;

        public override ArraySegment<byte> GetIoBuffer(int index, int length)
        {
            this.EnsureAccessible();
            return new ArraySegment<byte>(this.array, index, length);
        }

        public override ArraySegment<byte>[] GetIoBuffers(int index, int length) => new[] { this.GetIoBuffer(index, length) };

        public override bool HasArray => true;

        public override byte[] Array
        {
            get
            {
                this.EnsureAccessible();
                return this.array;
            }
        }

        public override int ArrayOffset => 0;

        public override IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            this.CheckDstIndex(index, length, dstIndex, dst.Capacity);
            if (dst.HasArray)
            {
                this.GetBytes(index, dst.Array, dst.ArrayOffset + dstIndex, length);
            }
            else
            {
                dst.SetBytes(dstIndex, this.array, index, length);
            }
            return this;
        }

        public override IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            this.CheckDstIndex(index, length, dstIndex, dst.Length);
            System.Array.Copy(this.array, index, dst, dstIndex, length);
            return this;
        }

        public override IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            destination.Write(this.Array, this.ArrayOffset + index, length);
            return this;
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
                src.GetBytes(srcIndex, this.array, index, length);
            }
            return this;
        }

        public override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            this.CheckSrcIndex(index, length, srcIndex, src.Length);
            System.Array.Copy(src, srcIndex, this.array, index, length);
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

        public override IByteBuffer SetZero(int index, int length)
        {
            this.CheckIndex(index, length);
            System.Array.Clear(this.array, index, length);
            return this;
        }

        public override byte GetByte(int index)
        {
            this.EnsureAccessible();
            return this._GetByte(index);
        }

        protected override byte _GetByte(int index) => this.array[index];

        public override short GetShort(int index)
        {
            this.EnsureAccessible();
            return this._GetShort(index);
        }

        protected override short _GetShort(int index) => unchecked((short)(this.array[index] << 8 | this.array[index + 1]));

        public override int GetInt(int index)
        {
            this.EnsureAccessible();
            return this._GetInt(index);
        }

        protected override int _GetInt(int index)
        {
            return unchecked(this.array[index] << 24 |
                this.array[index + 1] << 16 |
                this.array[index + 2] << 8 |
                this.array[index + 3]);
        }

        public override long GetLong(int index)
        {
            this.EnsureAccessible();
            return this._GetLong(index);
        }

        public override int GetMedium(int index)
        {
            this.EnsureAccessible();
            return this._GetMedium(index);
        }

        protected override int _GetMedium(int index)
        {
            return (sbyte)this.array[index] << 16 |
                    this.array[index + 1] << 8 |
                    this.array[index + 2];
        }

        protected override long _GetLong(int index)
        {
            unchecked
            {
                int i1 = this.array[index] << 24 |
                    this.array[index + 1] << 16 |
                    this.array[index + 2] << 8 |
                    this.array[index + 3];
                int i2 = this.array[index + 4] << 24 |
                    this.array[index + 5] << 16 |
                    this.array[index + 6] << 8 |
                    this.array[index + 7];
                return (uint)i2 | ((long)i1 << 32);
            }
        }

        public override IByteBuffer SetByte(int index, int value)
        {
            this.EnsureAccessible();
            this._SetByte(index, value);
            return this;
        }

        protected override void _SetByte(int index, int value) => this.array[index] = (byte)value;

        public override IByteBuffer SetShort(int index, int value)
        {
            this.EnsureAccessible();
            this._SetShort(index, value);
            return this;
        }

        protected override void _SetShort(int index, int value)
        {
            unchecked
            {
                this.array[index] = (byte)((ushort)value >> 8);
                this.array[index + 1] = (byte)value;
            }
        }

        public override IByteBuffer SetMedium(int index, int value)
        {
            this.EnsureAccessible();
            this._SetMedium(index, value);
            return this;
        }

        protected override void _SetMedium(int index, int value)
        {
            unchecked
            {
                uint unsignedValue = (uint)value;
                this.array[index] = (byte)(unsignedValue >> 16);
                this.array[index + 1] = (byte)(unsignedValue >> 8);
                this.array[index + 2] = (byte)value;
            }
        }

        public override IByteBuffer SetInt(int index, int value)
        {
            this.EnsureAccessible();
            this._SetInt(index, value);
            return this;
        }

        protected override void _SetInt(int index, int value)
        {
            unchecked
            {
                uint unsignedValue = (uint)value;
                this.array[index] = (byte)(unsignedValue >> 24);
                this.array[index + 1] = (byte)(unsignedValue >> 16);
                this.array[index + 2] = (byte)(unsignedValue >> 8);
                this.array[index + 3] = (byte)value;
            }
        }

        public override IByteBuffer SetLong(int index, long value)
        {
            this.EnsureAccessible();
            this._SetLong(index, value);
            return this;
        }

        protected override void _SetLong(int index, long value)
        {
            unchecked
            {
                ulong unsignedValue = (ulong)value;
                this.array[index] = (byte)(unsignedValue >> 56);
                this.array[index + 1] = (byte)(unsignedValue >> 48);
                this.array[index + 2] = (byte)(unsignedValue >> 40);
                this.array[index + 3] = (byte)(unsignedValue >> 32);
                this.array[index + 4] = (byte)(unsignedValue >> 24);
                this.array[index + 5] = (byte)(unsignedValue >> 16);
                this.array[index + 6] = (byte)(unsignedValue >> 8);
                this.array[index + 7] = (byte)value;
            }
        }

        public override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            var copiedArray = new byte[length];
            System.Array.Copy(this.array, index, copiedArray, 0, length);
            return new UnpooledHeapByteBuffer(this.Allocator, copiedArray, this.MaxCapacity);
        }

        protected override void Deallocate() => this.array = null;

        public override IByteBuffer Unwrap() => null;
    }
}