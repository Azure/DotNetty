// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using DotNetty.Common.Utilities;

    enum SizeClass
    {
        Tiny,
        Small,
        Normal
    }

    abstract class PoolArena<T> : IPoolArenaMetric
    {
        internal static readonly int NumTinySubpagePools = 512 >> 4;

        internal readonly PooledByteBufferAllocator Parent;

        readonly int maxOrder;
        readonly int maxChunkCount;
        internal readonly int PageSize;
        internal readonly int PageShifts;
        internal readonly int ChunkSize;
        internal readonly int SubpageOverflowMask;
        internal readonly int NumSmallSubpagePools;
        readonly PoolSubpage<T>[] tinySubpagePools;
        readonly PoolSubpage<T>[] smallSubpagePools;

        readonly PoolChunkList<T> q050;
        readonly PoolChunkList<T> q025;
        readonly PoolChunkList<T> q000;
        readonly PoolChunkList<T> qInit;
        readonly PoolChunkList<T> q075;
        readonly PoolChunkList<T> q100;

        readonly List<IPoolChunkListMetric> chunkListMetrics;

        int chunkCount;

        // Metrics for allocations and deallocations
        long allocationsTiny;
        long allocationsSmall;
        long allocationsNormal;
        // We need to use the LongCounter here as this is not guarded via synchronized block.
        long allocationsHuge;
        long activeBytesHuge;

        long deallocationsTiny;
        long deallocationsSmall;
        long deallocationsNormal;
        // We need to use the LongCounter here as this is not guarded via synchronized block.
        long deallocationsHuge;

        // Number of thread caches backed by this arena.
        int numThreadCaches;

        // TODO: Test if adding padding helps under contention
        //private long pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7;

        protected PoolArena(PooledByteBufferAllocator parent, int pageSize, int maxOrder, int pageShifts, int chunkSize, int maxChunkCount)
        {
            this.Parent = parent;
            this.PageSize = pageSize;
            this.maxOrder = maxOrder;
            this.PageShifts = pageShifts;
            this.ChunkSize = chunkSize;
            this.maxChunkCount = maxChunkCount;
            this.SubpageOverflowMask = ~(pageSize - 1);
            this.tinySubpagePools = this.NewSubpagePoolArray(NumTinySubpagePools);
            for (int i = 0; i < this.tinySubpagePools.Length; i++)
            {
                this.tinySubpagePools[i] = this.NewSubpagePoolHead(pageSize);
            }

            this.NumSmallSubpagePools = pageShifts - 9;
            this.smallSubpagePools = this.NewSubpagePoolArray(this.NumSmallSubpagePools);
            for (int i = 0; i < this.smallSubpagePools.Length; i++)
            {
                this.smallSubpagePools[i] = this.NewSubpagePoolHead(pageSize);
            }

            this.q100 = new PoolChunkList<T>(null, 100, int.MaxValue, chunkSize);
            this.q075 = new PoolChunkList<T>(this.q100, 75, 100, chunkSize);
            this.q050 = new PoolChunkList<T>(this.q075, 50, 100, chunkSize);
            this.q025 = new PoolChunkList<T>(this.q050, 25, 75, chunkSize);
            this.q000 = new PoolChunkList<T>(this.q025, 1, 50, chunkSize);
            this.qInit = new PoolChunkList<T>(this.q000, int.MinValue, 25, chunkSize);

            this.q100.PrevList(this.q075);
            this.q075.PrevList(this.q050);
            this.q050.PrevList(this.q025);
            this.q025.PrevList(this.q000);
            this.q000.PrevList(null);
            this.qInit.PrevList(this.qInit);

            var metrics = new List<IPoolChunkListMetric>(6);
            metrics.Add(this.qInit);
            metrics.Add(this.q000);
            metrics.Add(this.q025);
            metrics.Add(this.q050);
            metrics.Add(this.q075);
            metrics.Add(this.q100);
            this.chunkListMetrics = metrics;
        }

        public int NumThreadCaches => Volatile.Read(ref this.numThreadCaches);

        public void RegisterThreadCache() => Interlocked.Increment(ref this.numThreadCaches);

        public void DeregisterThreadCache() => Interlocked.Decrement(ref this.numThreadCaches);

        PoolSubpage<T> NewSubpagePoolHead(int pageSize)
        {
            var head = new PoolSubpage<T>(pageSize);
            head.Prev = head;
            head.Next = head;
            return head;
        }

        PoolSubpage<T>[] NewSubpagePoolArray(int size) => new PoolSubpage<T>[size];

        internal PooledByteBuffer<T> Allocate(PoolThreadCache<T> cache, int reqCapacity, int maxCapacity)
        {
            PooledByteBuffer<T> buf = this.NewByteBuf(maxCapacity);
            this.Allocate(cache, buf, reqCapacity);
            return buf;
        }

        internal static int TinyIdx(int normCapacity) => normCapacity.RightUShift(4);

        internal static int SmallIdx(int normCapacity)
        {
            int tableIdx = 0;
            int i = normCapacity.RightUShift(10);
            while (i != 0)
            {
                i = i.RightUShift(1);
                tableIdx++;
            }
            return tableIdx;
        }

        // capacity < pageSize
        internal bool IsTinyOrSmall(int normCapacity) => (normCapacity & this.SubpageOverflowMask) == 0;

        // normCapacity < 512
        internal static bool IsTiny(int normCapacity) => (normCapacity & 0xFFFFFE00) == 0;

        void Allocate(PoolThreadCache<T> cache, PooledByteBuffer<T> buf, int reqCapacity)
        {
            int normCapacity = this.NormalizeCapacity(reqCapacity);
            if (this.IsTinyOrSmall(normCapacity))
            {
                // capacity < pageSize
                int tableIdx;
                PoolSubpage<T>[] table;
                bool tiny = IsTiny(normCapacity);
                if (tiny)
                {
                    // < 512
                    if (cache.AllocateTiny(this, buf, reqCapacity, normCapacity))
                    {
                        // was able to allocate out of the cache so move on
                        return;
                    }
                    tableIdx = TinyIdx(normCapacity);
                    table = this.tinySubpagePools;
                }
                else
                {
                    if (cache.AllocateSmall(this, buf, reqCapacity, normCapacity))
                    {
                        // was able to allocate out of the cache so move on
                        return;
                    }
                    tableIdx = SmallIdx(normCapacity);
                    table = this.smallSubpagePools;
                }

                PoolSubpage<T> head = table[tableIdx];

                /**
                 * Synchronize on the head. This is needed as {@link PoolSubpage#allocate()} and
                 * {@link PoolSubpage#free(int)} may modify the doubly linked list as well.
                 */
                lock (head)
                {
                    PoolSubpage<T> s = head.Next;
                    if (s != head)
                    {
                        Contract.Assert(s.DoNotDestroy && s.ElemSize == normCapacity);
                        long handle = s.Allocate();
                        Contract.Assert(handle >= 0);
                        s.Chunk.InitBufWithSubpage(buf, handle, reqCapacity);

                        if (tiny)
                        {
                            ++this.allocationsTiny;
                        }
                        else
                        {
                            ++this.allocationsSmall;
                        }
                        return;
                    }
                }
                this.AllocateNormal(buf, reqCapacity, normCapacity);
                return;
            }
            if (normCapacity <= this.ChunkSize)
            {
                if (cache.AllocateNormal(this, buf, reqCapacity, normCapacity))
                {
                    // was able to allocate out of the cache so move on
                    return;
                }
                this.AllocateNormal(buf, reqCapacity, normCapacity);
            }
            else
            {
                // Huge allocations are never served via the cache so just call allocateHuge
                this.AllocateHuge(buf, reqCapacity);
            }
        }

        void AllocateNormal(PooledByteBuffer<T> buf, int reqCapacity, int normCapacity)
        {
            lock (this)
            {
                if (this.q050.Allocate(buf, reqCapacity, normCapacity) || this.q025.Allocate(buf, reqCapacity, normCapacity)
                    || this.q000.Allocate(buf, reqCapacity, normCapacity) || this.qInit.Allocate(buf, reqCapacity, normCapacity)
                    || this.q075.Allocate(buf, reqCapacity, normCapacity))
                {
                    ++this.allocationsNormal;
                    return;
                }

                if (this.chunkCount < this.maxChunkCount)
                {
                    // Add a new chunk.
                    PoolChunk<T> c = this.NewChunk(this.PageSize, this.maxOrder, this.PageShifts, this.ChunkSize);
                    this.chunkCount++;
                    long handle = c.Allocate(normCapacity);
                    ++this.allocationsNormal;
                    Contract.Assert(handle > 0);
                    c.InitBuf(buf, handle, reqCapacity);
                    this.qInit.Add(c);
                    return;
                }
            }

            PoolChunk<T> chunk = this.NewUnpooledChunk(reqCapacity, false);
            buf.InitUnpooled(chunk, reqCapacity);
            Interlocked.Increment(ref this.allocationsNormal);
        }

        void AllocateHuge(PooledByteBuffer<T> buf, int reqCapacity)
        {
            PoolChunk<T> chunk = this.NewUnpooledChunk(reqCapacity, true);
            Interlocked.Add(ref this.activeBytesHuge, chunk.ChunkSize);
            buf.InitUnpooled(chunk, reqCapacity);
            Interlocked.Increment(ref this.allocationsHuge);
        }

        internal void Free(PoolChunk<T> chunk, long handle, int normCapacity, PoolThreadCache<T> cache)
        {
            if (chunk.Unpooled)
            {
                int size = chunk.ChunkSize;
                this.DestroyChunk(chunk);
                switch (chunk.Origin)
                {
                    case PoolChunk<T>.PoolChunkOrigin.UnpooledNormal:
                        Interlocked.Decrement(ref this.deallocationsNormal);
                        break;
                    case PoolChunk<T>.PoolChunkOrigin.UnpooledHuge:
                        Interlocked.Add(ref this.activeBytesHuge, -size);
                        Interlocked.Decrement(ref this.deallocationsHuge);
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported PoolChunk.Origin: " + chunk.Origin);
                }
            }
            else
            {
                SizeClass sc = this.SizeClass(normCapacity);
                if (cache != null && cache.Add(this, chunk, handle, normCapacity, sc))
                {
                    // cached so not free it.
                    return;
                }

                this.FreeChunk(chunk, handle, sc);
            }
        }

        SizeClass SizeClass(int normCapacity)
        {
            if (!this.IsTinyOrSmall(normCapacity))
            {
                return Buffers.SizeClass.Normal;
            }
            return IsTiny(normCapacity) ? Buffers.SizeClass.Tiny : Buffers.SizeClass.Small;
        }

        internal void FreeChunk(PoolChunk<T> chunk, long handle, SizeClass sizeClass)
        {
            bool mustDestroyChunk;
            lock (this)
            {
                switch (sizeClass)
                {
                    case Buffers.SizeClass.Normal:
                        ++this.deallocationsNormal;
                        break;
                    case Buffers.SizeClass.Small:
                        ++this.deallocationsSmall;
                        break;
                    case Buffers.SizeClass.Tiny:
                        ++this.deallocationsTiny;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                mustDestroyChunk = !chunk.Parent.Free(chunk, handle);
            }
            if (mustDestroyChunk)
            {
                // destroyChunk not need to be called while holding the synchronized lock.
                this.DestroyChunk(chunk);
            }
        }

        internal PoolSubpage<T> FindSubpagePoolHead(int elemSize)
        {
            int tableIdx;
            PoolSubpage<T>[] table;
            if (IsTiny(elemSize))
            {
                // < 512
                tableIdx = elemSize.RightUShift(4);
                table = this.tinySubpagePools;
            }
            else
            {
                tableIdx = 0;
                elemSize = elemSize.RightUShift(10);
                while (elemSize != 0)
                {
                    elemSize = elemSize.RightUShift(1);
                    tableIdx++;
                }
                table = this.smallSubpagePools;
            }

            return table[tableIdx];
        }

        internal int NormalizeCapacity(int reqCapacity)
        {
            Contract.Requires(reqCapacity >= 0);

            if (reqCapacity >= this.ChunkSize)
            {
                return reqCapacity;
            }

            if (!IsTiny(reqCapacity))
            {
                // >= 512
                // Doubled

                int normalizedCapacity = reqCapacity;
                normalizedCapacity--;
                normalizedCapacity |= normalizedCapacity.RightUShift(1);
                normalizedCapacity |= normalizedCapacity.RightUShift(2);
                normalizedCapacity |= normalizedCapacity.RightUShift(4);
                normalizedCapacity |= normalizedCapacity.RightUShift(8);
                normalizedCapacity |= normalizedCapacity.RightUShift(16);
                normalizedCapacity++;

                if (normalizedCapacity < 0)
                {
                    normalizedCapacity = normalizedCapacity.RightUShift(1);
                }

                return normalizedCapacity;
            }

            // Quantum-spaced
            if ((reqCapacity & 15) == 0)
            {
                return reqCapacity;
            }

            return (reqCapacity & ~15) + 16;
        }

        internal void Reallocate(PooledByteBuffer<T> buf, int newCapacity, bool freeOldMemory)
        {
            Contract.Requires(newCapacity >= 0 && newCapacity <= buf.MaxCapacity);

            int oldCapacity = buf.Length;
            if (oldCapacity == newCapacity)
            {
                return;
            }

            PoolChunk<T> oldChunk = buf.Chunk;
            long oldHandle = buf.Handle;
            T oldMemory = buf.Memory;
            int oldOffset = buf.Offset;
            int oldMaxLength = buf.MaxLength;
            int readerIndex = buf.ReaderIndex;
            int writerIndex = buf.WriterIndex;

            this.Allocate(this.Parent.ThreadCache<T>(), buf, newCapacity);
            if (newCapacity > oldCapacity)
            {
                this.MemoryCopy(
                    oldMemory, oldOffset,
                    buf.Memory, buf.Offset, oldCapacity);
            }
            else if (newCapacity < oldCapacity)
            {
                if (readerIndex < newCapacity)
                {
                    if (writerIndex > newCapacity)
                    {
                        writerIndex = newCapacity;
                    }
                    this.MemoryCopy(
                        oldMemory, oldOffset + readerIndex,
                        buf.Memory, buf.Offset + readerIndex, writerIndex - readerIndex);
                }
                else
                {
                    readerIndex = writerIndex = newCapacity;
                }
            }

            buf.SetIndex(readerIndex, writerIndex);

            if (freeOldMemory)
            {
                this.Free(oldChunk, oldHandle, oldMaxLength, buf.Cache);
            }
        }

        public int NumTinySubpages => this.tinySubpagePools.Length;

        public int NumSmallSubpages => this.smallSubpagePools.Length;

        public int NumChunkLists => this.chunkListMetrics.Count;

        public IReadOnlyList<IPoolSubpageMetric> TinySubpages => SubPageMetricList(this.tinySubpagePools);

        public IReadOnlyList<IPoolSubpageMetric> SmallSubpages => SubPageMetricList(this.smallSubpagePools);

        public IReadOnlyList<IPoolChunkListMetric> ChunkLists => this.chunkListMetrics;

        static List<IPoolSubpageMetric> SubPageMetricList(PoolSubpage<T>[] pages)
        {
            var metrics = new List<IPoolSubpageMetric>();
            for (int i = 1; i < pages.Length; i++)
            {
                PoolSubpage<T> head = pages[i];
                if (head.Next == head)
                {
                    continue;
                }
                PoolSubpage<T> s = head.Next;
                for (;;)
                {
                    metrics.Add(s);
                    s = s.Next;
                    if (s == head)
                    {
                        break;
                    }
                }
            }
            return metrics;
        }

        public long NumAllocations => this.allocationsTiny + this.allocationsSmall + this.allocationsNormal + this.NumHugeAllocations;

        public long NumTinyAllocations => this.allocationsTiny;

        public long NumSmallAllocations => this.allocationsSmall;

        public long NumNormalAllocations => this.allocationsNormal;

        public long NumDeallocations => this.deallocationsTiny + this.deallocationsSmall + this.deallocationsNormal + this.NumHugeDeallocations;

        public long NumTinyDeallocations => this.deallocationsTiny;

        public long NumSmallDeallocations => this.deallocationsSmall;

        public long NumNormalDeallocations => this.deallocationsNormal;

        public long NumHugeAllocations => Volatile.Read(ref this.allocationsHuge);

        public long NumHugeDeallocations => Volatile.Read(ref this.deallocationsHuge);

        public long NumActiveAllocations => Math.Max(this.NumAllocations - this.NumDeallocations, 0);

        public long NumActiveTinyAllocations => Math.Max(this.NumTinyAllocations - this.NumTinyDeallocations, 0);

        public long NumActiveSmallAllocations => Math.Max(this.NumSmallAllocations - this.NumSmallDeallocations, 0);

        public long NumActiveNormalAllocations
        {
            get
            {
                long val;
                lock (this)
                {
                    val = this.NumNormalAllocations - this.NumNormalDeallocations;
                }
                return Math.Max(val, 0);
            }
        }

        public long NumActiveHugeAllocations => Math.Max(this.NumHugeAllocations - this.NumHugeDeallocations, 0);

        public long NumActiveBytes
        {
            get
            {
                long val = Volatile.Read(ref this.activeBytesHuge);
                lock (this)
                {
                    for (int i = 0; i < this.chunkListMetrics.Count; i++)
                    {
                        foreach (IPoolChunkMetric m in this.chunkListMetrics[i])
                        {
                            val += m.ChunkSize;
                        }
                    }
                }
                return Math.Max(0, val);
            }
        }

        protected abstract PoolChunk<T> NewChunk(int pageSize, int maxOrder, int pageShifts, int chunkSize);

        protected abstract PoolChunk<T> NewUnpooledChunk(int capacity, bool huge);

        protected abstract PooledByteBuffer<T> NewByteBuf(int maxCapacity);

        protected abstract void MemoryCopy(T src, int srcOffset, T dst, int dstOffset, int length);

        protected abstract void DestroyChunk(PoolChunk<T> chunk);

        public override string ToString()
        {
            lock (this)
            {
                StringBuilder buf = new StringBuilder()
                    .Append("Chunk(s) at 0~25%:")
                    .Append(StringUtil.Newline)
                    .Append(this.qInit)
                    .Append(StringUtil.Newline)
                    .Append("Chunk(s) at 0~50%:")
                    .Append(StringUtil.Newline)
                    .Append(this.q000)
                    .Append(StringUtil.Newline)
                    .Append("Chunk(s) at 25~75%:")
                    .Append(StringUtil.Newline)
                    .Append(this.q025)
                    .Append(StringUtil.Newline)
                    .Append("Chunk(s) at 50~100%:")
                    .Append(StringUtil.Newline)
                    .Append(this.q050)
                    .Append(StringUtil.Newline)
                    .Append("Chunk(s) at 75~100%:")
                    .Append(StringUtil.Newline)
                    .Append(this.q075)
                    .Append(StringUtil.Newline)
                    .Append("Chunk(s) at 100%:")
                    .Append(StringUtil.Newline)
                    .Append(this.q100)
                    .Append(StringUtil.Newline)
                    .Append("tiny subpages:");
                for (int i = 1; i < this.tinySubpagePools.Length; i++)
                {
                    PoolSubpage<T> head = this.tinySubpagePools[i];
                    if (head.Next == head)
                    {
                        continue;
                    }

                    buf.Append(StringUtil.Newline)
                        .Append(i)
                        .Append(": ");
                    PoolSubpage<T> s = head.Next;
                    for (;;)
                    {
                        buf.Append(s);
                        s = s.Next;
                        if (s == head)
                        {
                            break;
                        }
                    }
                }
                buf.Append(StringUtil.Newline)
                    .Append("small subpages:");
                for (int i = 1; i < this.smallSubpagePools.Length; i++)
                {
                    PoolSubpage<T> head = this.smallSubpagePools[i];
                    if (head.Next == head)
                    {
                        continue;
                    }

                    buf.Append(StringUtil.Newline)
                        .Append(i)
                        .Append(": ");
                    PoolSubpage<T> s = head.Next;
                    for (;;)
                    {
                        buf.Append(s);
                        s = s.Next;
                        if (s == head)
                        {
                            break;
                        }
                    }
                }
                buf.Append(StringUtil.Newline);

                return buf.ToString();
            }
        }
    }

    sealed class HeapArena : PoolArena<byte[]>
    {
        public HeapArena(PooledByteBufferAllocator parent, int pageSize, int maxOrder, int pageShifts, int chunkSize, int maxChunkCount)
            : base(parent, pageSize, maxOrder, pageShifts, chunkSize, maxChunkCount)
        {
        }

        protected override PoolChunk<byte[]> NewChunk(int pageSize, int maxOrder, int pageShifts, int chunkSize) => new PoolChunk<byte[]>(this, new byte[chunkSize], pageSize, maxOrder, pageShifts, chunkSize);

        protected override PoolChunk<byte[]> NewUnpooledChunk(int capacity, bool huge) => new PoolChunk<byte[]>(this, new byte[capacity], capacity, huge);

        protected override void DestroyChunk(PoolChunk<byte[]> chunk)
        {
            // Rely on GC.
        }

        protected override PooledByteBuffer<byte[]> NewByteBuf(int maxCapacity) => PooledHeapByteBuffer.NewInstance(maxCapacity);

        protected override void MemoryCopy(byte[] src, int srcOffset, byte[] dst, int dstOffset, int length)
        {
            if (length == 0)
            {
                return;
            }

            Array.Copy(src, srcOffset, dst, dstOffset, length);
        }
    }
}