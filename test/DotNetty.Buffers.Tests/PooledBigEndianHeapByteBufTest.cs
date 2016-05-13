// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    public class PooledBigEndianHeapByteBufTest : AbstractPooledByteBufTest
    {
        protected override IByteBuffer Alloc(int length) => PooledByteBufferAllocator.Default.Buffer(length);
    }
}