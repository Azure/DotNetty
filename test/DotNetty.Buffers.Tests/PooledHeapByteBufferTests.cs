// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    public sealed class PooledHeapByteBufferTests : AbstractPooledByteBufferTests
    {
        protected override IByteBuffer Alloc(int length, int maxCapacity) => PooledByteBufferAllocator.Default.HeapBuffer(length, maxCapacity);
    }
}