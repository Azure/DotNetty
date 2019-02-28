// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench.Allocators
{
    using DotNetty.Buffers;

    public class PooledHeapByteBufferAllocatorBenchmark : AbstractByteBufferAllocatorBenchmark
    {
        public PooledHeapByteBufferAllocatorBenchmark() 
            : base( new PooledByteBufferAllocator(true, 4, 4, 8192, 11, 0, 0, 0)) // Disable thread-local cache
        {
        }
    }
}
