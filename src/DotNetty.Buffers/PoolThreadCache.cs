// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using DotNetty.Common;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using Thread = DotNetty.Common.Concurrency.XThread;

    /// <summary>
    ///     Acts a Thread cache for allocations. This implementation is moduled after
    ///     <a href="http://people.freebsd.org/~jasone/jemalloc/bsdcan2006/jemalloc.pdf">jemalloc</a> and the descripted
    ///     technics of
    ///     <a
    ///         href="https://www.facebook.com/notes/facebook-engineering/scalable-memory-allocation-using-jemalloc/
    /// 480222803919">
    ///         Scalable
    ///         memory allocation using jemalloc
    ///     </a>
    ///     .
    /// </summary>
    sealed class PoolThreadCache<T>
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<PoolThreadCache<T>>();

        internal readonly PoolArena<T> HeapArena;
        internal readonly PoolArena<T> DirectArena;

        // Hold the caches for the different size classes, which are tiny, small and normal.
        readonly MemoryRegionCache[] tinySubPageHeapCaches;
        readonly MemoryRegionCache[] smallSubPageHeapCaches;
        readonly MemoryRegionCache[] tinySubPageDirectCaches;
        readonly MemoryRegionCache[] smallSubPageDirectCaches;
        readonly MemoryRegionCache[] normalHeapCaches;
        readonly MemoryRegionCache[] normalDirectCaches;

        // Used for bitshifting when calculate the index of normal caches later
        readonly int numShiftsNormalDirect;
        readonly int numShiftsNormalHeap;
        readonly int freeSweepAllocationThreshold;

        int allocations;

        readonly Thread deathWatchThread;
        readonly Action freeTask;

        // TODO: Test if adding padding helps under contention
        //private long pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7;

        internal PoolThreadCache(PoolArena<T> heapArena, PoolArena<T> directArena,
            int tinyCacheSize, int smallCacheSize, int normalCacheSize,
            int maxCachedBufferCapacity, int freeSweepAllocationThreshold)
        {
            Contract.Requires(maxCachedBufferCapacity >= 0);
            Contract.Requires(freeSweepAllocationThreshold > 0);

            this.freeSweepAllocationThreshold = freeSweepAllocationThreshold;
            this.HeapArena = heapArena;
            this.DirectArena = directArena;
            if (directArena != null)
            {
                this.tinySubPageDirectCaches = CreateSubPageCaches(
                    tinyCacheSize, PoolArena<T>.NumTinySubpagePools, SizeClass.Tiny);
                this.smallSubPageDirectCaches = CreateSubPageCaches(
                    smallCacheSize, directArena.NumSmallSubpagePools, SizeClass.Small);

                this.numShiftsNormalDirect = Log2(directArena.PageSize);
                this.normalDirectCaches = CreateNormalCaches(
                    normalCacheSize, maxCachedBufferCapacity, directArena);

                directArena.IncrementNumThreadCaches();
            }
            else
            {
                // No directArea is configured so just null out all caches
                this.tinySubPageDirectCaches = null;
                this.smallSubPageDirectCaches = null;
                this.normalDirectCaches = null;
                this.numShiftsNormalDirect = -1;
            }
            if (heapArena != null)
            {
                // Create the caches for the heap allocations
                this.tinySubPageHeapCaches = CreateSubPageCaches(
                    tinyCacheSize, PoolArena<T>.NumTinySubpagePools, SizeClass.Tiny);
                this.smallSubPageHeapCaches = CreateSubPageCaches(
                    smallCacheSize, heapArena.NumSmallSubpagePools, SizeClass.Small);

                this.numShiftsNormalHeap = Log2(heapArena.PageSize);
                this.normalHeapCaches = CreateNormalCaches(
                    normalCacheSize, maxCachedBufferCapacity, heapArena);

                heapArena.IncrementNumThreadCaches();
            }
            else
            {
                // No heapArea is configured so just null out all caches
                this.tinySubPageHeapCaches = null;
                this.smallSubPageHeapCaches = null;
                this.normalHeapCaches = null;
                this.numShiftsNormalHeap = -1;
            }

            // We only need to watch the thread when any cache is used.
            if (this.tinySubPageDirectCaches != null || this.smallSubPageDirectCaches != null || this.normalDirectCaches != null
                || this.tinySubPageHeapCaches != null || this.smallSubPageHeapCaches != null || this.normalHeapCaches != null)
            {
                this.freeTask = this.Free0;
                this.deathWatchThread = Thread.CurrentThread;

                // The thread-local cache will keep a list of pooled buffers which must be returned to
                // the pool when the thread is not alive anymore.
                ThreadDeathWatcher.Watch(this.deathWatchThread, this.freeTask);
            }
            else
            {
                this.freeTask = null;
                this.deathWatchThread = null;
            }
        }

        static MemoryRegionCache[] CreateSubPageCaches(
            int cacheSize, int numCaches, SizeClass sizeClass)
        {
            if (cacheSize > 0)
            {
                var cache = new MemoryRegionCache[numCaches];
                for (int i = 0; i < cache.Length; i++)
                {
                    // TODO: maybe use cacheSize / cache.length
                    cache[i] = new SubPageMemoryRegionCache(cacheSize, sizeClass);
                }
                return cache;
            }
            else
            {
                return null;
            }
        }

        static MemoryRegionCache[] CreateNormalCaches(
            int cacheSize, int maxCachedBufferCapacity, PoolArena<T> area)
        {
            if (cacheSize > 0)
            {
                int max = Math.Min(area.ChunkSize, maxCachedBufferCapacity);
                int arraySize = Math.Max(1, Log2(max / area.PageSize) + 1);

                var cache = new MemoryRegionCache[arraySize];
                for (int i = 0; i < cache.Length; i++)
                {
                    cache[i] = new NormalMemoryRegionCache(cacheSize);
                }
                return cache;
            }
            else
            {
                return null;
            }
        }

        static int Log2(int val)
        {
            // todo: revisit this vs IntegerExtensions.(Ceil/Floor)Log2
            int res = 0;
            while (val > 1)
            {
                val >>= 1;
                res++;
            }
            return res;
        }

        /**
         * Try to allocate a tiny buffer out of the cache. Returns {@code true} if successful {@code false} otherwise
         */
        internal bool AllocateTiny(PoolArena<T> area, PooledByteBuffer<T> buf, int reqCapacity, int normCapacity) =>
            this.Allocate(this.CacheForTiny(area, normCapacity), buf, reqCapacity);

        /**
         * Try to allocate a small buffer out of the cache. Returns {@code true} if successful {@code false} otherwise
         */
        internal bool AllocateSmall(PoolArena<T> area, PooledByteBuffer<T> buf, int reqCapacity, int normCapacity) =>
            this.Allocate(this.CacheForSmall(area, normCapacity), buf, reqCapacity);

        /**
         * Try to allocate a small buffer out of the cache. Returns {@code true} if successful {@code false} otherwise
         */
        internal bool AllocateNormal(PoolArena<T> area, PooledByteBuffer<T> buf, int reqCapacity, int normCapacity) =>
            this.Allocate(this.CacheForNormal(area, normCapacity), buf, reqCapacity);

        bool Allocate(MemoryRegionCache cache, PooledByteBuffer<T> buf, int reqCapacity)
        {
            if (cache == null)
            {
                // no cache found so just return false here
                return false;
            }
            bool allocated = cache.Allocate(buf, reqCapacity);
            if (++this.allocations >= this.freeSweepAllocationThreshold)
            {
                this.allocations = 0;
                this.Trim();
            }
            return allocated;
        }

        /**
         * Add {@link PoolChunk} and {@code handle} to the cache if there is enough room.
         * Returns {@code true} if it fit into the cache {@code false} otherwise.
         */
        internal bool Add(PoolArena<T> area, PoolChunk<T> chunk, long handle, int normCapacity, SizeClass sizeClass)
        {
            MemoryRegionCache cache = this.Cache(area, normCapacity, sizeClass);
            if (cache == null)
            {
                return false;
            }
            return cache.Add(chunk, handle);
        }

        MemoryRegionCache Cache(PoolArena<T> area, int normCapacity, SizeClass sizeClass)
        {
            switch (sizeClass)
            {
                case SizeClass.Normal:
                    return this.CacheForNormal(area, normCapacity);
                case SizeClass.Small:
                    return this.CacheForSmall(area, normCapacity);
                case SizeClass.Tiny:
                    return this.CacheForTiny(area, normCapacity);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /**
         *  Should be called if the Thread that uses this cache is about to exist to release resources out of the cache
         */
        internal void Free()
        {
            if (this.freeTask != null)
            {
                Debug.Assert(this.deathWatchThread != null);
                ThreadDeathWatcher.Unwatch(this.deathWatchThread, this.freeTask);
            }

            this.Free0();
        }

        void Free0()
        {
            int numFreed = Free(this.tinySubPageDirectCaches) +
                Free(this.smallSubPageDirectCaches) +
                Free(this.normalDirectCaches) +
                Free(this.tinySubPageHeapCaches) +
                Free(this.smallSubPageHeapCaches) +
                Free(this.normalHeapCaches);

            if (numFreed > 0 && Logger.DebugEnabled)
            {
                Logger.Debug("Freed {} thread-local buffer(s) from thread: {}", numFreed, this.deathWatchThread.Name);
            }

            this.DirectArena?.DecrementNumThreadCaches();
            this.HeapArena?.DecrementNumThreadCaches();
        }

        static int Free(MemoryRegionCache[] caches)
        {
            if (caches == null)
            {
                return 0;
            }

            int numFreed = 0;
            foreach (MemoryRegionCache c in caches)
            {
                numFreed += Free(c);
            }
            return numFreed;
        }

        static int Free(MemoryRegionCache cache)
        {
            if (cache == null)
            {
                return 0;
            }
            return cache.Free();
        }

        internal void Trim()
        {
            Trim(this.tinySubPageDirectCaches);
            Trim(this.smallSubPageDirectCaches);
            Trim(this.normalDirectCaches);
            Trim(this.tinySubPageHeapCaches);
            Trim(this.smallSubPageHeapCaches);
            Trim(this.normalHeapCaches);
        }

        static void Trim(MemoryRegionCache[] caches)
        {
            if (caches == null)
            {
                return;
            }
            foreach (MemoryRegionCache c in caches)
            {
                Trim(c);
            }
        }

        static void Trim(MemoryRegionCache cache) => cache?.Trim();

        MemoryRegionCache CacheForTiny(PoolArena<T> area, int normCapacity)
        {
            int idx = PoolArena<T>.TinyIdx(normCapacity);
            return Cache(area.IsDirect ? this.tinySubPageDirectCaches : this.tinySubPageHeapCaches, idx);
        }

        MemoryRegionCache CacheForSmall(PoolArena<T> area, int normCapacity)
        {
            int idx = PoolArena<T>.SmallIdx(normCapacity);
            return Cache(area.IsDirect ? this.smallSubPageDirectCaches : this.smallSubPageHeapCaches, idx);
        }

        MemoryRegionCache CacheForNormal(PoolArena<T> area, int normCapacity)
        {
            if (area.IsDirect)
            {
                int idx = Log2(normCapacity >> this.numShiftsNormalDirect);
                return Cache(this.normalDirectCaches, idx);
            }
            int idx1 = Log2(normCapacity >> this.numShiftsNormalHeap);
            return Cache(this.normalHeapCaches, idx1);
        }

        static MemoryRegionCache Cache(MemoryRegionCache[] cache, int idx)
        {
            if (cache == null || idx > cache.Length - 1)
            {
                return null;
            }
            return cache[idx];
        }

        /**
         * Cache used for buffers which are backed by TINY or SMALL size.
         */
        sealed class SubPageMemoryRegionCache : MemoryRegionCache
        {
            internal SubPageMemoryRegionCache(int size, SizeClass sizeClass)
                : base(size, sizeClass)
            {
            }

            protected override void InitBuf(
                PoolChunk<T> chunk, long handle, PooledByteBuffer<T> buf, int reqCapacity) =>
                chunk.InitBufWithSubpage(buf, handle, reqCapacity);
        }

        /**
         * Cache used for buffers which are backed by NORMAL size.
         */
        sealed class NormalMemoryRegionCache : MemoryRegionCache
        {
            internal NormalMemoryRegionCache(int size)
                : base(size, SizeClass.Normal)
            {
            }

            protected override void InitBuf(
                PoolChunk<T> chunk, long handle, PooledByteBuffer<T> buf, int reqCapacity) =>
                chunk.InitBuf(buf, handle, reqCapacity);
        }

        abstract class MemoryRegionCache
        {
            readonly int size;
            readonly IQueue<Entry> queue;
            readonly SizeClass sizeClass;
            int allocations;

            protected MemoryRegionCache(int size, SizeClass sizeClass)
            {
                this.size = MathUtil.SafeFindNextPositivePowerOfTwo(size);
                this.queue = PlatformDependent.NewFixedMpscQueue<Entry>(this.size);
                this.sizeClass = sizeClass;
            }

            /**
             * Init the {@link PooledByteBuffer} using the provided chunk and handle with the capacity restrictions.
             */
            protected abstract void InitBuf(PoolChunk<T> chunk, long handle,
                PooledByteBuffer<T> buf, int reqCapacity);

            /**
             * Add to cache if not already full.
             */
            public bool Add(PoolChunk<T> chunk, long handle)
            {
                Entry entry = NewEntry(chunk, handle);
                bool queued = this.queue.TryEnqueue(entry);
                if (!queued)
                {
                    // If it was not possible to cache the chunk, immediately recycle the entry
                    entry.Recycle();
                }

                return queued;
            }

            /**
             * Allocate something out of the cache if possible and remove the entry from the cache.
             */
            public bool Allocate(PooledByteBuffer<T> buf, int reqCapacity)
            {
                if (!this.queue.TryDequeue(out Entry entry))
                {
                    return false;
                }
                this.InitBuf(entry.Chunk, entry.Handle, buf, reqCapacity);
                entry.Recycle();

                // allocations is not thread-safe which is fine as this is only called from the same thread all time.
                ++this.allocations;
                return true;
            }

            /**
             * Clear out this cache and free up all previous cached {@link PoolChunk}s and {@code handle}s.
             */
            public int Free() => this.Free(int.MaxValue);

            int Free(int max)
            {
                int numFreed = 0;
                for (; numFreed < max; numFreed++)
                {
                    if (this.queue.TryDequeue(out Entry entry))
                    {
                        this.FreeEntry(entry);
                    }
                    else
                    {
                        // all cleared
                        return numFreed;
                    }
                }
                return numFreed;
            }

            /**
             * Free up cached {@link PoolChunk}s if not allocated frequently enough.
             */
            public void Trim()
            {
                int toFree = this.size - this.allocations;
                this.allocations = 0;

                // We not even allocated all the number that are
                if (toFree > 0)
                {
                    this.Free(toFree);
                }
            }

            void FreeEntry(Entry entry)
            {
                PoolChunk<T> chunk = entry.Chunk;
                long handle = entry.Handle;

                // recycle now so PoolChunk can be GC'ed.
                entry.Recycle();

                chunk.Arena.FreeChunk(chunk, handle, this.sizeClass);
            }

            sealed class Entry
            {
                readonly ThreadLocalPool.Handle recyclerHandle;
                public PoolChunk<T> Chunk;
                public long Handle = -1;

                public Entry(ThreadLocalPool.Handle recyclerHandle)
                {
                    this.recyclerHandle = recyclerHandle;
                }

                internal void Recycle()
                {
                    this.Chunk = null;
                    this.Handle = -1;
                    this.recyclerHandle.Release(this);
                }
            }

            static Entry NewEntry(PoolChunk<T> chunk, long handle)
            {
                Entry entry = Recycler.Take();
                entry.Chunk = chunk;
                entry.Handle = handle;
                return entry;
            }

            static readonly ThreadLocalPool<Entry> Recycler = new ThreadLocalPool<Entry>(handle => new Entry(handle));
        }
    }
}