// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Text;
    using DotNetty.Common;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    public class PooledByteBufferAllocator : AbstractByteBufferAllocator, IByteBufferAllocatorMetricProvider
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<PooledByteBufferAllocator>();

        public static readonly int DefaultNumHeapArena;

        public static readonly int DefaultPageSize;
        public static readonly int DefaultMaxOrder; // 8192 << 11 = 16 MiB per chunk
        public static readonly int DefaultTinyCacheSize;
        public static readonly int DefaultSmallCacheSize;
        public static readonly int DefaultNormalCacheSize;

        static readonly int DefaultMaxCachedBufferCapacity;
        static readonly int DefaultCacheTrimInterval;

        const int MinPageSize = 4096;
        const int MaxChunkSize = (int)(((long)int.MaxValue + 1) / 2);

        static PooledByteBufferAllocator()
        {
            int defaultPageSize = SystemPropertyUtil.GetInt("io.netty.allocator.pageSize", 8192);
            Exception pageSizeFallbackCause = null;
            try
            {
                ValidateAndCalculatePageShifts(defaultPageSize);
            }
            catch (Exception t)
            {
                pageSizeFallbackCause = t;
                defaultPageSize = 8192;
            }
            DefaultPageSize = defaultPageSize;

            int defaultMaxOrder = SystemPropertyUtil.GetInt("io.netty.allocator.maxOrder", 11);
            Exception maxOrderFallbackCause = null;
            try
            {
                ValidateAndCalculateChunkSize(DefaultPageSize, defaultMaxOrder);
            }
            catch (Exception t)
            {
                maxOrderFallbackCause = t;
                defaultMaxOrder = 11;
            }
            DefaultMaxOrder = defaultMaxOrder;

            // todo: Determine reasonable default for heapArenaCount
            // Assuming each arena has 3 chunks, the pool should not consume more than 50% of max memory.

            // Use 2 * cores by default to reduce contention as we use 2 * cores for the number of EventLoops
            // in NIO and EPOLL as well. If we choose a smaller number we will run into hotspots as allocation and
            // deallocation needs to be synchronized on the PoolArena.
            // See https://github.com/netty/netty/issues/3888
            int defaultMinNumArena = Environment.ProcessorCount * 2;
            DefaultNumHeapArena = Math.Max(0, SystemPropertyUtil.GetInt("dotNetty.allocator.numHeapArenas", defaultMinNumArena));

            // cache sizes
            DefaultTinyCacheSize = SystemPropertyUtil.GetInt("io.netty.allocator.tinyCacheSize", 512);
            DefaultSmallCacheSize = SystemPropertyUtil.GetInt("io.netty.allocator.smallCacheSize", 256);
            DefaultNormalCacheSize = SystemPropertyUtil.GetInt("io.netty.allocator.normalCacheSize", 64);

            // 32 kb is the default maximum capacity of the cached buffer. Similar to what is explained in
            // 'Scalable memory allocation using jemalloc'
            DefaultMaxCachedBufferCapacity = SystemPropertyUtil.GetInt("io.netty.allocator.maxCachedBufferCapacity", 32 * 1024);

            // the number of threshold of allocations when cached entries will be freed up if not frequently used
            DefaultCacheTrimInterval = SystemPropertyUtil.GetInt(
                "io.netty.allocator.cacheTrimInterval", 8192);

            if (Logger.DebugEnabled)
            {
                Logger.Debug("-Dio.netty.allocator.numHeapArenas: {}", DefaultNumHeapArena);
                if (pageSizeFallbackCause == null)
                {
                    Logger.Debug("-Dio.netty.allocator.pageSize: {}", DefaultPageSize);
                }
                else
                {
                    Logger.Debug("-Dio.netty.allocator.pageSize: {}", DefaultPageSize, pageSizeFallbackCause);
                }
                if (maxOrderFallbackCause == null)
                {
                    Logger.Debug("-Dio.netty.allocator.maxOrder: {}", DefaultMaxOrder);
                }
                else
                {
                    Logger.Debug("-Dio.netty.allocator.maxOrder: {}", DefaultMaxOrder, maxOrderFallbackCause);
                }
                Logger.Debug("-Dio.netty.allocator.chunkSize: {}", DefaultPageSize << DefaultMaxOrder);
                Logger.Debug("-Dio.netty.allocator.tinyCacheSize: {}", DefaultTinyCacheSize);
                Logger.Debug("-Dio.netty.allocator.smallCacheSize: {}", DefaultSmallCacheSize);
                Logger.Debug("-Dio.netty.allocator.normalCacheSize: {}", DefaultNormalCacheSize);
                Logger.Debug("-Dio.netty.allocator.maxCachedBufferCapacity: {}", DefaultMaxCachedBufferCapacity);
                Logger.Debug("-Dio.netty.allocator.cacheTrimInterval: {}", DefaultCacheTrimInterval);
            }

            Default = new PooledByteBufferAllocator();
        }

        public static readonly PooledByteBufferAllocator Default;

        readonly PoolArena<byte[]>[] heapArenas;
        readonly int tinyCacheSize;
        readonly int smallCacheSize;
        readonly int normalCacheSize;
        readonly IReadOnlyList<IPoolArenaMetric> heapArenaMetrics;
        readonly PoolThreadLocalCache threadCache;
        readonly int chunkSize;
        readonly PooledByteBufferAllocatorMetric metric;

        public PooledByteBufferAllocator()
            : this(DefaultNumHeapArena, DefaultPageSize, DefaultMaxOrder)
        {
        }

        public PooledByteBufferAllocator(int nHeapArena, int pageSize, int maxOrder)
            : this(nHeapArena, pageSize, maxOrder,
                DefaultTinyCacheSize, DefaultSmallCacheSize, DefaultNormalCacheSize)
        {
        }

        public PooledByteBufferAllocator(int nHeapArena, int pageSize, int maxOrder,
            int tinyCacheSize, int smallCacheSize, int normalCacheSize)
        {
            Contract.Requires(nHeapArena >= 0);

            this.threadCache = new PoolThreadLocalCache(this);
            this.tinyCacheSize = tinyCacheSize;
            this.smallCacheSize = smallCacheSize;
            this.normalCacheSize = normalCacheSize;
            this.chunkSize = ValidateAndCalculateChunkSize(pageSize, maxOrder);

            int pageShifts = ValidateAndCalculatePageShifts(pageSize);

            if (nHeapArena > 0)
            {
                this.heapArenas = NewArenaArray<byte[]>(nHeapArena);
                var metrics = new List<IPoolArenaMetric>(this.heapArenas.Length);
                for (int i = 0; i < this.heapArenas.Length; i++)
                {
                    var arena = new HeapArena(this, pageSize, maxOrder, pageShifts, this.chunkSize);
                    this.heapArenas[i] = arena;
                    metrics.Add(arena);
                }
                this.heapArenaMetrics = metrics.AsReadOnly();
            }
            else
            {
                this.heapArenas = null;
                this.heapArenaMetrics = new IPoolArenaMetric[0];
            }

            this.metric = new PooledByteBufferAllocatorMetric(this);
        }

        static PoolArena<T>[] NewArenaArray<T>(int size) => new PoolArena<T>[size];

        static int ValidateAndCalculatePageShifts(int pageSize)
        {
            Contract.Requires(pageSize >= MinPageSize);
            Contract.Requires((pageSize & pageSize - 1) == 0, "Expected power of 2");

            // Logarithm base 2. At this point we know that pageSize is a power of two.
            return (sizeof(int) * 8 - 1) -  pageSize.NumberOfLeadingZeros();
        }

        static int ValidateAndCalculateChunkSize(int pageSize, int maxOrder)
        {
            Contract.Requires(maxOrder <= 14);

            // Ensure the resulting chunkSize does not overflow.
            int chunkSize = pageSize;
            for (int i = maxOrder; i > 0; i--)
            {
                if (chunkSize > MaxChunkSize >> 1)
                {
                    throw new ArgumentException($"pageSize ({pageSize}) << maxOrder ({maxOrder}) must not exceed {MaxChunkSize}");
                }
                chunkSize <<= 1;
            }
            return chunkSize;
        }

        protected override IByteBuffer NewHeapBuffer(int initialCapacity, int maxCapacity)
        {
            PoolThreadCache<byte[]> cache = this.threadCache.Value;
            PoolArena<byte[]> heapArena = cache.HeapArena;

            IByteBuffer buf;
            if (heapArena != null)
            {
                buf = heapArena.Allocate(cache, initialCapacity, maxCapacity);
            }
            else
            {
                buf = new UnpooledHeapByteBuffer(this, initialCapacity, maxCapacity);
            }

            return ToLeakAwareBuffer(buf);
        }

        sealed class PoolThreadLocalCache : FastThreadLocal<PoolThreadCache<byte[]>>
        {
            readonly PooledByteBufferAllocator owner;

            public PoolThreadLocalCache(PooledByteBufferAllocator owner)
            {
                this.owner = owner;
            }

            protected override PoolThreadCache<byte[]> GetInitialValue()
            {
                lock (this)
                {
                    PoolArena<byte[]> heapArena = this.LeastUsedArena(this.owner.heapArenas);
                        return new PoolThreadCache<byte[]>(
                            heapArena, this.owner.tinyCacheSize, this.owner.smallCacheSize, this.owner.normalCacheSize,
                            DefaultMaxCachedBufferCapacity, DefaultCacheTrimInterval);
                }
            }

            protected override void OnRemoval(PoolThreadCache<byte[]> threadCache) => threadCache.Free();

            PoolArena<T> LeastUsedArena<T>(PoolArena<T>[] arenas)
            {
                if (arenas == null || arenas.Length == 0)
                {
                    return null;
                }

                PoolArena<T> minArena = arenas[0];
                for (int i = 1; i < arenas.Length; i++)
                {
                    PoolArena<T> arena = arenas[i];
                    if (arena.NumThreadCaches < minArena.NumThreadCaches)
                    {
                        minArena = arena;
                    }
                }

                return minArena;
            }
        }

        internal IReadOnlyList<IPoolArenaMetric> HeapArenas() => this.heapArenaMetrics;

        // ReSharper disable ConvertToAutoPropertyWhenPossible
        internal int TinyCacheSize => this.tinyCacheSize;

        internal int SmallCacheSize => this.smallCacheSize;

        internal int NormalCacheSize => this.normalCacheSize;

        internal int ChunkSize => this.chunkSize;
        // ReSharper restore ConvertToAutoPropertyWhenPossible

        // ReSharper disable ConvertToAutoProperty
        public PooledByteBufferAllocatorMetric Metric => this.metric;
        // ReSharper restore ConvertToAutoProperty

        IByteBufferAllocatorMetric IByteBufferAllocatorMetricProvider.Metric => this.Metric;
        
        internal long UsedHeapMemory => UsedMemory(this.heapArenas);

        static long UsedMemory(PoolArena<byte[]>[] arenas)
        {
            if (arenas == null)
            {
                return -1;
            }
            long used = 0;
            foreach (PoolArena<byte[]> arena in arenas)
            {
                used += arena.NumActiveBytes;
                if (used < 0)
                {
                    return long.MaxValue;
                }
            }

            return used;
        }

        internal PoolThreadCache<T> ThreadCache<T>() => (PoolThreadCache<T>)(object)this.threadCache.Value;

        /// Returns the status of the allocator (which contains all metrics) as string. Be aware this may be expensive
        /// and so should not called too frequently.
        public string DumpStats()
        {
            int heapArenasLen = this.heapArenas?.Length ?? 0;
            StringBuilder buf = new StringBuilder(512)
                    .Append(heapArenasLen)
                    .Append(" heap arena(s):")
                    .Append(StringUtil.Newline);
            if (heapArenasLen > 0)
            {
                // ReSharper disable once PossibleNullReferenceException
                foreach (PoolArena<byte[]> a in this.heapArenas)
                {
                    buf.Append(a);
                }
            }

            return buf.ToString();
        }
    }
}