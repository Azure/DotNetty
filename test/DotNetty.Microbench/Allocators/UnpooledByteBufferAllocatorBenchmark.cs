// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench.Allocators
{
    using DotNetty.Buffers;

    public class UnpooledHeapByteBufferAllocatorBenchmark : AbstractByteBufferAllocatorBenchmark
    {
        public UnpooledHeapByteBufferAllocatorBenchmark() : base(new UnpooledByteBufferAllocator(true))
        {
        }
    }
}
