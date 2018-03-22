// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using Xunit;

    public class PoolArenaTests
    {
        [Fact]
        public void NormalizeCapacity()
        {
            var arena = new HeapArena(null, 0, 0, 9, 999999);
            int[] reqCapacities = { 0, 15, 510, 1024, 1023, 1025 };
            int[] expectedResult = { 0, 16, 512, 1024, 1024, 2048 };
            for (int i = 0; i < reqCapacities.Length; i++)
            {
                Assert.Equal(expectedResult[i], arena.NormalizeCapacity(reqCapacities[i]));
            }
        }

        [Fact]
        public void AllocationCounter()
        {
            var allocator = new PooledByteBufferAllocator(
                true,   // preferDirect
                0,      // nHeapArena
                1,      // nDirectArena
                8192,   // pageSize
                11,     // maxOrder
                0,      // tinyCacheSize
                0,      // smallCacheSize
                0      // normalCacheSize
            );

            // create tiny buffer
            IByteBuffer b1 = allocator.Buffer(24);
            // create small buffer
            IByteBuffer b2 = allocator.Buffer(800);
            // create normal buffer
            IByteBuffer b3 = allocator.Buffer(8192 * 2);

            Assert.NotNull(b1);
            Assert.NotNull(b2);
            Assert.NotNull(b3);

            // then release buffer to deallocated memory while threadlocal cache has been disabled
            // allocations counter value must equals deallocations counter value
            Assert.True(b1.Release());
            Assert.True(b2.Release());
            Assert.True(b3.Release());

            Assert.True(allocator.DirectArenas().Count >= 1);
            IPoolArenaMetric metric = allocator.DirectArenas()[0];

            Assert.Equal(3, metric.NumDeallocations);
            Assert.Equal(3, metric.NumAllocations);

            Assert.Equal(1, metric.NumTinyDeallocations);
            Assert.Equal(1, metric.NumTinyAllocations);
            Assert.Equal(1, metric.NumSmallDeallocations);
            Assert.Equal(1, metric.NumSmallAllocations);
            Assert.Equal(1, metric.NumNormalDeallocations);
            Assert.Equal(1, metric.NumNormalAllocations);
        }
    }
}
