// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using DotNetty.Common.Utilities;

    sealed class PoolSubpage<T> : IPoolSubpageMetric
    {
        internal readonly PoolChunk<T> Chunk;
        readonly int memoryMapIdx;
        readonly int runOffset;
        readonly int pageSize;
        readonly long[] bitmap;

        internal PoolSubpage<T> Prev;
        internal PoolSubpage<T> Next;

        internal bool DoNotDestroy;
        internal int ElemSize;
        int maxNumElems;
        int bitmapLength;
        int nextAvail;
        int numAvail;

        // TODO: Test if adding padding helps under contention
        //private long pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7;

        /** Special constructor that creates a linked list head */

        public PoolSubpage(int pageSize)
        {
            this.Chunk = null;
            this.memoryMapIdx = -1;
            this.runOffset = -1;
            this.ElemSize = -1;
            this.pageSize = pageSize;
            this.bitmap = null;
        }

        public PoolSubpage(PoolSubpage<T> head, PoolChunk<T> chunk, int memoryMapIdx, int runOffset, int pageSize, int elemSize)
        {
            this.Chunk = chunk;
            this.memoryMapIdx = memoryMapIdx;
            this.runOffset = runOffset;
            this.pageSize = pageSize;
            this.bitmap = new long[pageSize.RightUShift(10)]; // pageSize / 16 / 64
            this.Init(head, elemSize);
        }

        public void Init(PoolSubpage<T> head, int elemSize)
        {
            this.DoNotDestroy = true;
            this.ElemSize = elemSize;
            if (elemSize != 0)
            {
                this.maxNumElems = this.numAvail = this.pageSize / elemSize;
                this.nextAvail = 0;
                this.bitmapLength = this.maxNumElems.RightUShift(6);
                if ((this.maxNumElems & 63) != 0)
                {
                    this.bitmapLength++;
                }

                for (int i = 0; i < this.bitmapLength; i++)
                {
                    this.bitmap[i] = 0;
                }
            }

            this.AddToPool(head);
        }

        /**
         * Returns the bitmap index of the subpage allocation.
         */

        internal long Allocate()
        {
            if (this.ElemSize == 0)
            {
                return this.ToHandle(0);
            }

            if (this.numAvail == 0 || !this.DoNotDestroy)
            {
                return -1;
            }

            int bitmapIdx = this.GetNextAvail();
            int q = bitmapIdx.RightUShift(6);
            int r = bitmapIdx & 63;
            Contract.Assert((this.bitmap[q].RightUShift(r) & 1) == 0);
            this.bitmap[q] |= 1L << r;

            if (--this.numAvail == 0)
            {
                this.RemoveFromPool();
            }

            return this.ToHandle(bitmapIdx);
        }

        /**
         * @return {@code true} if this subpage is in use.
         *         {@code false} if this subpage is not used by its chunk and thus it's OK to be released.
         */

        internal bool Free(PoolSubpage<T> head, int bitmapIdx)
        {
            if (this.ElemSize == 0)
            {
                return true;
            }

            int q = bitmapIdx.RightUShift(6);
            int r = bitmapIdx & 63;
            Debug.Assert((this.bitmap[q].RightUShift(r) & 1) != 0);
            this.bitmap[q] ^= 1L << r;

            this.SetNextAvail(bitmapIdx);

            if (this.numAvail++ == 0)
            {
                this.AddToPool(head);
                return true;
            }

            if (this.numAvail != this.maxNumElems)
            {
                return true;
            }
            else
            {
                // Subpage not in use (numAvail == maxNumElems)
                if (this.Prev == this.Next)
                {
                    // Do not remove if this subpage is the only one left in the pool.
                    return true;
                }

                // Remove this subpage from the pool if there are other subpages left in the pool.
                this.DoNotDestroy = false;
                this.RemoveFromPool();
                return false;
            }
        }

        void AddToPool(PoolSubpage<T> head)
        {
            Debug.Assert(this.Prev == null && this.Next == null);

            this.Prev = head;
            this.Next = head.Next;
            this.Next.Prev = this;
            head.Next = this;
        }

        void RemoveFromPool()
        {
            Debug.Assert(this.Prev != null && this.Next != null);

            this.Prev.Next = this.Next;
            this.Next.Prev = this.Prev;
            this.Next = null;
            this.Prev = null;
        }

        void SetNextAvail(int bitmapIdx) => this.nextAvail = bitmapIdx;

        int GetNextAvail()
        {
            int nextAvail = this.nextAvail;
            if (nextAvail >= 0)
            {
                this.nextAvail = -1;
                return nextAvail;
            }
            return this.FindNextAvail();
        }

        int FindNextAvail()
        {
            long[] bitmap = this.bitmap;
            int bitmapLength = this.bitmapLength;
            for (int i = 0; i < bitmapLength; i++)
            {
                long bits = bitmap[i];
                if (~bits != 0)
                {
                    return this.FindNextAvail0(i, bits);
                }
            }
            return -1;
        }

        int FindNextAvail0(int i, long bits)
        {
            int maxNumElems = this.maxNumElems;
            int baseVal = i << 6;

            for (int j = 0; j < 64; j++)
            {
                if ((bits & 1) == 0)
                {
                    int val = baseVal | j;
                    if (val < maxNumElems)
                    {
                        return val;
                    }
                    else
                    {
                        break;
                    }
                }
                bits = bits.RightUShift(1);
            }
            return -1;
        }

        long ToHandle(int bitmapIdx) => 0x4000000000000000L | (long)bitmapIdx << 32 | (uint)this.memoryMapIdx;

        public override string ToString()
        {
            bool doNotDestroy;
            int maxNumElems;
            int numAvail;
            int elemSize;
            lock (this.Chunk.Arena)
            {
                if (!this.DoNotDestroy)
                {
                    doNotDestroy = false;
                    // Not used for creating the String.
                    maxNumElems = numAvail = elemSize = -1;
                }
                else
                {
                    doNotDestroy = true;
                    maxNumElems = this.maxNumElems;
                    numAvail = this.numAvail;
                    elemSize = this.ElemSize;
                }
            }

            if (!doNotDestroy)
            {
                return "(" + this.memoryMapIdx + ": not in use)";
            }

            return '(' + this.memoryMapIdx + ": " + (maxNumElems - numAvail) + '/' + maxNumElems +
                ", offset: " + this.runOffset + ", length: " + this.pageSize + ", elemSize: " + elemSize + ')';
        }

        public int MaxNumElements
        {
            get
            {
                lock (this.Chunk.Arena)
                {
                    return this.maxNumElems;
                }
            }
        }

        public int NumAvailable
        {
            get
            {
                lock (this.Chunk.Arena)
                {
                    return this.numAvail;
                }
            }
        }

        public int ElementSize
        {
            get
            {
                lock (this.Chunk.Arena)
                {
                    return this.ElemSize;
                }
            }
        }

        public int PageSize => this.pageSize;

        internal void Destroy() => this.Chunk?.Destroy();
    }
}