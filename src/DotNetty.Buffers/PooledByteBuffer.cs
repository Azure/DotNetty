// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Diagnostics.Contracts;
    using DotNetty.Common;

    class PooledByteBuffer : UnpooledHeapByteBuffer
    {
        readonly ThreadLocalPool.Handle returnHandle;
        int length;
        readonly byte[] pooledArray;

        public PooledByteBuffer(ThreadLocalPool.Handle returnHandle, IByteBufferAllocator allocator, int maxFixedCapacity, int maxCapacity)
            : this(returnHandle, allocator, new byte[maxFixedCapacity], maxCapacity)
        {
        }

        PooledByteBuffer(ThreadLocalPool.Handle returnHandle, IByteBufferAllocator allocator, byte[] pooledArray, int maxCapacity)
            : base(allocator, pooledArray, 0, 0, maxCapacity)
        {
            this.length = pooledArray.Length;
            this.returnHandle = returnHandle;
            this.pooledArray = pooledArray;
        }

        internal void Init()
        {
            this.SetIndex(0, 0);
            this.DiscardMarkers();
        }

        public override int Capacity
        {
            get { return this.length; }
        }

        public override IByteBuffer AdjustCapacity(int newCapacity)
        {
            this.EnsureAccessible();
            Contract.Requires(newCapacity >= 0 && newCapacity <= this.MaxCapacity);

            if (this.Array == this.pooledArray)
            {
                if (newCapacity > this.length)
                {
                    if (newCapacity < this.pooledArray.Length)
                    {
                        this.length = newCapacity;
                        return this;
                    }
                }
                else if (newCapacity < this.length)
                {
                    this.length = newCapacity;
                    this.SetIndex(Math.Min(this.ReaderIndex, newCapacity), Math.Min(this.WriterIndex, newCapacity));
                    return this;
                }
                else
                {
                    return this;
                }
            }

            // todo: fall through to here means buffer pool is being used inefficiently. consider providing insight on such events
            base.AdjustCapacity(newCapacity);
            this.length = newCapacity;
            return this;
        }

        public override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            IByteBuffer copy = this.Allocator.Buffer(length, this.MaxCapacity);
            copy.WriteBytes(this.Array, this.ArrayOffset + index, length);
            return copy;
        }

        protected override void Deallocate()
        {
            this.SetArray(this.pooledArray); // release byte array that has been allocated in response to capacity adjustment to a value higher than max pooled size
            this.SetReferenceCount(1); // ensures that next time buffer is pulled from the pool it has "fresh" ref count
            this.returnHandle.Release(this);
        }
    }
}