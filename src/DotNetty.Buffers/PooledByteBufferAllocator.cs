// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System.Diagnostics.Contracts;
    using DotNetty.Common;

    public class PooledByteBufferAllocator : AbstractByteBufferAllocator
    {
        readonly ThreadLocalPool<PooledByteBuffer> pool;

        public PooledByteBufferAllocator(int maxPooledBufSize, int maxLocalPoolSize)
        {
            Contract.Requires(maxLocalPoolSize > maxPooledBufSize);

            this.MaxPooledBufSize = maxPooledBufSize;
            this.pool = new ThreadLocalPool<PooledByteBuffer>(
                handle => new PooledByteBuffer(handle, this, maxPooledBufSize, int.MaxValue),
                maxLocalPoolSize / maxPooledBufSize,
                false);
        }

        public int MaxPooledBufSize { get; private set; }

        protected override IByteBuffer NewBuffer(int initialCapacity, int maxCapacity)
        {
            if (initialCapacity > this.MaxPooledBufSize)
            {
                return new UnpooledHeapByteBuffer(this, initialCapacity, maxCapacity);
            }

            PooledByteBuffer buffer = this.pool.Take();
            buffer.Init();

            return ToLeakAwareBuffer(buffer);
        }
    }
}