// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Buffers
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Internal;

    public unsafe class UnpooledUnsafeDirectByteBuffer : AbstractReferenceCountedByteBuffer
    {
        readonly IByteBufferAllocator allocator;

        int capacity;
        bool doNotFree;
        byte[] buffer;

        public UnpooledUnsafeDirectByteBuffer(IByteBufferAllocator alloc, int initialCapacity, int maxCapacity)
            : base(maxCapacity)
        {
            Contract.Requires(alloc != null);
            Contract.Requires(initialCapacity >= 0);
            Contract.Requires(maxCapacity >= 0);

            if (initialCapacity > maxCapacity)
            {
                throw new ArgumentException($"initialCapacity({initialCapacity}) > maxCapacity({maxCapacity})");
            }

            this.allocator = alloc;
            this.SetByteBuffer(this.NewArray(initialCapacity), false);
        }

        protected UnpooledUnsafeDirectByteBuffer(IByteBufferAllocator alloc, byte[] initialBuffer, int maxCapacity, bool doFree)
            : base(maxCapacity)
        {
            Contract.Requires(alloc != null);
            Contract.Requires(initialBuffer != null);

            int initialCapacity = initialBuffer.Length;
            if (initialCapacity > maxCapacity)
            {
                throw new ArgumentException($"initialCapacity({initialCapacity}) > maxCapacity({maxCapacity})");
            }

            this.allocator = alloc;
            this.doNotFree = !doFree;
            this.SetByteBuffer(initialBuffer, false);
        }

        protected virtual byte[] AllocateDirect(int initialCapacity) => this.NewArray(initialCapacity);

        protected byte[] NewArray(int initialCapacity) => new byte[initialCapacity];

        protected virtual void FreeDirect(byte[] array)
        {
            // NOOP rely on GC.
        }

        void SetByteBuffer(byte[] array, bool tryFree)
        {
            if (tryFree)
            {
                byte[] oldBuffer = this.buffer;
                if (oldBuffer != null)
                {
                    if (this.doNotFree)
                    {
                        this.doNotFree = false;
                    }
                    else
                    {
                        this.FreeDirect(oldBuffer);
                    }
                }
            }
            this.buffer = array;
            this.capacity = array.Length;
        }

        public override bool IsDirect => true;

        public override int Capacity => this.capacity;

        public override IByteBuffer AdjustCapacity(int newCapacity)
        {
            this.CheckNewCapacity(newCapacity);

            int rIdx = this.ReaderIndex;
            int wIdx = this.WriterIndex;

            int oldCapacity = this.capacity;
            if (newCapacity > oldCapacity)
            {
                byte[] oldBuffer = this.buffer;
                byte[] newBuffer = this.AllocateDirect(newCapacity);
                PlatformDependent.CopyMemory(oldBuffer, 0, newBuffer, 0, oldCapacity);
                this.SetByteBuffer(newBuffer, true);
            }
            else if (newCapacity < oldCapacity)
            {
                byte[] oldBuffer = this.buffer;
                byte[] newBuffer = this.AllocateDirect(newCapacity);
                if (rIdx < newCapacity)
                {
                    if (wIdx > newCapacity)
                    {
                        this.SetWriterIndex(wIdx = newCapacity);
                    }
                    PlatformDependent.CopyMemory(oldBuffer, rIdx, newBuffer, 0, wIdx - rIdx);
                }
                else
                {
                    this.SetIndex(newCapacity, newCapacity);
                }
                this.SetByteBuffer(newBuffer, true);
            }
            return this;
        }

        public override IByteBufferAllocator Allocator => this.allocator;

        public override bool HasArray => true;

        public override byte[] Array
        {
            get
            {
                this.EnsureAccessible();
                return this.buffer;
            }
        }

        public override int ArrayOffset => 0;

        public override bool HasMemoryAddress => true;

        public override ref byte GetPinnableMemoryAddress()
        {
            this.EnsureAccessible();
            return ref this.buffer[0];
        }

        public override IntPtr AddressOfPinnedMemory() => IntPtr.Zero;

        protected internal override byte _GetByte(int index) => this.buffer[index];

        protected internal override short _GetShort(int index)
        {
            fixed (byte* addr = &this.Addr(index))
                return UnsafeByteBufferUtil.GetShort(addr);
        }

        protected internal override short _GetShortLE(int index)
        {
            fixed (byte* addr = &this.Addr(index))
                return UnsafeByteBufferUtil.GetShortLE(addr);
        }

        protected internal override int _GetUnsignedMedium(int index)
        {
            fixed (byte* addr = &this.Addr(index))
                return UnsafeByteBufferUtil.GetUnsignedMedium(addr);
        }

        protected internal override int _GetUnsignedMediumLE(int index)
        {
            fixed (byte* addr = &this.Addr(index))
                return UnsafeByteBufferUtil.GetUnsignedMediumLE(addr);
        }

        protected internal override int _GetInt(int index)
        {
            fixed (byte* addr = &this.Addr(index))
                return UnsafeByteBufferUtil.GetInt(addr);
        }

        protected internal override int _GetIntLE(int index)
        {
            fixed (byte* addr = &this.Addr(index))
                return UnsafeByteBufferUtil.GetIntLE(addr);
        }

        protected internal override long _GetLong(int index)
        {
            fixed (byte* addr = &this.Addr(index))
                return UnsafeByteBufferUtil.GetLong(addr);
        }

        protected internal override long _GetLongLE(int index)
        {
            fixed (byte* addr = &this.Addr(index))
                return UnsafeByteBufferUtil.GetLongLE(addr);
        }

        public override IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            this.CheckIndex(index, length);
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.GetBytes(this, addr, index, dst, dstIndex, length);
            return this;
        }

        public override IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            this.CheckIndex(index, length);
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.GetBytes(this, addr, index, dst, dstIndex, length);
            return this;
        }

        protected internal override void _SetByte(int index, int value) => this.buffer[index] = unchecked((byte)value);

        protected internal override void _SetShort(int index, int value)
        {
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.SetShort(addr, value);
        }

        protected internal override void _SetShortLE(int index, int value)
        {
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.SetShortLE(addr, value);
        }

        protected internal override void _SetMedium(int index, int value)
        {
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.SetMedium(addr, value);
        }

        protected internal override void _SetMediumLE(int index, int value)
        {
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.SetMediumLE(addr, value);
        }

        protected internal override void _SetInt(int index, int value)
        {
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.SetInt(addr, value);
        }

        protected internal override void _SetIntLE(int index, int value)
        {
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.SetIntLE(addr, value);
        }

        protected internal override void _SetLong(int index, long value)
        {
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.SetLong(addr, value);
        }

        protected internal override void _SetLongLE(int index, long value)
        {
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.SetLongLE(addr, value);
        }

        public override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            this.CheckIndex(index, length);
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.SetBytes(this, addr, index, src, srcIndex, length);
            return this;
        }

        public override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            this.CheckIndex(index, length);
            if (length != 0)
            {
                fixed (byte* addr = &this.Addr(index))
                    UnsafeByteBufferUtil.SetBytes(this, addr, index, src, srcIndex, length);
            }
            return this;
        }

        public override IByteBuffer GetBytes(int index, Stream output, int length)
        {
            this.CheckIndex(index, length);
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.GetBytes(this, addr, index, output, length);
            return this;
        }

        public override Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            this.CheckIndex(index, length);
            int read;
            fixed (byte* addr = &this.Addr(index))
                read = UnsafeByteBufferUtil.SetBytes(this, addr, index, src, length);
            return Task.FromResult(read);
        }

        public override int IoBufferCount => 1;

        public override ArraySegment<byte> GetIoBuffer(int index, int length)
        {
            this.CheckIndex(index, length);
            return new ArraySegment<byte>(this.buffer, index, length);
        }

        public override ArraySegment<byte>[] GetIoBuffers(int index, int length) => new[] { this.GetIoBuffer(index, length) };

        public override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            fixed (byte* addr = &this.Addr(index))
                return UnsafeByteBufferUtil.Copy(this, addr, index, length);
        }

        protected internal override void Deallocate()
        {
            byte[] buf = this.buffer;
            if (buf == null)
            {
                return;
            }

            this.buffer = null;

            if (!this.doNotFree)
            {
                this.FreeDirect(buf);
            }
        }

        public override IByteBuffer Unwrap() => null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ref byte Addr(int index) => ref this.buffer[index];

        public override IByteBuffer SetZero(int index, int length)
        {
            this.CheckIndex(index, length);
            fixed (byte* addr = &this.Addr(index))
                UnsafeByteBufferUtil.SetZero(addr, length);
            return this;
        }

        public override IByteBuffer WriteZero(int length)
        {
            this.EnsureWritable(length);
            int wIndex = this.WriterIndex;
            fixed (byte* addr = &this.Addr(wIndex))
                UnsafeByteBufferUtil.SetZero(addr, length);
            this.SetWriterIndex(wIndex + length);
            return this;
        }
    }
}