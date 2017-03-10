// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Diagnostics.Contracts;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    abstract class PooledByteBuffer<T> : AbstractReferenceCountedByteBuffer
    {
        readonly ThreadLocalPool.Handle recyclerHandle;

        protected internal PoolChunk<T> Chunk;
        protected internal long Handle;
        protected internal T Memory;
        protected internal int Offset;
        protected internal int Length;
        internal int MaxLength;
        internal PoolThreadCache<T> Cache;
        PooledByteBufferAllocator allocator;

        protected PooledByteBuffer(ThreadLocalPool.Handle recyclerHandle, int maxCapacity)
            : base(maxCapacity)
        {
            this.recyclerHandle = recyclerHandle;
        }

        internal void Init(PoolChunk<T> chunk, long handle, int offset, int length, int maxLength, PoolThreadCache<T> cache)
        {
            this.Init0(chunk, handle, offset, length, maxLength, cache);
            this.DiscardMarkers();
        }

        internal void InitUnpooled(PoolChunk<T> chunk, int length) => this.Init0(chunk, 0, 0, length, length, null);

        void Init0(PoolChunk<T> chunk, long handle, int offset, int length, int maxLength, PoolThreadCache<T> cache)
        {
            Contract.Assert(handle >= 0);
            Contract.Assert(chunk != null);

            this.Chunk = chunk;
            this.Memory = chunk.Memory;
            this.allocator = chunk.Arena.Parent;
            this.Cache = cache;
            this.Handle = handle;
            this.Offset = offset;
            this.Length = length;
            this.MaxLength = maxLength;
            this.SetIndex(0, 0);
        }

        public override int Capacity => this.Length;

        public sealed override IByteBuffer AdjustCapacity(int newCapacity)
        {
            this.CheckNewCapacity(newCapacity);

            // If the request capacity does not require reallocation, just update the length of the memory.
            if (this.Chunk.Unpooled)
            {
                if (newCapacity == this.Length)
                {
                    return this;
                }
            }
            else
            {
                if (newCapacity > this.Length)
                {
                    if (newCapacity <= this.MaxLength)
                    {
                        this.Length = newCapacity;
                        return this;
                    }
                }
                else if (newCapacity < this.Length)
                {
                    if (newCapacity > this.MaxLength.RightUShift(1))
                    {
                        if (this.MaxLength <= 512)
                        {
                            if (newCapacity > this.MaxLength - 16)
                            {
                                this.Length = newCapacity;
                                this.SetIndex(Math.Min(this.ReaderIndex, newCapacity), Math.Min(this.WriterIndex, newCapacity));
                                return this;
                            }
                        }
                        else
                        {
                            // > 512 (i.e. >= 1024)
                            this.Length = newCapacity;
                            this.SetIndex(Math.Min(this.ReaderIndex, newCapacity), Math.Min(this.WriterIndex, newCapacity));
                            return this;
                        }
                    }
                }
                else
                {
                    return this;
                }
            }

            // Reallocation required.
            this.Chunk.Arena.Reallocate(this, newCapacity, true);
            return this;
        }

        public sealed override IByteBufferAllocator Allocator => this.allocator;

        public sealed override ByteOrder Order => ByteOrder.BigEndian;

        public sealed override IByteBuffer Unwrap() => null;

        protected sealed override void Deallocate()
        {
            if (this.Handle >= 0)
            {
                long handle = this.Handle;
                this.Handle = -1;
                this.Memory = default(T);
                this.Chunk.Arena.Free(this.Chunk, handle, this.MaxLength, this.Cache);
                this.Chunk = null;
                this.allocator = null;
                this.Recycle();
            }
        }

        void Recycle() => this.recyclerHandle.Release(this);

        protected int Idx(int index) => this.Offset + index;
    }
}