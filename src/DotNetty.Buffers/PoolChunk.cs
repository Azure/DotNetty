// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System.Diagnostics.Contracts;
    using System.Runtime.CompilerServices;
    using System.Text;
    using DotNetty.Common.Utilities;

    /// <summary>
    ///     Description of algorithm for PageRun/PoolSubpage allocation from PoolChunk
    ///     Notation: The following terms are important to understand the code
    ///     > page  - a page is the smallest unit of memory chunk that can be allocated
    ///     > chunk - a chunk is a collection of pages
    ///     > in this code chunkSize = 2^{maxOrder} /// pageSize
    ///     To begin we allocate a byte array of size = chunkSize
    ///     Whenever a ByteBuf of given size needs to be created we search for the first position
    ///     in the byte array that has enough empty space to accommodate the requested size and
    ///     return a (long) handle that encodes this offset information, (this memory segment is then
    ///     marked as reserved so it is always used by exactly one ByteBuf and no more)
    ///     For simplicity all sizes are normalized according to PoolArena#normalizeCapacity method
    ///     This ensures that when we request for memory segments of size >= pageSize the normalizedCapacity
    ///     equals the next nearest power of 2
    ///     To search for the first offset in chunk that has at least requested size available we construct a
    ///     complete balanced binary tree and store it in an array (just like heaps) - memoryMap
    ///     The tree looks like this (the size of each node being mentioned in the parenthesis)
    ///     depth=0        1 node (chunkSize)
    ///     depth=1        2 nodes (chunkSize/2)
    ///     ..
    ///     ..
    ///     depth=d        2^d nodes (chunkSize/2^d)
    ///     ..
    ///     depth=maxOrder 2^maxOrder nodes (chunkSize/2^{maxOrder} = pageSize)
    ///     depth=maxOrder is the last level and the leafs consist of pages
    ///     With this tree available searching in chunkArray translates like this:
    ///     To allocate a memory segment of size chunkSize/2^k we search for the first node (from left) at height k
    ///     which is unused
    ///     Algorithm:
    ///     ----------
    ///     Encode the tree in memoryMap with the notation
    ///     memoryMap[id] = x => in the subtree rooted at id, the first node that is free to be allocated
    ///     is at depth x (counted from depth=0) i.e., at depths [depth_of_id, x), there is no node that is free
    ///     As we allocate & free nodes, we update values stored in memoryMap so that the property is maintained
    ///     Initialization -
    ///     In the beginning we construct the memoryMap array by storing the depth of a node at each node
    ///     i.e., memoryMap[id] = depth_of_id
    ///     Observations:
    ///     -------------
    ///     1) memoryMap[id] = depth_of_id  => it is free / unallocated
    ///     2) memoryMap[id] > depth_of_id  => at least one of its child nodes is allocated, so we cannot allocate it, but
    ///     some of its children can still be allocated based on their availability
    ///     3) memoryMap[id] = maxOrder + 1 => the node is fully allocated & thus none of its children can be allocated, it
    ///     is thus marked as unusable
    ///     Algorithm: [allocateNode(d) => we want to find the first node (from left) at height h that can be allocated]
    ///     ----------
    ///     1) start at root (i.e., depth = 0 or id = 1)
    ///     2) if memoryMap[1] > d => cannot be allocated from this chunk
    ///     3) if left node value &lt;= h; we can allocate from left subtree so move to left and repeat until found
    ///     4) else try in right subtree
    ///     Algorithm: [allocateRun(size)]
    ///     ----------
    ///     1) Compute d = log_2(chunkSize/size)
    ///     2) Return allocateNode(d)
    ///     Algorithm: [allocateSubpage(size)]
    ///     ----------
    ///     1) use allocateNode(maxOrder) to find an empty (i.e., unused) leaf (i.e., page)
    ///     2) use this handle to construct the PoolSubpage object or if it already exists just call init(normCapacity)
    ///     note that this PoolSubpage object is added to subpagesPool in the PoolArena when we init() it
    ///     Note:
    ///     -----
    ///     In the implementation for improving cache coherence,
    ///     we store 2 pieces of information (i.e, 2 byte vals) as a short value in memoryMap
    ///     memoryMap[id]= (depth_of_id, x)
    ///     where as per convention defined above
    ///     the second value (i.e, x) indicates that the first node which is free to be allocated is at depth x (from root)
    /// </summary>
    sealed class PoolChunk<T> : IPoolChunkMetric
    {
        internal enum PoolChunkOrigin
        {
            Pooled,
            UnpooledHuge,
            UnpooledNormal
        }

        internal readonly PoolArena<T> Arena;
        internal readonly T Memory;
        internal readonly PoolChunkOrigin Origin;

        readonly sbyte[] memoryMap;
        readonly sbyte[] depthMap;
        readonly PoolSubpage<T>[] subpages;
        /** Used to determine if the requested capacity is equal to or greater than pageSize. */
        readonly int subpageOverflowMask;
        readonly int pageSize;
        readonly int pageShifts;
        readonly int maxOrder;
        readonly int chunkSize;
        readonly int log2ChunkSize;
        readonly int maxSubpageAllocs;
        /** Used to mark memory as unusable */
        readonly sbyte unusable;

        int freeBytes;

        internal PoolChunkList<T> Parent;
        internal PoolChunk<T> Prev;
        internal PoolChunk<T> Next;

        // TODO: Test if adding padding helps under contention
        //private long pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7;

        internal PoolChunk(PoolArena<T> arena, T memory, int pageSize, int maxOrder, int pageShifts, int chunkSize)
        {
            Contract.Requires(maxOrder < 30, "maxOrder should be < 30, but is: " + maxOrder);

            this.Origin = PoolChunkOrigin.Pooled;
            this.Arena = arena;
            this.Memory = memory;
            this.pageSize = pageSize;
            this.pageShifts = pageShifts;
            this.maxOrder = maxOrder;
            this.chunkSize = chunkSize;
            this.unusable = (sbyte)(maxOrder + 1);
            this.log2ChunkSize = IntegerExtensions.Log2(chunkSize);
            this.subpageOverflowMask = ~(pageSize - 1);
            this.freeBytes = chunkSize;

            Contract.Assert(maxOrder < 30, "maxOrder should be < 30, but is: " + maxOrder);
            this.maxSubpageAllocs = 1 << maxOrder;

            // Generate the memory map.
            this.memoryMap = new sbyte[this.maxSubpageAllocs << 1];
            this.depthMap = new sbyte[this.memoryMap.Length];
            int memoryMapIndex = 1;
            for (int d = 0; d <= maxOrder; ++d)
            {
                // move down the tree one level at a time
                int depth = 1 << d;
                for (int p = 0; p < depth; ++p)
                {
                    // in each level traverse left to right and set value to the depth of subtree
                    this.memoryMap[memoryMapIndex] = (sbyte)d;
                    this.depthMap[memoryMapIndex] = (sbyte)d;
                    memoryMapIndex++;
                }
            }

            this.subpages = this.NewSubpageArray(this.maxSubpageAllocs);
        }

        /** Creates a special chunk that is not pooled. */

        internal PoolChunk(PoolArena<T> arena, T memory, int size, bool huge)
        {
            this.Origin = huge ? PoolChunkOrigin.UnpooledHuge : PoolChunkOrigin.UnpooledNormal;
            this.Arena = arena;
            this.Memory = memory;
            this.memoryMap = null;
            this.depthMap = null;
            this.subpages = null;
            this.subpageOverflowMask = 0;
            this.pageSize = 0;
            this.pageShifts = 0;
            this.maxOrder = 0;
            this.unusable = (sbyte)(this.maxOrder + 1);
            this.chunkSize = size;
            this.log2ChunkSize = IntegerExtensions.Log2(this.chunkSize);
            this.maxSubpageAllocs = 0;
        }

        PoolSubpage<T>[] NewSubpageArray(int size) => new PoolSubpage<T>[size];

        internal bool Unpooled => this.Origin != PoolChunkOrigin.Pooled;

        public int Usage
        {
            get
            {
                int freeBytes = this.freeBytes;
                if (freeBytes == 0)
                {
                    return 100;
                }

                int freePercentage = (int)(freeBytes * 100L / this.chunkSize);
                if (freePercentage == 0)
                {
                    return 99;
                }
                return 100 - freePercentage;
            }
        }

        internal long Allocate(int normCapacity)
        {
            if ((normCapacity & this.subpageOverflowMask) != 0)
            {
                // >= pageSize
                return this.AllocateRun(normCapacity);
            }
            else
            {
                return this.AllocateSubpage(normCapacity);
            }
        }

        /**
         * Update method used by allocate
         * This is triggered only when a successor is allocated and all its predecessors
         * need to update their state
         * The minimal depth at which subtree rooted at id has some free space
         *
         * @param id id
         */

        void UpdateParentsAlloc(int id)
        {
            while (id > 1)
            {
                int parentId = id.RightUShift(1);
                sbyte val1 = this.Value(id);
                sbyte val2 = this.Value(id ^ 1);
                sbyte val = val1 < val2 ? val1 : val2;
                this.SetValue(parentId, val);
                id = parentId;
            }
        }

        /**
         * Update method used by free
         * This needs to handle the special case when both children are completely free
         * in which case parent be directly allocated on request of size = child-size * 2
         *
         * @param id id
         */

        void UpdateParentsFree(int id)
        {
            int logChild = this.Depth(id) + 1;
            while (id > 1)
            {
                int parentId = id.RightUShift(1);
                sbyte val1 = this.Value(id);
                sbyte val2 = this.Value(id ^ 1);
                logChild -= 1; // in first iteration equals log, subsequently reduce 1 from logChild as we traverse up

                if (val1 == logChild && val2 == logChild)
                {
                    this.SetValue(parentId, (sbyte)(logChild - 1));
                }
                else
                {
                    sbyte val = val1 < val2 ? val1 : val2;
                    this.SetValue(parentId, val);
                }

                id = parentId;
            }
        }

        /**
         * Algorithm to allocate an index in memoryMap when we query for a free node
         * at depth d
         *
         * @param d depth
         * @return index in memoryMap
         */

        int AllocateNode(int d)
        {
            int id = 1;
            int initial = -(1 << d); // has last d bits = 0 and rest all = 1
            sbyte val = this.Value(id);
            if (val > d)
            {
                // unusable
                return -1;
            }
            while (val < d || (id & initial) == 0)
            {
                // id & initial == 1 << d for all ids at depth d, for < d it is 0
                id <<= 1;
                val = this.Value(id);
                if (val > d)
                {
                    id ^= 1;
                    val = this.Value(id);
                }
            }
            sbyte value = this.Value(id);
            Contract.Assert(value == d && (id & initial) == 1 << d, $"val = {value}, id & initial = {id & initial}, d = {d}");
            this.SetValue(id, this.unusable); // mark as unusable
            this.UpdateParentsAlloc(id);
            return id;
        }

        /**
         * Allocate a run of pages (>=1)
         *
         * @param normCapacity normalized capacity
         * @return index in memoryMap
         */

        long AllocateRun(int normCapacity)
        {
            int d = this.maxOrder - (IntegerExtensions.Log2(normCapacity) - this.pageShifts);
            int id = this.AllocateNode(d);
            if (id < 0)
            {
                return id;
            }
            this.freeBytes -= this.RunLength(id);
            return id;
        }

        /**
         * Create/ initialize a new PoolSubpage of normCapacity
         * Any PoolSubpage created/ initialized here is added to subpage pool in the PoolArena that owns this PoolChunk
         *
         * @param normCapacity normalized capacity
         * @return index in memoryMap
         */

        long AllocateSubpage(int normCapacity)
        {
            int d = this.maxOrder; // subpages are only be allocated from pages i.e., leaves
            int id = this.AllocateNode(d);
            if (id < 0)
            {
                return id;
            }

            PoolSubpage<T>[] subpages = this.subpages;
            int pageSize = this.pageSize;

            this.freeBytes -= pageSize;

            int subpageIdx = this.SubpageIdx(id);
            PoolSubpage<T> subpage = subpages[subpageIdx];
            if (subpage == null)
            {
                subpage = new PoolSubpage<T>(this, id, this.RunOffset(id), pageSize, normCapacity);
                subpages[subpageIdx] = subpage;
            }
            else
            {
                subpage.Init(normCapacity);
            }
            return subpage.Allocate();
        }

        /**
         * Free a subpage or a run of pages
         * When a subpage is freed from PoolSubpage, it might be added back to subpage pool of the owning PoolArena
         * If the subpage pool in PoolArena has at least one other PoolSubpage of given elemSize, we can
         * completely free the owning Page so it is available for subsequent allocations
         *
         * @param handle handle to free
         */

        internal void Free(long handle)
        {
            int memoryMapIdx = MemoryMapIdx(handle);
            int bitmapIdx = BitmapIdx(handle);

            if (bitmapIdx != 0)
            {
                // free a subpage
                PoolSubpage<T> subpage = this.subpages[this.SubpageIdx(memoryMapIdx)];
                Contract.Assert(subpage != null && subpage.DoNotDestroy);

                // Obtain the head of the PoolSubPage pool that is owned by the PoolArena and synchronize on it.
                // This is need as we may add it back and so alter the linked-list structure.
                PoolSubpage<T> head = this.Arena.FindSubpagePoolHead(subpage.ElemSize);
                lock (head)
                {
                    if (subpage.Free(bitmapIdx & 0x3FFFFFFF))
                    {
                        return;
                    }
                }
            }
            this.freeBytes += this.RunLength(memoryMapIdx);
            this.SetValue(memoryMapIdx, this.Depth(memoryMapIdx));
            this.UpdateParentsFree(memoryMapIdx);
        }

        internal void InitBuf(PooledByteBuffer<T> buf, long handle, int reqCapacity)
        {
            int memoryMapIdx = MemoryMapIdx(handle);
            int bitmapIdx = BitmapIdx(handle);
            if (bitmapIdx == 0)
            {
                sbyte val = this.Value(memoryMapIdx);
                Contract.Assert(val == this.unusable, val.ToString());
                buf.Init(this, handle, this.RunOffset(memoryMapIdx), reqCapacity, this.RunLength(memoryMapIdx),
                    this.Arena.Parent.ThreadCache<T>());
            }
            else
            {
                this.InitBufWithSubpage(buf, handle, bitmapIdx, reqCapacity);
            }
        }

        internal void InitBufWithSubpage(PooledByteBuffer<T> buf, long handle, int reqCapacity) => this.InitBufWithSubpage(buf, handle, BitmapIdx(handle), reqCapacity);

        void InitBufWithSubpage(PooledByteBuffer<T> buf, long handle, int bitmapIdx, int reqCapacity)
        {
            Contract.Assert(bitmapIdx != 0);

            int memoryMapIdx = MemoryMapIdx(handle);

            PoolSubpage<T> subpage = this.subpages[this.SubpageIdx(memoryMapIdx)];
            Contract.Assert(subpage.DoNotDestroy);
            Contract.Assert(reqCapacity <= subpage.ElemSize);

            buf.Init(
                this, handle,
                this.RunOffset(memoryMapIdx) + (bitmapIdx & 0x3FFFFFFF) * subpage.ElemSize, reqCapacity, subpage.ElemSize,
                this.Arena.Parent.ThreadCache<T>());
        }

        sbyte Value(int id) => this.memoryMap[id];

        void SetValue(int id, sbyte val) => this.memoryMap[id] = val;

        sbyte Depth(int id) => this.depthMap[id];

        /// represents the size in #bytes supported by node 'id' in the tree
        int RunLength(int id) => 1 << this.log2ChunkSize - this.Depth(id);

        int RunOffset(int id)
        {
            // represents the 0-based offset in #bytes from start of the byte-array chunk
            int shift = id ^ 1 << this.Depth(id);
            return shift * this.RunLength(id);
        }

        int SubpageIdx(int memoryMapIdx) => memoryMapIdx ^ this.maxSubpageAllocs; // remove highest set bit, to get offset

        static int MemoryMapIdx(long handle) => (int)handle;

        static int BitmapIdx(long handle) => (int)handle.RightUShift(IntegerExtensions.SizeInBits);

        public int ChunkSize => this.chunkSize;

        public int FreeBytes => this.freeBytes;

        public override string ToString()
        {
            return new StringBuilder()
                .Append("Chunk(")
                .Append(RuntimeHelpers.GetHashCode(this).ToString("X"))
                .Append(": ")
                .Append(this.Usage)
                .Append("%, ")
                .Append(this.chunkSize - this.freeBytes)
                .Append('/')
                .Append(this.chunkSize)
                .Append(')')
                .ToString();
        }
    }
}