// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;

    internal class PoolArena
    {
        private readonly int pageSize;
        private readonly int maxOrder;
        private readonly int chunkSize;
        private readonly int pageShifts;
        private readonly int subpageOverflowMask;
        private readonly PoolSubpage[] tinySubpagePools;
        private readonly PoolSubpage[] smallSubpagePools;
        private ChunkPool chunkPools;
        internal readonly IByteBufferAllocator parent;

        public PoolArena(IByteBufferAllocator parent, int pageSize, int maxOrder, int chunkSize)
        {
            this.parent = parent;
            this.pageSize = pageSize;
            this.maxOrder = maxOrder;
            this.chunkSize = chunkSize;
            this.pageShifts = Log2(pageSize);
            this.subpageOverflowMask = ~(pageSize - 1);
            this.tinySubpagePools = new PoolSubpage[32];
            for (var i = 0; i < this.tinySubpagePools.Length; i++)
            {
                this.tinySubpagePools[i] = this.CreateSubpagePoolHead();
            }
            this.smallSubpagePools = new PoolSubpage[this.pageShifts - 9];
            for (var i = 0; i < this.smallSubpagePools.Length; i++)
            {
                this.smallSubpagePools[i] = this.CreateSubpagePoolHead();
            }
            this.chunkPools = new ChunkPool();
        }

        public IByteBuffer Allocate(int reqCapacity, int maxCapacity)
        {
            var buffer = new PooledByteBuffer(maxCapacity);
            this.Allocate(buffer, reqCapacity);
            return buffer;
        }

        public void Allocate(PooledByteBuffer buffer, int reqCapacity)
        {
            var normCapacity = this.NormalizeCapacity(reqCapacity);
            if (this.IsTinyOrSmall(normCapacity))
            {
                var tableIdx = 0;
                PoolSubpage[] table;
                if ((normCapacity & 0xFFFFFE00) == 0)
                {
                    tableIdx = normCapacity >> 4;
                    table = this.tinySubpagePools;
                }
                else
                {
                    tableIdx = 0;
                    var i = normCapacity >> 10;//1024
                    while (i != 0)
                    {
                        i >>= 1;
                        tableIdx++;
                    }
                    table = this.smallSubpagePools;
                }
                lock (this)
                {
                    var head = table[tableIdx];
                    var s = head.Next;
                    if (s != head)
                    {
                        long handle = s.Allocate();
                        s.Chunk.InitBufferWithSubpage(buffer, handle, reqCapacity);
                        return;
                    }
                }
            }
            else if (normCapacity > this.chunkSize)
            {
                var unpooledChunk = new PoolChunk(this, normCapacity);
                buffer.Init(unpooledChunk, 0, 0, reqCapacity, normCapacity);
                return;
            }
            this.Allocate(buffer, reqCapacity, normCapacity);
        }

        internal void Reallocate(PooledByteBuffer buffer, int newCapacity, bool freeOldMemory)
        {
            if (newCapacity < 0 || newCapacity > buffer.MaxCapacity)
            {
                throw new ArgumentOutOfRangeException("newCapacity: " + newCapacity);
            }
            var oldCapacity = buffer.Capacity;
            if (oldCapacity == newCapacity)
            {
                return;
            }
            var oldChunk = buffer.Chunk;
            var oldHandle = buffer.Handle;
            var oldBuffer = buffer.Array;
            var oldOffset = buffer.ArrayOffset;
            //var oldLength = buffer.Length;
            var readerIndex = buffer.ReaderIndex;
            var writerIndex = buffer.WriterIndex;
            this.Allocate(buffer, newCapacity);
            if (newCapacity > oldCapacity)
            {
                Buffer.BlockCopy(oldBuffer, oldOffset, buffer.Array, buffer.ArrayOffset, oldCapacity);
            }
            else if (newCapacity < oldCapacity)
            {
                if (readerIndex < newCapacity)
                {
                    if (writerIndex > newCapacity)
                    {
                        writerIndex = newCapacity;
                    }
                    Buffer.BlockCopy(oldBuffer, oldOffset + readerIndex, buffer.Array, buffer.ArrayOffset + readerIndex, writerIndex - readerIndex);
                }
                else
                {
                    readerIndex = writerIndex = newCapacity;
                }
            }
            buffer.SetIndex(readerIndex, writerIndex);
            if (freeOldMemory)
            {
                this.Free(oldChunk, oldHandle);
            }
        }

        public void Free(PoolChunk chunk, long handle)
        {
            if (chunk.Unpooled)
            {
                // Rely on GC.
                return;
            }
            lock (this)
            {
                chunk.Free(handle);
            }
        }

        internal PoolSubpage FindSubpagePoolHead(int elemSize)
        {
            var tableIdx = 0;
            PoolSubpage[] table;
            if (IsTiny(elemSize))
            { // < 512
                tableIdx = elemSize >> 4;
                table = this.tinySubpagePools;
            }
            else
            {
                tableIdx = 0;
                elemSize >>= 10;
                while (elemSize != 0)
                {
                    elemSize >>= 1;
                    tableIdx++;
                }
                table = this.smallSubpagePools;
            }

            return table[tableIdx];
        }

        private void Allocate(PooledByteBuffer buffer, int reqCapacity, int normCapacity)
        {
            lock (this)
            {
                if (this.chunkPools.Allocate(buffer, reqCapacity, normCapacity))
                {
                    return;
                }
                var chunk = new PoolChunk(this, this.pageSize, this.maxOrder, this.pageShifts, this.chunkSize);
                var handle = chunk.Allocate(normCapacity);
                chunk.InitBuffer(buffer, handle, reqCapacity);
                this.chunkPools.Add(chunk);
            }
        }

        private int NormalizeCapacity(int reqCapacity)
        {
            if (reqCapacity >= chunkSize)
            {
                return reqCapacity;
            }

            if (!this.IsTiny(reqCapacity))
            {
                //make sure the new capacity is double capacity of request.
                var normalizedCapacity = reqCapacity;
                normalizedCapacity--;
                normalizedCapacity |= normalizedCapacity >> 1;
                normalizedCapacity |= normalizedCapacity >> 2;
                normalizedCapacity |= normalizedCapacity >> 4;
                normalizedCapacity |= normalizedCapacity >> 8;
                normalizedCapacity |= normalizedCapacity >> 16;
                normalizedCapacity++;
                if (normalizedCapacity < 0)
                {
                    normalizedCapacity >>= 1;
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

        public bool IsTiny(int normCapacity)
        {
            return (normCapacity & 0xFFFFFE00) == 0;
        }

        public bool IsTinyOrSmall(int normCapacity)
        {
            return (normCapacity & this.subpageOverflowMask) == 0;
        }

        private PoolSubpage CreateSubpagePoolHead()
        {
            var head = new PoolSubpage(this.pageSize);
            head.Prev = head;
            head.Next = head;
            return head;
        }

        private class ChunkPool
        {
            private int totalChunks;
            private PoolChunk head;

            public int TotalChunks
            {
                get
                {
                    return this.totalChunks;
                }
            }

            public bool Allocate(PooledByteBuffer buffer, int reqCapacity, int normCapacity)
            {
                if (this.head == null)
                {
                    return false;
                }
                var cur = this.head;
                do
                {
                    var handle = cur.Allocate(normCapacity);
                    if (handle >= 0)
                    {
                        cur.InitBuffer(buffer, handle, reqCapacity);
                        return true;
                    }
                } while ((cur = cur.Next) != null);
                return false;
            }

            public void Add(PoolChunk chunk)
            {
                if (this.head == null)
                {
                    this.head = chunk;
                    chunk.Prev = chunk.Next = null;
                }
                else
                {
                    chunk.Prev = null;
                    chunk.Next = head;
                    this.head.Prev = chunk;
                    this.head = chunk;
                }
                this.totalChunks++;
            }

            private void Remove(PoolChunk cur)
            {
                if (cur == head)
                {
                    this.head = cur.Next;
                    if (this.head != null)
                    {
                        this.head.Prev = null;
                    }
                }
                else
                {
                    var next = cur.Next;
                    cur.Prev.Next = next;
                    if (next != null)
                    {
                        next.Prev = cur.Prev;
                    }
                }
                this.totalChunks--;
            }
        }

        private static int Log2(int value)
        {
            var num = 0;
            while ((value >>= 1) > 0)
            {
                num++;
            }
            return num;
        }
    }
}
