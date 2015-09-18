// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;

    internal class PoolSubpage
    {
        private int memoryMapIdx;
        private int runOffset;
        private int pageSize;
        private long[] bitmap;
        private int maxNumElems;
        private int bitmapLength;
        private int nextAvail;
        private int numAvail;
        private int elemSize;
        private PoolChunk chunk;
        private bool doNotDestroy;

        public PoolSubpage(int pageSize)
        {
            this.pageSize = pageSize;
        }

        public PoolSubpage(PoolChunk chunk, int memoryMapIdx, int runOffset, int pageSize, int elemSize)
        {
            this.chunk = chunk;
            this.memoryMapIdx = memoryMapIdx;
            this.runOffset = runOffset;
            this.pageSize = pageSize;
            this.bitmap = new long[pageSize >> 10];
            this.Init(elemSize);
        }

        public PoolSubpage Prev
        {
            get;
            set;
        }

        public PoolSubpage Next
        {
            get;
            set;
        }

        public PoolChunk Chunk
        {
            get
            {
                return this.chunk;
            }
        }

        public int ElemSize
        {
            get
            {
                return this.elemSize;
            }
        }

        public long Allocate()
        {
            if (this.elemSize == 0)
            {
                return this.ToHandle(0);
            }
            if (this.numAvail == 0 || !this.doNotDestroy)
            {
                return -1;
            }
            var bitmapIdx = this.GetNextAvail();
            var q = bitmapIdx >> 6;
            var r = bitmapIdx & 63;
            this.bitmap[q] |= 1L << r;

            if (--this.numAvail == 0)
            {
                this.RemoveFromPool();
            }

            return this.ToHandle(bitmapIdx);
        }

        public bool Free(int bitmapIdx)
        {
            if (this.elemSize == 0)
            {
                return true;
            }
            var q = bitmapIdx >> 6;
            var r = bitmapIdx & 63;
            this.bitmap[q] ^= 1L << r;
            if (this.numAvail++ == 0)
            {
                this.nextAvail = bitmapIdx;
                this.AddToPool();
                return true;
            }
            if (this.numAvail < this.maxNumElems)
            {
                return true;
            }
            else
            {
                if (Prev == Next)
                {
                    // Do not remove if this subpage is the only one left in the pool.
                    return true;
                }
                // Remove this subpage from the pool if there are other subpages left in the pool.
                this.doNotDestroy = false;
                this.RemoveFromPool();
                return false;
            }
        }

        private void Init(int elemSize)
        {
            this.doNotDestroy = true;
            this.elemSize = elemSize;
            if (elemSize != 0)
            {
                this.maxNumElems = this.numAvail = this.pageSize / elemSize;
                this.nextAvail = 0;
                this.bitmapLength = this.maxNumElems >> 6;
                if ((this.maxNumElems & 63) != 0)
                {
                    this.bitmapLength++;
                }
                for (var i = 0; i < this.bitmapLength; i++)
                {
                    this.bitmap[i] = 0;
                }
            }
            this.AddToPool();
        }

        private int GetNextAvail()
        {
            if (this.nextAvail >= 0)
            {
                return this.nextAvail--;
            }
            return this.FindNextAvail();
        }

        private int FindNextAvail()
        {
            for (var i = 0; i < this.bitmapLength; i++)
            {
                var bits = this.bitmap[i];
                if (~bits != 0)
                {
                    return this.findNextAvail0(i, bits);
                }
            }
            return -1;
        }

        private int findNextAvail0(int i, long bits)
        {
            var baseVal = i << 6;

            for (var j = 0; j < 64; j++)
            {
                if ((bits & 1) == 0)
                {
                    var val = baseVal | j;
                    if (val < this.maxNumElems)
                    {
                        return val;
                    }
                    else
                    {
                        break;
                    }
                }
                bits >>= 1;
            }
            return -1;
        }

        private void AddToPool()
        {
            var head = this.chunk.Arena.FindSubpagePoolHead(elemSize);
            this.Prev = head;
            Next = head.Next;
            Next.Prev = this;
            head.Next = this;
        }

        private void RemoveFromPool()
        {
            Prev.Next = this.Next;
            Next.Prev = this.Prev;
            this.Next = null;
            this.Prev = null;
        }

        private long ToHandle(int bitmapIdx)
        {
            // 0x4000000000000000L=1<<62
            return 0x4000000000000000L | (long)bitmapIdx << 32 | (uint)this.memoryMapIdx;
        }
    }
}
