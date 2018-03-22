// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Buffers
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Internal;

    public class UnpooledHeapByteBuffer : AbstractReferenceCountedByteBuffer
    {
        readonly IByteBufferAllocator allocator;
        byte[] array;

        protected internal UnpooledHeapByteBuffer(IByteBufferAllocator alloc, int initialCapacity, int maxCapacity)
            : base(maxCapacity)
        {
            Contract.Requires(alloc != null);
            Contract.Requires(initialCapacity <= maxCapacity);

            this.allocator = alloc;
            this.SetArray(this.NewArray(initialCapacity));
            this.SetIndex0(0, 0);
        }

        protected internal UnpooledHeapByteBuffer(IByteBufferAllocator alloc, byte[] initialArray, int maxCapacity)
            : base(maxCapacity)
        {
            Contract.Requires(alloc != null);
            Contract.Requires(initialArray != null);

            if (initialArray.Length > maxCapacity)
            {
                throw new ArgumentException($"initialCapacity({initialArray.Length}) > maxCapacity({maxCapacity})");
            }

            this.allocator = alloc;
            this.SetArray(initialArray);
            this.SetIndex0(0, initialArray.Length);
        }

        protected virtual byte[] AllocateArray(int initialCapacity) => this.NewArray(initialCapacity);

        protected byte[] NewArray(int initialCapacity) => new byte[initialCapacity];

        protected virtual void FreeArray(byte[] bytes)
        {
            // NOOP
        }

        protected void SetArray(byte[] initialArray) => this.array = initialArray;

        public override IByteBufferAllocator Allocator => this.allocator;

        public override bool IsDirect => false;

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
            byte[] oldArray = this.array;
            if (newCapacity > oldCapacity)
            {
                byte[] newArray = this.AllocateArray(newCapacity);
                PlatformDependent.CopyMemory(this.array, 0, newArray, 0, oldCapacity);

                this.SetArray(newArray);
                this.FreeArray(oldArray);
            }
            else if (newCapacity < oldCapacity)
            {
                byte[] newArray = this.AllocateArray(newCapacity);
                int readerIndex = this.ReaderIndex;
                if (readerIndex < newCapacity)
                {
                    int writerIndex = this.WriterIndex;
                    if (writerIndex > newCapacity)
                    {
                        this.SetWriterIndex0(writerIndex = newCapacity);
                    }

                    PlatformDependent.CopyMemory(this.array, readerIndex, newArray, 0, writerIndex - readerIndex);
                }
                else
                {
                    this.SetIndex(newCapacity, newCapacity);
                }

                this.SetArray(newArray);
                this.FreeArray(oldArray);
            }
            return this;
        }

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

        public override bool HasMemoryAddress => true;

        public override ref byte GetPinnableMemoryAddress()
        {
            this.EnsureAccessible();
            return ref this.array[0];
        }

        public override IntPtr AddressOfPinnedMemory() => IntPtr.Zero;

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
            PlatformDependent.CopyMemory(this.array, index, dst, dstIndex, length);
            return this;
        }

        public override IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            this.EnsureAccessible();
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
            PlatformDependent.CopyMemory(src, srcIndex, this.array, index, length);
            return this;
        }

        public override async Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            this.EnsureAccessible();
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

        public override int IoBufferCount => 1;

        public override ArraySegment<byte> GetIoBuffer(int index, int length)
        {
            this.EnsureAccessible();
            return new ArraySegment<byte>(this.array, index, length);
        }

        public override ArraySegment<byte>[] GetIoBuffers(int index, int length) => new[] { this.GetIoBuffer(index, length) };

        public override byte GetByte(int index)
        {
            this.EnsureAccessible();
            return this._GetByte(index);
        }

        protected internal override byte _GetByte(int index) => HeapByteBufferUtil.GetByte(this.array, index);

        public override IByteBuffer SetZero(int index, int length)
        {
            this.CheckIndex(index, length);
            PlatformDependent.Clear(this.array, index, length);
            return this;
        }

        public override short GetShort(int index)
        {
            this.EnsureAccessible();
            return this._GetShort(index);
        }

        protected internal override short _GetShort(int index) => HeapByteBufferUtil.GetShort(this.array, index);

        public override short GetShortLE(int index)
        {
            this.EnsureAccessible();
            return this._GetShortLE(index);
        }

        protected internal override short _GetShortLE(int index) => HeapByteBufferUtil.GetShortLE(this.array, index);

        public override int GetUnsignedMedium(int index)
        {
            this.EnsureAccessible();
            return this._GetUnsignedMedium(index);
        }

        protected internal override int _GetUnsignedMedium(int index) => HeapByteBufferUtil.GetUnsignedMedium(this.array, index);

        public override int GetUnsignedMediumLE(int index)
        {
            this.EnsureAccessible();
            return this._GetUnsignedMediumLE(index);
        }

        protected internal override int _GetUnsignedMediumLE(int index) => HeapByteBufferUtil.GetUnsignedMediumLE(this.array, index);

        public override int GetInt(int index)
        {
            this.EnsureAccessible();
            return this._GetInt(index);
        }

        protected internal override int _GetInt(int index) => HeapByteBufferUtil.GetInt(this.array, index);

        public override int GetIntLE(int index)
        {
            this.EnsureAccessible();
            return this._GetIntLE(index);
        }

        protected internal override int _GetIntLE(int index) => HeapByteBufferUtil.GetIntLE(this.array, index);

        public override long GetLong(int index)
        {
            this.EnsureAccessible();
            return this._GetLong(index);
        }

        protected internal override long _GetLong(int index) => HeapByteBufferUtil.GetLong(this.array, index);

        public override long GetLongLE(int index)
        {
            this.EnsureAccessible();
            return this._GetLongLE(index);
        }

        protected internal override long _GetLongLE(int index) => HeapByteBufferUtil.GetLongLE(this.array, index);

        public override IByteBuffer SetByte(int index, int value)
        {
            this.EnsureAccessible();
            this._SetByte(index, value);
            return this;
        }

        protected internal override void _SetByte(int index, int value) => HeapByteBufferUtil.SetByte(this.array, index, value);

        public override IByteBuffer SetShort(int index, int value)
        {
            this.EnsureAccessible();
            this._SetShort(index, value);
            return this;
        }

        protected internal override void _SetShort(int index, int value) => HeapByteBufferUtil.SetShort(this.array, index, value);

        public override IByteBuffer SetShortLE(int index, int value)
        {
            this.EnsureAccessible();
            this._SetShortLE(index, value);
            return this;
        }

        protected internal override void _SetShortLE(int index, int value) => HeapByteBufferUtil.SetShortLE(this.array, index, value);

        public override IByteBuffer SetMedium(int index, int value)
        {
            this.EnsureAccessible();
            this._SetMedium(index, value);
            return this;
        }

        protected internal override void _SetMedium(int index, int value) => HeapByteBufferUtil.SetMedium(this.array, index, value);

        public override IByteBuffer SetMediumLE(int index, int value)
        {
            this.EnsureAccessible();
            this._SetMediumLE(index, value);
            return this;
        }

        protected internal override void _SetMediumLE(int index, int value) => HeapByteBufferUtil.SetMediumLE(this.array, index, value);

        public override IByteBuffer SetInt(int index, int value)
        {
            this.EnsureAccessible();
            this._SetInt(index, value);
            return this;
        }

        protected internal override void _SetInt(int index, int value) => HeapByteBufferUtil.SetInt(this.array, index, value);

        public override IByteBuffer SetIntLE(int index, int value)
        {
            this.EnsureAccessible();
            this._SetIntLE(index, value);
            return this;
        }

        protected internal override void _SetIntLE(int index, int value) => HeapByteBufferUtil.SetIntLE(this.array, index, value);

        public override IByteBuffer SetLong(int index, long value)
        {
            this.EnsureAccessible();
            this._SetLong(index, value);
            return this;
        }

        protected internal override void _SetLong(int index, long value) => HeapByteBufferUtil.SetLong(this.array, index, value);

        public override IByteBuffer SetLongLE(int index, long value)
        {
            this.EnsureAccessible();
            this._SetLongLE(index, value);
            return this;
        }

        protected internal override void _SetLongLE(int index, long value) => HeapByteBufferUtil.SetLongLE(this.array, index, value);

        public override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            var copiedArray = new byte[length];
            PlatformDependent.CopyMemory(this.array, index, copiedArray, 0, length);

            return new UnpooledHeapByteBuffer(this.Allocator, copiedArray, this.MaxCapacity);
        }

        protected internal override void Deallocate()
        {
            this.FreeArray(this.array);
            this.array = null;
        }

        public override IByteBuffer Unwrap() => null;
    }
}