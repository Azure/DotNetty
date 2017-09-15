// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Text;
    using DotNetty.Common.Utilities;

    sealed class PoolChunkList<T> : IPoolChunkListMetric
    {
        readonly PoolArena<T> arena;
        readonly PoolChunkList<T> nextList;
        readonly int minUsage;
        readonly int maxUsage;
        readonly int maxCapacity;
        PoolChunk<T> head;

        // This is only update once when create the linked like list of PoolChunkList in PoolArena constructor.
        PoolChunkList<T> prevList;

        // TODO: Test if adding padding helps under contention
        //private long pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7;

        public PoolChunkList(PoolArena<T> arena, PoolChunkList<T> nextList, int minUsage, int maxUsage, int chunkSize)
        {
            Contract.Assert(minUsage <= maxUsage);
            this.arena = arena;
            this.nextList = nextList;
            this.minUsage = minUsage;
            this.maxUsage = maxUsage;
            this.maxCapacity = CalculateMaxCapacity(minUsage, chunkSize);
        }

        /// Calculates the maximum capacity of a buffer that will ever be possible to allocate out of the {@link PoolChunk}s
        /// that belong to the {@link PoolChunkList} with the given {@code minUsage} and {@code maxUsage} settings.
        static int CalculateMaxCapacity(int minUsage, int chunkSize)
        {
            minUsage = MinUsage0(minUsage);

            if (minUsage == 100)
            {
                // If the minUsage is 100 we can not allocate anything out of this list.
                return 0;
            }

            // Calculate the maximum amount of bytes that can be allocated from a PoolChunk in this PoolChunkList.
            //
            // As an example:
            // - If a PoolChunkList has minUsage == 25 we are allowed to allocate at most 75% of the chunkSize because
            //   this is the maximum amount available in any PoolChunk in this PoolChunkList.
            return (int)(chunkSize * (100L - minUsage) / 100L);
        }

        internal void PrevList(PoolChunkList<T> list)
        {
            Debug.Assert(this.prevList == null);
            this.prevList = list;
        }

        internal bool Allocate(PooledByteBuffer<T> buf, int reqCapacity, int normCapacity)
        {
            if (this.head == null || normCapacity > this.maxCapacity)
            {
                // Either this PoolChunkList is empty or the requested capacity is larger then the capacity which can
                // be handled by the PoolChunks that are contained in this PoolChunkList.
                return false;
            }

            for (PoolChunk<T> cur = this.head;;)
            {
                long handle = cur.Allocate(normCapacity);
                if (handle < 0)
                {
                    cur = cur.Next;
                    if (cur == null)
                    {
                        return false;
                    }
                }
                else
                {
                    cur.InitBuf(buf, handle, reqCapacity);
                    if (cur.Usage >= this.maxUsage)
                    {
                        this.Remove(cur);
                        this.nextList.Add(cur);
                    }
                    return true;
                }
            }
        }

        internal bool Free(PoolChunk<T> chunk, long handle)
        {
            chunk.Free(handle);
            if (chunk.Usage < this.minUsage)
            {
                this.Remove(chunk);
                // Move the PoolChunk down the PoolChunkList linked-list.
                return this.Move0(chunk);
            }
            return true;
        }

        bool Move(PoolChunk<T> chunk)
        {
            Contract.Assert(chunk.Usage < this.maxUsage);

            if (chunk.Usage < this.minUsage)
            {
                // Move the PoolChunk down the PoolChunkList linked-list.
                return this.Move0(chunk);
            }

            // PoolChunk fits into this PoolChunkList, adding it here.
            this.Add0(chunk);
            return true;
        }

        /// Moves the {@link PoolChunk} down the {@link PoolChunkList} linked-list so it will end up in the right
        /// {@link PoolChunkList} that has the correct minUsage / maxUsage in respect to {@link PoolChunk#usage()}.
        bool Move0(PoolChunk<T> chunk)
        {
            if (this.prevList == null)
            {
                // There is no previous PoolChunkList so return false which result in having the PoolChunk destroyed and
                // all memory associated with the PoolChunk will be released.
                Debug.Assert(chunk.Usage == 0);
                return false;
            }
            return this.prevList.Move(chunk);
        }

        internal void Add(PoolChunk<T> chunk)
        {
            if (chunk.Usage >= this.maxUsage)
            {
                this.nextList.Add(chunk);
                return;
            }
            this.Add0(chunk);
        }

        /// Adds the {@link PoolChunk} to this {@link PoolChunkList}.
        void Add0(PoolChunk<T> chunk)
        {
            chunk.Parent = this;
            if (this.head == null)
            {
                this.head = chunk;
                chunk.Prev = null;
                chunk.Next = null;
            }
            else
            {
                chunk.Prev = null;
                chunk.Next = this.head;
                this.head.Prev = chunk;
                this.head = chunk;
            }
        }

        void Remove(PoolChunk<T> cur)
        {
            if (cur == this.head)
            {
                this.head = cur.Next;
                if (this.head != null)
                {
                    this.head.Prev = null;
                }
            }
            else
            {
                PoolChunk<T> next = cur.Next;
                cur.Prev.Next = next;
                if (next != null)
                {
                    next.Prev = cur.Prev;
                }
            }
        }

        public int MinUsage => MinUsage0(this.minUsage);

        public int MaxUsage => Math.Min(this.maxUsage, 100);

        static int MinUsage0(int value) => Math.Max(1, value);

        public IEnumerator<IPoolChunkMetric> GetEnumerator() => 
            this.head == null ? Enumerable.Empty<IPoolChunkMetric>().GetEnumerator() : this.GetEnumeratorInternal();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        IEnumerator<IPoolChunkMetric> GetEnumeratorInternal()
        {
            lock (this.arena)
            {
                for (PoolChunk<T> cur = this.head; cur != null;)
                {
                    yield return cur;
                    cur = cur.Next;
                }
            }
        }

        public override string ToString()
        {
            var buf = new StringBuilder();
            lock (this.arena)
            {
                if (this.head == null)
                {
                    return "none";
                }

                for (PoolChunk<T> cur = this.head; ;)
                {
                    buf.Append(cur);
                    cur = cur.Next;
                    if (cur == null)
                    {
                        break;
                    }
                    buf.Append(StringUtil.Newline);
                }
            }

            return buf.ToString();
        }

        internal void Destroy(PoolArena<T> poolArena)
        {
            PoolChunk<T> chunk = this.head;
            while (chunk != null)
            {
                poolArena.DestroyChunk(chunk);
                chunk = chunk.Next;
            }

            this.head = null;
        }
    }
}