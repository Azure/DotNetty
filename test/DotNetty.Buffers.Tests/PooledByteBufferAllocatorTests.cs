// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable UnusedParameter.Local
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local
namespace DotNetty.Buffers.Tests
{
    using System.Collections.Generic;
    using Xunit;

    public class PooledByteBufferAllocatorTests : AbstractByteBufferAllocatorTests
    {
        protected override IByteBufferAllocator NewAllocator(bool preferDirect) => new PooledByteBufferAllocator(preferDirect);

        protected override IByteBufferAllocator NewUnpooledAllocator() => new PooledByteBufferAllocator(0, 0, 8192, 1);

        protected override long ExpectedUsedMemory(IByteBufferAllocator allocator, int capacity)
        {
            return ((PooledByteBufferAllocator)allocator).Metric.ChunkSize;
        }

        protected override long ExpectedUsedMemoryAfterRelease(IByteBufferAllocator allocator, int capacity)
        {
            // This is the case as allocations will start in qInit and chunks in qInit will never be released until
            // these are moved to q000.
            // See https://www.bsdcan.org/2006/papers/jemalloc.pdf
            return ((PooledByteBufferAllocator)allocator).Metric.ChunkSize;
        }

        [Fact]
        public void PooledUnsafeHeapBufferAndUnsafeDirectBuffer()
        {
            var allocator = (PooledByteBufferAllocator)this.NewAllocator(true);
            IByteBuffer directBuffer = allocator.DirectBuffer();
            AssertInstanceOf<PooledUnsafeDirectByteBuffer>(directBuffer);
            directBuffer.Release();

            IByteBuffer heapBuffer = allocator.HeapBuffer();
            AssertInstanceOf<PooledHeapByteBuffer>(heapBuffer);
            heapBuffer.Release();
        }

        [Fact]
        public void ArenaMetricsNoCache() => ArenaMetrics0(new PooledByteBufferAllocator(true, 2, 2, 8192, 11, 0, 0, 0), 100, 0, 100, 100);

        [Fact]
        public void ArenaMetricsCache() => ArenaMetrics0(new PooledByteBufferAllocator(true, 2, 2, 8192, 11, 1000, 1000, 1000), 100, 1, 1, 0);

        static void ArenaMetrics0(PooledByteBufferAllocator allocator, int num, int expectedActive, int expectedAlloc, int expectedDealloc)
        {
            for (int i = 0; i < num; i++)
            {
                Assert.True(allocator.HeapBuffer().Release());
            }

            AssertArenaMetrics(allocator.Metric.HeapArenas(), expectedActive, expectedAlloc, expectedDealloc);
        }

        static void AssertArenaMetrics(IReadOnlyList<IPoolArenaMetric> arenaMetrics, long expectedActive, long expectedAlloc, long expectedDealloc)
        {
            long active = 0;
            long alloc = 0;
            long dealloc = 0;
            foreach (IPoolArenaMetric arena in arenaMetrics)
            {
                active += arena.NumActiveAllocations;
                alloc += arena.NumAllocations;
                dealloc += arena.NumDeallocations;
            }

            Assert.Equal(expectedActive, active);
            Assert.Equal(expectedAlloc, alloc);
            Assert.Equal(expectedDealloc, dealloc);
        }

        [Fact]
        public void PoolChunkListMetric()
        {
            foreach (IPoolArenaMetric arenaMetric in PooledByteBufferAllocator.Default.Metric.HeapArenas())
            {
                AssertPoolChunkListMetric(arenaMetric);
            }
        }

        static void AssertPoolChunkListMetric(IPoolArenaMetric arenaMetric)
        {
            IReadOnlyList<IPoolChunkListMetric> lists = arenaMetric.ChunkLists;
            Assert.Equal(6, lists.Count);
            AssertPoolChunkListMetric(lists[0], 1, 25);
            AssertPoolChunkListMetric(lists[1], 1, 50);
            AssertPoolChunkListMetric(lists[2], 25, 75);
            AssertPoolChunkListMetric(lists[4], 75, 100);
            AssertPoolChunkListMetric(lists[5], 100, 100);
        }

