// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;        
    using System.Threading;
    using DotNetty.Common;

    public class PooledByteBufferAllocator : AbstractByteBufferAllocator
    {
        private const int MIN_PAGE_SIZE = 4096;

        private static readonly int DEFAULT_NUM_ARENA = Environment.ProcessorCount;
        private static readonly int DEFAULT_PAGE_SIZE = 4096;
        private static readonly int DEFAULT_MAX_ORDER = 11;
        private static readonly int MAX_CHUNK_SIZE = 1073741824;//512M

        private PoolArena[] _arenas;
        private int _seqNum;

        public PooledByteBufferAllocator()
            : this(DEFAULT_NUM_ARENA, DEFAULT_PAGE_SIZE, DEFAULT_MAX_ORDER)
        {
        }

        public PooledByteBufferAllocator(int numberArenas, int pageSize, int maxOrder)
        {
            if (numberArenas <= 0)
            {
                throw new ArgumentOutOfRangeException("numberArenas", "numberArenas must be greater than 0.");
            }
            if (pageSize < MIN_PAGE_SIZE)
            {
                throw new ArgumentException("pageSize", string.Format("pageSize cannot be less than {0}", MIN_PAGE_SIZE));
            }
            if ((pageSize & pageSize - 1) != 0)
            {
                throw new ArgumentException("pageSize shoud be power of 2.");
            }
            if (maxOrder > 14)
            {
                throw new ArgumentException("maxOrder: " + maxOrder + " (expected: 0-14)");
            }
            if ((pageSize << maxOrder) > MAX_CHUNK_SIZE)
            {
                throw new ArgumentException(String.Format("pageSize ({0}) << maxOrder ({1}) must not exceed {2}", pageSize, maxOrder, MAX_CHUNK_SIZE));
            }
            _arenas = new PoolArena[numberArenas];
            var chunkSize = pageSize << maxOrder;
            for (var i = 0; i < numberArenas; i++)
            {
                _arenas[i] = new PoolArena(this, pageSize, maxOrder, chunkSize);
            }
        }

        protected override IByteBuffer NewBuffer(int initialCapacity, int maxCapacity)
        {
            var arena = this.NextArenaToUses();
            var buffer = arena.Allocate(initialCapacity, maxCapacity);
            return buffer;
        }

        private PoolArena NextArenaToUses()
        {
            var index = (uint)Interlocked.Increment(ref _seqNum) % _arenas.Length;
            return _arenas[index];
        }
    }
}