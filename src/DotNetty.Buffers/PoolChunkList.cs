// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Text;

    sealed class PoolChunkList<T> : IPoolChunkListMetric
    {
        static readonly IEnumerable<IPoolChunkMetric> EMPTY_METRICS = Enumerable.Empty<IPoolChunkMetric>();
        readonly PoolChunkList<T> nextList;
        readonly int minUsage;
        readonly int maxUsage;

        PoolChunk<T> head;

        // This is only update once when create the linked like list of PoolChunkList in PoolArena constructor.
        PoolChunkList<T> prevList;

        // TODO: Test if adding padding helps under contention
        //private long pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7;

        public PoolChunkList(PoolChunkList<T> nextList, int minUsage, int maxUsage)
        {
            this.nextList = nextList;
            this.minUsage = minUsage;
            this.maxUsage = maxUsage;
        }

        internal void PrevList(PoolChunkList<T> prevList)
        {
            Contract.Requires(this.prevList == null);
            this.prevList = prevList;
        }

        internal bool Allocate(PooledByteBuffer<T> buf, int reqCapacity, int normCapacity)
        {
            if (this.head == null)
            {
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
                if (this.prevList == null)
                {
                    Contract.Assert(chunk.Usage == 0);
                    return false;
                }
                else
                {
                    this.prevList.Add(chunk);
                    return true;
                }
            }
            return true;
        }

        internal void Add(PoolChunk<T> chunk)
        {
            if (chunk.Usage >= this.maxUsage)
            {
                this.nextList.Add(chunk);
                return;
            }

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

        public int MinUsage
        {
            get { return this.minUsage; }
        }

        public int MaxUsage
        {
            get { return this.maxUsage; }
        }

        public IEnumerable<IPoolChunkMetric> Iterator()
        {
            if (this.head == null)
            {
                return EMPTY_METRICS;
            }
            var metrics = new List<IPoolChunkMetric>();
            for (PoolChunk<T> cur = this.head;;)
            {
                metrics.Add(cur);
                cur = cur.Next;
                if (cur == null)
                {
                    break;
                }
            }
            return metrics;
        }

        public override string ToString()
        {
            if (this.head == null)
            {
                return "none";
            }

            var buf = new StringBuilder();
            for (PoolChunk<T> cur = this.head;;)
            {
                buf.Append(cur);
                cur = cur.Next;
                if (cur == null)
                {
                    break;
                }
                buf.Append(Environment.NewLine); // todo: StringUtil.NEWLINE
            }

            return buf.ToString();
        }
    }
}