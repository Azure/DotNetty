// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Threading;
    using DotNetty.Common;

    internal class PooledByteBuffer : AbstractReferenceCountedByteBuffer
    {
        private int offset;
        private int length;
        private long handle;
        private int maxLength;
        private PoolChunk chunk;

        public PooledByteBuffer(int maxCapacity)
            : base(maxCapacity)
        {
        }

        public override IByteBufferAllocator Allocator
        {
            get
            {
                return this.chunk.Arena.parent;
            }
        }

        public override bool HasArray
        {
            get
            {
                return this.Array != null;
            }
        }

        public override byte[] Array
        {
            get
            {
                if (this.chunk == null)
                {
                    throw new InvalidOperationException("chunk value is not setted.");
                }
                return this.chunk.Buffer;
            }
        }

        public override int ArrayOffset
        {
            get
            {
                return this.offset;
            }
        }

        public override int Capacity
        {
            get
            {
                return this.length;
            }
        }

        internal PoolChunk Chunk
        {
            get
            {
                return this.chunk;
            }
        }

        internal long Handle
        {
            get
            {
                return this.handle;
            }
        }

        internal void Init(PoolChunk chunk, long handle, int offset, int length, int maxLength)
        {
            this.chunk = chunk;
            this.handle = handle;
            this.offset = offset;
            this.length = length;
            this.maxLength = maxLength;
            this.SetIndex(0, 0);
        }

        public override IByteBuffer Unwrap()
        {
            return null;
        }

        public override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            IByteBuffer copy = this.Allocator.Buffer(length, this.MaxCapacity);            
            copy.WriteBytes(this.Array, this.Idx(index), length);
            return copy;
        }        

        public override IByteBuffer AdjustCapacity(int newCapacity)
        {
            this.EnsureAccessible();
            if (newCapacity < 0)
            {
                throw new ArgumentOutOfRangeException("newCapacity", "The value cannot be negative.");
            }
            if (newCapacity == this.length)
            {
                return this;
            }
            else if (newCapacity > this.length)
            {
                if (newCapacity <= this.maxLength)
                {
                    this.length = newCapacity;
                    return this;
                }
            }
            else if (newCapacity < this.length)
            {
                if (newCapacity > (this.maxLength >> 1))
                {
                    if (this.maxLength <= 512)
                    {
                        if (newCapacity > this.maxLength - 16)
                        {
                            this.length = newCapacity;
                            this.SetIndex(Math.Min(this.ReaderIndex, newCapacity), Math.Min(this.WriterIndex, newCapacity));
                            return this;
                        }
                    }
                    else // > 512 (i.e. >= 1024)
                    {
                        this.length = newCapacity;
                        this.SetIndex(Math.Min(this.ReaderIndex, newCapacity), Math.Min(this.WriterIndex, newCapacity));
                        return this;
                    }
                }
            }
            // Reallocation required
            this.chunk.Arena.Reallocate(this, newCapacity, true);
            return this;
        }

        public override IByteBuffer GetBytes(int index, IByteBuffer destination, int dstIndex, int length)
        {
            this.CheckDstIndex(index, length, dstIndex, destination.Capacity);
            this.GetBytes(index, destination.Array, destination.ArrayOffset + dstIndex, length);
            return this;
        }

        public override IByteBuffer GetBytes(int index, byte[] destination, int dstIndex, int length)
        {
            this.CheckDstIndex(index, length, dstIndex, destination.Length);
            Buffer.BlockCopy(this.Array, this.Idx(index), destination, dstIndex, length);
            return this;
        }

        public override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            this.CheckSrcIndex(index, length, srcIndex, src.Capacity);
            this.SetBytes(index, src.Array, src.ArrayOffset + srcIndex, length);
            return this;
        }

        public override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            this.CheckSrcIndex(index, length, srcIndex, src.Length);
            Buffer.BlockCopy(src, srcIndex, this.Array, this.Idx(index), length);
            return this;
        }

        protected override void Deallocate()
        {
            if (this.handle >= 0)
            {
                var handle = Interlocked.Exchange(ref this.handle, -1);
                this.chunk.Arena.Free(this.chunk, handle);
            }
        }

        protected override byte _GetByte(int index)
        {
            var bytes = this.GetBytesCore(index, 1);
            return bytes[0];
        }

        protected override int _GetInt(int index)
        {
            return BitConverter.ToInt32(this.GetBytesCore(index, 4), 0);
        }

        protected override long _GetLong(int index)
        {
            return BitConverter.ToInt64(this.GetBytesCore(index, 8), 0);
        }

        protected override short _GetShort(int index)
        {
            return BitConverter.ToInt16(this.GetBytesCore(index, 2), 0);
        }

        protected override void _SetByte(int index, int value)
        {
            this.SetBytesCore(index, new byte[] { (byte)value }, 1);
        }

        protected override void _SetInt(int index, int value)
        {
            this.SetBytesCore(index, BitConverter.GetBytes(value), 4);
        }

        protected override void _SetLong(int index, long value)
        {
            this.SetBytesCore(index, BitConverter.GetBytes(value), 8);
        }

        protected override void _SetShort(int index, int value)
        {
            this.SetBytesCore(index, BitConverter.GetBytes(value), 2);
        }        

        private byte[] GetBytesCore(int index, int length)
        {
            this.CheckIndex(index, length);
            var bytes = new byte[length];
            this.GetBytes(index, bytes, 0, length);
            return bytes;
        }

        private void SetBytesCore(int index, byte[] src, int length)
        {
            CheckIndex(index, length);
            this.SetBytes(index, src, 0, length);            
        }

        private int Idx(int index)
        {
            return this.offset + index;
        }
    }
}