        static void AssertPoolChunkListMetric(IPoolChunkListMetric m, int min, int max)
        {
            Assert.Equal(min, m.MinUsage);
            Assert.Equal(max, m.MaxUsage);
        }

        [Fact]
        public void SmallSubpageMetric()
        {
            var allocator = new PooledByteBufferAllocator(true, 1, 1, 8192, 11, 0, 0, 0);
            IByteBuffer buffer = allocator.HeapBuffer(500);
            try
            {
                IPoolArenaMetric metric = allocator.Metric.HeapArenas()[0];
                IPoolSubpageMetric subpageMetric = metric.SmallSubpages[0];
                Assert.Equal(1, subpageMetric.MaxNumElements - subpageMetric.NumAvailable);
            }
            finally
            {
                buffer.Release();
            }
        }

        [Fact]
        public void TinySubpageMetric()
        {
            var allocator = new PooledByteBufferAllocator(true, 1, 1, 8192, 11, 0, 0, 0);
            IByteBuffer buffer = allocator.HeapBuffer(1);
            try
            {
                IPoolArenaMetric metric = allocator.Metric.HeapArenas()[0];
                IPoolSubpageMetric subpageMetric = metric.TinySubpages[0];
                Assert.Equal(1, subpageMetric.MaxNumElements - subpageMetric.NumAvailable);
            }
            finally
            {
                buffer.Release();
            }
        }

        [Fact]
        public void AllocNotNull()
        {
            var allocator = new PooledByteBufferAllocator(true, 1, 1, 8192, 11, 0, 0, 0);
            // Huge allocation
            AllocNotNull(allocator, allocator.Metric.ChunkSize + 1);
            // Normal allocation
            AllocNotNull(allocator, 1024);
            // Small allocation
            AllocNotNull(allocator, 512);
            // Tiny allocation
            AllocNotNull(allocator, 1);
        }

        static void AllocNotNull(PooledByteBufferAllocator allocator, int capacity)
        {
            IByteBuffer buffer = allocator.HeapBuffer(capacity);
            Assert.NotNull(buffer.Allocator);
            Assert.True(buffer.Release());
            Assert.NotNull(buffer.Allocator);
        }

        [Fact]
        public void FreePoolChunk()
        {
            const int ChunkSize = 16 * 1024 * 1024;
            var allocator = new PooledByteBufferAllocator(true, 1, 0, 8192, 11, 0, 0, 0);
            IByteBuffer buffer = allocator.HeapBuffer(ChunkSize);
            IReadOnlyList<IPoolArenaMetric> arenas = allocator.Metric.HeapArenas();
            Assert.Equal(1, arenas.Count);
            IReadOnlyList<IPoolChunkListMetric> lists = arenas[0].ChunkLists;
            Assert.Equal(6, lists.Count);

            Assert.False(lists[0].GetEnumerator().MoveNext());
            Assert.False(lists[1].GetEnumerator().MoveNext());
            Assert.False(lists[2].GetEnumerator().MoveNext());
            Assert.False(lists[3].GetEnumerator().MoveNext());
            Assert.False(lists[4].GetEnumerator().MoveNext());

            // Must end up in the 6th PoolChunkList
            Assert.True(lists[5].GetEnumerator().MoveNext());
            Assert.True(buffer.Release());

            // Should be completely removed and so all PoolChunkLists must be empty
            Assert.False(lists[0].GetEnumerator().MoveNext());
            Assert.False(lists[1].GetEnumerator().MoveNext());
            Assert.False(lists[2].GetEnumerator().MoveNext());
            Assert.False(lists[3].GetEnumerator().MoveNext());
            Assert.False(lists[4].GetEnumerator().MoveNext());
            Assert.False(lists[5].GetEnumerator().MoveNext());
        }
    }
}
