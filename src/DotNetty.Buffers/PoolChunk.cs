// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;

    internal class PoolChunk
    {
        private byte[] memoryMap;
        private byte[] depthMap;
        private byte[] buffer;
        private PoolSubpage[] subpages;
        private bool unpooled;
        private int pageSize;
        private int pageShifts;
        private int chunkSize;
        private int maxSubpageAllocs;
        private int freeBytes;
        private int log2ChunkSize;
        private byte unusable;
        private int maxOrder;
        private readonly PoolArena arena;

        public PoolChunk(PoolArena arena, int pageSize, int maxOrder, int pageShifts, int chunkSize)
        {
            this.arena = arena;
            this.buffer = new byte[chunkSize];
            this.maxOrder = maxOrder;
            this.pageSize = pageSize;
            this.pageShifts = pageShifts;
            this.freeBytes = this.chunkSize = chunkSize;
            this.unusable = (byte)(maxOrder + 1);
            this.maxSubpageAllocs = 1 << maxOrder;
            this.memoryMap = new byte[this.maxSubpageAllocs << 1];
            this.depthMap = new byte[this.memoryMap.Length];
            var memoryMapIndex = 1;
            for (var d = 0; d <= maxOrder; d++)
            {
                var depth = 1 << d;
                for (int p = 0; p < depth; p++)
                {
                    this.memoryMap[memoryMapIndex] = (byte)d;
                    this.depthMap[memoryMapIndex] = (byte)d;
                    memoryMapIndex++;
                }
            }
            this.subpages = new PoolSubpage[this.maxSubpageAllocs];
            this.log2ChunkSize = Log2(chunkSize);
            this.unpooled = false;
        }

        public PoolChunk(PoolArena arena, int chunkSize)
        {
            this.unpooled = true;
            this.arena = arena;
            this.buffer = new byte[chunkSize];
        }

        public PoolChunk Prev
        {
            get;
            set;
        }

        public PoolChunk Next
        {
            get;
            set;
        }

        public PoolArena Arena
        {
            get
            {
                return this.arena;
            }
        }

        public byte[] Buffer
        {
            get
            {
                return this.buffer;
            }
        }

        public int Usage
        {
            get
            {
                if (this.freeBytes == 0)
                {
                    return 100;
                }

                var freePercentage = (int)(this.freeBytes * 100L / this.chunkSize);
                if (freePercentage == 0)
                {
                    return 99;
                }
                return 100 - freePercentage;
            }
        }

        public bool Unpooled
        {
            get
            {
                return this.unpooled;
            }
        }

        public long Allocate(int normCapacity)
        {
            if (this.arena.IsTinyOrSmall(normCapacity))
            {
                return this.AllocatePage(normCapacity);
            }
            return this.AllocateRun(normCapacity);
        }

        public void Free(long handle)
        {
            var memoryMapIdx = (int)handle;
            var bitmapIdx = (int)(handle >> 32);
            if (bitmapIdx != 0)
            {
                // free a subpage
                var subpage = this.subpages[this.PageIdx(memoryMapIdx)];
                if (subpage.Free(bitmapIdx & 0x3FFFFFFF))
                {
                    return;
                }
            }
            this.freeBytes += this.RunLength(memoryMapIdx);
            this.memoryMap[memoryMapIdx] = this.depthMap[memoryMapIdx];
            this.UpdateParentsFree(memoryMapIdx);
        }

        public void InitBuffer(PooledByteBuffer buffer, long handle, int reqCapacity)
        {
            int memoryMapIdx = (int)handle;
            int bitmapIdx = (int)(handle >> 32);
            if (bitmapIdx == 0)
            {
                //=pagesize
                buffer.Init(this, handle, this.RunOffset(memoryMapIdx), reqCapacity, this.RunLength(memoryMapIdx));
            }
            else
            {
                this.InitBufferWithSubpage(buffer, handle, bitmapIdx, reqCapacity);
            }
        }

        public void InitBufferWithSubpage(PooledByteBuffer buf, long handle, int reqCapacity)
        {
            InitBufferWithSubpage(buf, handle, (int)(handle >> 32), reqCapacity);
        }

        private void InitBufferWithSubpage(PooledByteBuffer buf, long handle, int bitmapIdx, int reqCapacity)
        {
            var memoryMapIdx = (int)handle;
            var idx = this.PageIdx(memoryMapIdx);
            var subpage = this.subpages[idx];
            buf.Init(this, handle, this.RunOffset(memoryMapIdx) + (bitmapIdx & 0x3FFFFFFF) * subpage.ElemSize, reqCapacity, subpage.ElemSize);
        }

        private long AllocateRun(int normCapacity)
        {
            var d = this.maxOrder - (Log2(normCapacity) - this.pageShifts);
            var id = this.AllocateNode(d);
            if (id < 0)
            {
                return id;
            }
            this.freeBytes -= this.RunLength(id);
            return id;
        }

        private long AllocatePage(int normCapacity)
        {
            var id = this.AllocateNode(this.maxOrder);
            if (id < 0)
            {
                return id;
            }
            this.freeBytes -= this.pageSize;

            int subpageIdx = this.PageIdx(id);
            var subpage = this.subpages[subpageIdx];
            if (subpage == null)
            {
                subpage = new PoolSubpage(this, id, this.RunOffset(id), this.pageSize, normCapacity);
                this.subpages[subpageIdx] = subpage;
            }
            return subpage.Allocate();
        }

        private int AllocateNode(int d)
        {
            var id = 1;
            var initial = -(1 << d);
            var val = this.memoryMap[id];
            if (val > d)
            { // unusable
                return -1;
            }
            while (val < d || (id & initial) == 0)
            {
                id <<= 1;
                val = this.memoryMap[id];
                if (val > d)
                {
                    id ^= 1;
                    val = this.memoryMap[id];
                }
            }
            this.memoryMap[id] = this.unusable; // mark as unusable
            this.UpdateParentsAlloc(id);
            return id;
        }

        private void UpdateParentsAlloc(int id)
        {
            while (id > 1)
            {
                var parentId = id >> 1;
                var val1 = this.memoryMap[id];
                var val2 = this.memoryMap[id ^ 1];
                var val = val1 < val2 ? val1 : val2;
                this.memoryMap[parentId] = val;
                id = parentId;
            }
        }

        private void UpdateParentsFree(int id)
        {
            var logChild = this.depthMap[id] + 1;
            while (id > 1)
            {
                var parentId = id >> 1;
                var val1 = this.memoryMap[id];
                var val2 = this.memoryMap[id ^ 1];
                logChild -= 1; // in first iteration equals log, subsequently reduce 1 from logChild as we traverse up

                if (val1 == logChild && val2 == logChild)
                {
                    this.memoryMap[parentId] = (byte)(logChild - 1);
                }
                else
                {
                    var val = val1 < val2 ? val1 : val2;
                    this.memoryMap[parentId] = val;
                }
                id = parentId;
            }
        }

        private int RunOffset(int id)
        {
            var shift = id ^ 1 << this.depthMap[id];
            return shift * this.RunLength(id);
        }

        private int RunLength(int id)
        {
            return 1 << this.log2ChunkSize - this.depthMap[id];
        }

        private int PageIdx(int memoryMapIdx)
        {
            return memoryMapIdx ^ this.maxSubpageAllocs;
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
