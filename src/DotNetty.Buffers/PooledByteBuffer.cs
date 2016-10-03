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
        //private ByteBuffer tmpNioBuf;

        protected PooledByteBuffer(ThreadLocalPool.Handle recyclerHandle, int maxCapacity)
            : base(maxCapacity)
        {
            this.recyclerHandle = recyclerHandle;
        }

        internal void Init(PoolChunk<T> chunk, long handle, int offset, int length, int maxLength, PoolThreadCache<T> cache)
        {
            Contract.Assert(handle >= 0);
            Contract.Assert(chunk != null);

            this.Chunk = chunk;
            this.Handle = handle;
            this.Memory = chunk.Memory;
            this.Offset = offset;
            this.Length = length;
            this.MaxLength = maxLength;
            this.SetIndex(0, 0);
            this.DiscardMarkers();
            //tmpNioBuf = null;
            this.Cache = cache;
        }

        internal void InitUnpooled(PoolChunk<T> chunk, int length)
        {
            Contract.Assert(chunk != null);

            this.Chunk = chunk;
            this.Handle = 0;
            this.Memory = chunk.Memory;
            this.Offset = 0;
            this.Length = this.MaxLength = length;
            this.SetIndex(0, 0);
            //tmpNioBuf = null;
            this.Cache = null;
        }

        public override int Capacity => this.Length;

        public sealed override IByteBuffer AdjustCapacity(int newCapacity)
        {
            this.EnsureAccessible();

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

        public sealed override IByteBufferAllocator Allocator => this.Chunk.Arena.Parent;

        public sealed override ByteOrder Order => ByteOrder.BigEndian;

        public sealed override IByteBuffer Unwrap() => null;

        //protected IByteBuffer internalNioBuffer() {
        //    ByteBuffer tmpNioBuf = this.tmpNioBuf;
        //    if (tmpNioBuf == null)
        //    {
        //        this.tmpNioBuf = tmpNioBuf = newInternalNioBuffer(memory);
        //    }
        //    return tmpNioBuf;
        //}

        //protected abstract ByteBuffer newInternalNioBuffer(T memory);

        protected sealed override void Deallocate()
        {
            if (this.Handle >= 0)
            {
                long handle = this.Handle;
                this.Handle = -1;
                this.Memory = default(T);
                this.Chunk.Arena.Free(this.Chunk, handle, this.MaxLength, this.Cache);
                this.Recycle();
            }
        }

        void Recycle() => this.recyclerHandle.Release(this);

        protected int Idx(int index) => this.Offset + index;
    }
}