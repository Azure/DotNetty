// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using Xunit;

    public class PooledBufferAllocatorTests
    {
        [Theory]
        [InlineData(16 * 1024, 10, new[] {16 * 1024 - 100, 8 * 1024})]
        [InlineData(16 * 1024, 0, new[] { 16 * 1024 - 100, 8 * 1024 })]
        [InlineData(1024, 2 * 1024, new[] { 16 * 1024 - 100, 8 * 1024 })]
        [InlineData(1024, 0, new[] { 1024, 1 })]
        [InlineData(1024, 0, new[] { 1024, 0, 10 * 1024 })]
        public void PooledBufferGrowTest(int bufferSize, int startSize, int[] writeSizes)
        {
            var alloc = new PooledByteBufferAllocator(bufferSize, int.MaxValue);
            IByteBuffer buffer = alloc.Buffer(startSize);
            int wrote = 0;
            foreach (int size in writeSizes)
            {
                buffer.WriteBytes(Unpooled.WrappedBuffer(new byte[size]));
                wrote += size;
            }

            Assert.Equal(wrote, buffer.ReadableBytes);
        }
    }
}