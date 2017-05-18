// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    public sealed class CompositeByteBuffer : AbstractReferenceCountedByteBuffer
    {
        class ComponentEntry
        {
            public readonly IByteBuffer Buffer;
            public readonly int Length;
            public int Offset;
            public int EndOffset;

            public ComponentEntry(IByteBuffer buffer)
            {
                this.Buffer = buffer;
                this.Length = buffer.ReadableBytes;
            }

            public void FreeIfNecessary() => this.Buffer.Release();
        }

        static readonly ArraySegment<byte> EmptyNioBuffer = Unpooled.Empty.GetIoBuffer();

        readonly IResourceLeak leak;
        readonly IByteBufferAllocator allocator;
        readonly List<ComponentEntry> components = new List<ComponentEntry>();
        readonly int maxNumComponents;

        bool freed;

        public CompositeByteBuffer(IByteBufferAllocator allocator, int maxNumComponents)
            : base(AbstractByteBufferAllocator.DefaultMaxCapacity)
        {
            Contract.Requires(allocator != null);
            Contract.Requires(maxNumComponents >= 2);

            this.allocator = allocator;
            this.maxNumComponents = maxNumComponents;
            this.leak = LeakDetector.Open(this);
        }

        public CompositeByteBuffer(IByteBufferAllocator allocator, int maxNumComponents, params IByteBuffer[] buffers)
            : base(AbstractByteBufferAllocator.DefaultMaxCapacity)
        {
            Contract.Requires(allocator != null);
            Contract.Requires(maxNumComponents >= 2);

            this.allocator = allocator;
            this.maxNumComponents = maxNumComponents;

            this.AddComponents0(0, buffers);
            this.ConsolidateIfNeeded();
            this.SetIndex(0, this.Capacity);
            this.leak = LeakDetector.Open(this);
        }

        public CompositeByteBuffer(IByteBufferAllocator allocator, int maxNumComponents, IByteBuffer[] buffers, int offset, int length)
            : base(AbstractByteBufferAllocator.DefaultMaxCapacity)
        {
            Contract.Requires(allocator != null);
            Contract.Requires(maxNumComponents >= 2);

            this.allocator = allocator;
            this.maxNumComponents = maxNumComponents;
            this.AddComponents0(0, buffers, offset, length);
            this.ConsolidateIfNeeded();
            this.SetIndex(0, this.Capacity);
            this.leak = LeakDetector.Open(this);
        }

        public CompositeByteBuffer(
            IByteBufferAllocator allocator, int maxNumComponents, IEnumerable<IByteBuffer> buffers)
            : base(AbstractByteBufferAllocator.DefaultMaxCapacity)
        {
            Contract.Requires(allocator != null);
            Contract.Requires(maxNumComponents >= 2);

            this.allocator = allocator;
            this.maxNumComponents = maxNumComponents;
            this.AddComponents0(0, buffers);
            this.ConsolidateIfNeeded();
            this.SetIndex(0, this.Capacity);
            this.leak = LeakDetector.Open(this);
        }

        /// <summary>
        ///     Add the given {@link IByteBuffer}.
        ///     Be aware that this method does not increase the {@code writerIndex} of the {@link CompositeByteBuffer}.
        ///     If you need to have it increased you need to handle it by your own.
        ///     @param buffer the {@link IByteBuffer} to add
        /// </summary>
        public CompositeByteBuffer AddComponent(IByteBuffer buffer)
        {
            this.AddComponent0(this.components.Count, buffer);
            this.ConsolidateIfNeeded();
            return this;
        }

        /// <summary>
        ///     Add the given {@link IByteBuffer}s.
        ///     Be aware that this method does not increase the {@code writerIndex} of the {@link CompositeByteBuffer}.
        ///     If you need to have it increased you need to handle it by your own.
        ///     @param buffers the {@link IByteBuffer}s to add
        /// </summary>
        public CompositeByteBuffer AddComponents(params IByteBuffer[] buffers)
        {
            this.AddComponents0(this.components.Count, buffers);
            this.ConsolidateIfNeeded();
            return this;
        }

        /// <summary>
        ///     Add the given {@link IByteBuffer}s.
        ///     Be aware that this method does not increase the {@code writerIndex} of the {@link CompositeByteBuffer}.
        ///     If you need to have it increased you need to handle it by your own.
        ///     @param buffers the {@link IByteBuffer}s to add
        /// </summary>
        public CompositeByteBuffer AddComponents(IEnumerable<IByteBuffer> buffers)
        {
            this.AddComponents0(this.components.Count, buffers);
            this.ConsolidateIfNeeded();
            return this;
        }

        /// <summary>
        ///     Add the given {@link IByteBuffer} on the specific index.
        ///     Be aware that this method does not increase the {@code writerIndex} of the {@link CompositeByteBuffer}.
        ///     If you need to have it increased you need to handle it by your own.
        ///     @param cIndex the index on which the {@link IByteBuffer} will be added
        ///     @param buffer the {@link IByteBuffer} to add
        /// </summary>
        public CompositeByteBuffer AddComponent(int cIndex, IByteBuffer buffer)
        {
            this.AddComponent0(cIndex, buffer);
            this.ConsolidateIfNeeded();
            return this;
        }

        int AddComponent0(int cIndex, IByteBuffer buffer)
        {
            Contract.Requires(buffer != null);

            this.CheckComponentIndex(cIndex);

            int readableBytes = buffer.ReadableBytes;

            // No need to consolidate - just add a component to the list.
            var c = new ComponentEntry(buffer.WithOrder(ByteOrder.BigEndian).Slice());
            if (cIndex == this.components.Count)
            {
                this.components.Add(c);
                if (cIndex == 0)
                {
                    c.EndOffset = readableBytes;
                }
                else
                {
                    ComponentEntry prev = this.components[cIndex - 1];
                    c.Offset = prev.EndOffset;
                    c.EndOffset = c.Offset + readableBytes;
                }
            }
            else
            {
                this.components.Insert(cIndex, c);
                if (readableBytes != 0)
                {
                    this.UpdateComponentOffsets(cIndex);
                }
            }
            return cIndex;
        }

        /// <summary>
        ///     Add the given {@link IByteBuffer}s on the specific index
        ///     Be aware that this method does not increase the {@code writerIndex} of the {@link CompositeByteBuffer}.
        ///     If you need to have it increased you need to handle it by your own.
        ///     @param cIndex the index on which the {@link IByteBuffer} will be added.
        ///     @param buffers the {@link IByteBuffer}s to add
        /// </summary>
        public CompositeByteBuffer AddComponents(int cIndex, params IByteBuffer[] buffers)
        {
            this.AddComponents0(cIndex, buffers);
            this.ConsolidateIfNeeded();
            return this;
        }

        void AddComponents0(int cIndex, IEnumerable<IByteBuffer> buffers)
        {
            Contract.Requires(buffers != null);

            this.CheckComponentIndex(cIndex);

            // No need for consolidation
            foreach (IByteBuffer b in buffers)
            {
                if (b == null)
                {
                    break;
                }
                this.AddComponent0(cIndex, b);
                cIndex++;
                int size = this.components.Count;
                if (cIndex > size)
                {
                    cIndex = size;
                }
            }
        }

        int AddComponents0(int cIndex, IByteBuffer[] buffers, int offset, int length)
        {
            Contract.Requires(buffers != null);

            int i = offset;
            try
            {
                this.CheckComponentIndex(cIndex);

                // No need for consolidation
                while (i < length)
                {
                    // Increment i now to prepare for the next iteration and prevent a duplicate release (addComponent0
                    // will release if an exception occurs, and we also release in the finally block here).
                    IByteBuffer b = buffers[i++];
                    if (b == null)
                    {
                        break;
                    }

                    cIndex = this.AddComponent0(cIndex, b) + 1;
                    int size = this.components.Count;
                    if (cIndex > size)
                    {
                        cIndex = size;
                    }

                }

                return cIndex;
            }
            finally
            {
                for (; i < length; ++i)
                {
                    IByteBuffer b = buffers[i];
                    if (b != null)
                    {
                        try
                        {
                            b.Release();
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                }
            }

        }

        /// <summary>
        ///     Add the given {@link ByteBuf}s on the specific index
        ///     Be aware that this method does not increase the {@code writerIndex} of the {@link CompositeByteBuffer}.
        ///     If you need to have it increased you need to handle it by your own.
        ///     @param cIndex the index on which the {@link IByteBuffer} will be added.
        ///     @param buffers the {@link IByteBuffer}s to add
        /// </summary>
        public CompositeByteBuffer AddComponents(int cIndex, IEnumerable<IByteBuffer> buffers)
        {
            this.AddComponents0(cIndex, buffers);
            this.ConsolidateIfNeeded();
            return this;
        }

        /// <summary>
        ///     This should only be called as last operation from a method as this may adjust the underlying
        ///     array of components and so affect the index etc.
        /// </summary>
        void ConsolidateIfNeeded()
        {
            // Consolidate if the number of components will exceed the allowed maximum by the current
            // operation.
            int numComponents = this.components.Count;
            if (numComponents > this.maxNumComponents)
            {
                int capacity = this.components[numComponents - 1].EndOffset;

                IByteBuffer consolidated = this.AllocateBuffer(capacity);

                // We're not using foreach to avoid creating an iterator.
                for (int i = 0; i < numComponents; i++)
                {
                    ComponentEntry c1 = this.components[i];
                    IByteBuffer b = c1.Buffer;
                    consolidated.WriteBytes(b);
                    c1.FreeIfNecessary();
                }
                var c = new ComponentEntry(consolidated);
                c.EndOffset = c.Length;
                this.components.Clear();
                this.components.Add(c);
            }
        }

        void CheckComponentIndex(int cIndex)
        {
            this.EnsureAccessible();
            if (cIndex < 0 || cIndex > this.components.Count)
            {
                throw new ArgumentOutOfRangeException($"cIndex: {cIndex} (expected: >= 0 && <= numComponents({this.components.Count}))");
            }
        }

        void CheckComponentIndex(int cIndex, int numComponents)
        {
            this.EnsureAccessible();
            if (cIndex < 0 || cIndex + numComponents > this.components.Count)
            {
                throw new ArgumentOutOfRangeException($"cIndex: {cIndex}, numComponents: {numComponents} " + $"(expected: cIndex >= 0 && cIndex + numComponents <= totalNumComponents({this.components.Count}))");
            }
        }

        void UpdateComponentOffsets(int cIndex)
        {
            int size = this.components.Count;
            if (size <= cIndex)
            {
                return;
            }

            ComponentEntry c = this.components[cIndex];
            if (cIndex == 0)
            {
                c.Offset = 0;
                c.EndOffset = c.Length;
                cIndex++;
            }

            for (int i = cIndex; i < size; i++)
            {
                ComponentEntry prev = this.components[i - 1];
                ComponentEntry cur = this.components[i];
                cur.Offset = prev.EndOffset;
                cur.EndOffset = cur.Offset + cur.Length;
            }
        }

        /// <summary>
        ///     Remove the {@link IByteBuffer} from the given index.
        ///     @param cIndex the index on from which the {@link IByteBuffer} will be remove
        /// </summary>
        public CompositeByteBuffer RemoveComponent(int cIndex)
        {
            this.CheckComponentIndex(cIndex);
            ComponentEntry comp = this.components[cIndex];
            this.components.RemoveAt(cIndex);
            comp.FreeIfNecessary();
            if (comp.Length > 0)
            {
                // Only need to call updateComponentOffsets if the length was > 0
                this.UpdateComponentOffsets(cIndex);
            }
            return this;
        }

        /// <summary>
        ///     Remove the number of {@link IByteBuffer}s starting from the given index.
        ///     @param cIndex the index on which the {@link IByteBuffer}s will be started to removed
        ///     @param numComponents the number of components to remove
        /// </summary>
        public CompositeByteBuffer RemoveComponents(int cIndex, int numComponents)
        {
            this.CheckComponentIndex(cIndex, numComponents);

            if (numComponents == 0)
            {
                return this;
            }
            bool needsUpdate = false;
            for (int i = cIndex + numComponents; i >= cIndex; i--)
            {
                ComponentEntry c = this.components[i];
                needsUpdate |= c.Length > 0;
                c.FreeIfNecessary();
                this.components.RemoveAt(i);
            }

            if (needsUpdate)
            {
                // Only need to call updateComponentOffsets if the length was > 0
                this.UpdateComponentOffsets(cIndex);
            }
            return this;
        }

        public IEnumerator<IByteBuffer> GetEnumerator()
        {
            this.EnsureAccessible();
            foreach (ComponentEntry c in this.components)
            {
                yield return c.Buffer;
            }
        }

        /// <summary>
        ///     Same with {@link #slice(int, int)} except that this method returns a list.
        /// </summary>
        public IList<IByteBuffer> Decompose(int offset, int length)
        {
            this.CheckIndex(offset, length);
            if (length == 0)
            {
                return new ReadOnlyCollection<IByteBuffer>(new IByteBuffer[0]); // todo: optimize
            }

            int componentId = this.ToComponentIndex(offset);
            var slice = new List<IByteBuffer>(this.components.Count);

            // The first component
            ComponentEntry firstC = this.components[componentId];
            IByteBuffer first = firstC.Buffer.Duplicate();
            first.SetReaderIndex(offset - firstC.Offset);

            IByteBuffer buffer = first;
            int bytesToSlice = length;
            do
            {
                int readableBytes = buffer.ReadableBytes;
                if (bytesToSlice <= readableBytes)
                {
                    // Last component
                    buffer.SetWriterIndex(buffer.ReaderIndex + bytesToSlice);
                    slice.Add(buffer);
                    break;
                }
                else
                {
                    // Not the last component
                    slice.Add(buffer);
                    bytesToSlice -= readableBytes;
                    componentId++;

                    // Fetch the next component.
                    buffer = this.components[componentId].Buffer.Duplicate();
                }
            }
            while (bytesToSlice > 0);

            // Slice all components because only readable bytes are interesting.
            for (int i = 0; i < slice.Count; i++)
            {
                slice[i] = slice[i].Slice();
            }

            return slice;
        }

        public override int IoBufferCount
        {
            get
            {
                switch (this.components.Count)
                {
                    case 0:
                        return 1;
                    case 1:
                        return this.components[0].Buffer.IoBufferCount;
                    default:
                        int count = 0;
                        int componentsCount = this.components.Count;
                        for (int i = 0; i < componentsCount; i++)
                        {
                            ComponentEntry c = this.components[i];
                            count += c.Buffer.IoBufferCount;
                        }
                        return count;
                }
            }
        }

        public override ArraySegment<byte> GetIoBuffer(int index, int length)
        {
            this.CheckIndex(index, length);

            switch (this.components.Count)
            {
                case 0:
                    return EmptyNioBuffer;
                case 1:
                    IByteBuffer buf = this.components[0].Buffer;
                    if (buf.IoBufferCount == 1)
                    {
                        return this.components[0].Buffer.GetIoBuffer(index, length);
                    }
                    break;
            }

            var merged = new byte[length];
            ArraySegment<byte>[] buffers = this.GetIoBuffers(index, length);

            int offset = 0;
            foreach (ArraySegment<byte> buf in buffers)
            {
                Contract.Assert(merged.Length - offset >= buf.Count);
                System.Array.Copy(buf.Array, buf.Offset, merged, offset, buf.Count);
                offset += buf.Count;
            }

            return new ArraySegment<byte>(merged);
        }

        public override ArraySegment<byte>[] GetIoBuffers(int index, int length)
        {
            this.CheckIndex(index, length);
            if (length == 0)
            {
                return new[] { EmptyNioBuffer };
            }

            var buffers = new List<ArraySegment<byte>>(this.components.Count);
            int i = this.ToComponentIndex(index);
            while (length > 0)
            {
                ComponentEntry c = this.components[i];
                IByteBuffer s = c.Buffer;
                int adjustment = c.Offset;
                int localLength = Math.Min(length, s.Capacity - (index - adjustment));
                switch (s.IoBufferCount)
                {
                    case 0:
                        throw new NotSupportedException();
                    case 1:
                        buffers.Add(s.GetIoBuffer(index - adjustment, localLength));
                        break;
                    default:
                        buffers.AddRange(s.GetIoBuffers(index - adjustment, localLength));
                        break;
                }

                index += localLength;
                length -= localLength;
                i++;
            }

            return buffers.ToArray();
        }

        public override bool HasArray
        {
            get
            {
                switch (this.components.Count)
                {
                    case 0:
                        return true;
                    case 1:
                        return this.components[0].Buffer.HasArray;
                    default:
                        return false;
                }
            }
        }

        public override byte[] Array
        {
            get
            {
                switch (this.components.Count)
                {
                    case 0:
                        return ArrayExtensions.ZeroBytes;
                    case 1:
                        return this.components[0].Buffer.Array;
                    default:
                        throw new NotSupportedException();
                }
            }
        }

        public override int ArrayOffset
        {
            get
            {
                switch (this.components.Count)
                {
                    case 0:
                        return 0;
                    case 1:
                        return this.components[0].Buffer.ArrayOffset;
                    default:
                        throw new NotSupportedException();
                }
            }
        }

        public override int Capacity
        {
            get
            {
                int numComponents = this.components.Count;
                if (numComponents == 0)
                {
                    return 0;
                }
                return this.components[numComponents - 1].EndOffset;
            }
        }

        public override IByteBuffer AdjustCapacity(int newCapacity)
        {
            this.CheckNewCapacity(newCapacity);

            int oldCapacity = this.Capacity;
            if (newCapacity > oldCapacity)
            {
                int paddingLength = newCapacity - oldCapacity;
                IByteBuffer padding;
                int nComponents = this.components.Count;
                if (nComponents < this.maxNumComponents)
                {
                    padding = this.AllocateBuffer(paddingLength);
                    padding.SetIndex(0, paddingLength);
                    this.AddComponent0(this.components.Count, padding);
                }
                else
                {
                    padding = this.AllocateBuffer(paddingLength);
                    padding.SetIndex(0, paddingLength);
                    // FIXME: No need to create a padding buffer and consolidate.
                    // Just create a big single buffer and put the current content there.
                    this.AddComponent0(this.components.Count, padding);
                    this.ConsolidateIfNeeded();
                }
            }
            else if (newCapacity < oldCapacity)
            {
                int bytesToTrim = oldCapacity - newCapacity;
                for (int i = this.components.Count - 1; i >= 0; i--)
                {
                    ComponentEntry c = this.components[i];
                    if (bytesToTrim >= c.Length)
                    {
                        bytesToTrim -= c.Length;
                        this.components.RemoveAt(i);
                        continue;
                    }

                    // Replace the last component with the trimmed slice.
                    var newC = new ComponentEntry(c.Buffer.Slice(0, c.Length - bytesToTrim));
                    newC.Offset = c.Offset;
                    newC.EndOffset = newC.Offset + newC.Length;
                    this.components[i] = newC;
                    break;
                }

                if (this.ReaderIndex > newCapacity)
                {
                    this.SetIndex(newCapacity, newCapacity);
                }
                else if (this.WriterIndex > newCapacity)
                {
                    this.SetWriterIndex(newCapacity);
                }
            }
            return this;
        }

        public override IByteBufferAllocator Allocator => this.allocator;

        public override ByteOrder Order => ByteOrder.BigEndian;

        /// <summary>
        ///     Return the current number of {@link IByteBuffer}'s that are composed in this instance
        /// </summary>
        public int NumComponents => this.components.Count;

        /// <summary>
        ///     Return the max number of {@link IByteBuffer}'s that are composed in this instance
        /// </summary>
        public int MaxNumComponents => this.maxNumComponents;

        /// <summary>
        ///     Return the index for the given offset
        /// </summary>
        public int ToComponentIndex(int offset)
        {
            this.CheckIndex(offset);

            for (int low = 0, high = this.components.Count; low <= high;)
            {
                int mid = (low + high).RightUShift(1);
                ComponentEntry c = this.components[mid];
                if (offset >= c.EndOffset)
                {
                    low = mid + 1;
                }
                else if (offset < c.Offset)
                {
                    high = mid - 1;
                }
                else
                {
                    return mid;
                }
            }

            throw new Exception("should not reach here");
        }

        public int ToByteIndex(int cIndex)
        {
            this.CheckComponentIndex(cIndex);
            return this.components[cIndex].Offset;
        }

        public override byte GetByte(int index) => this._GetByte(index);

        protected override byte _GetByte(int index)
        {
            ComponentEntry c = this.FindComponent(index);
            return c.Buffer.GetByte(index - c.Offset);
        }

        protected override short _GetShort(int index)
        {
            ComponentEntry c = this.FindComponent(index);
            if (index + 2 <= c.EndOffset)
            {
                return c.Buffer.GetShort(index - c.Offset);
            }
            else if (this.Order == ByteOrder.BigEndian)
            {
                return (short)(this._GetByte(index) << 8 | this._GetByte(index + 1));
            }
            else
            {
                return (short)(this._GetByte(index) | this._GetByte(index + 1) << 8);
            }
        }

        protected override int _GetInt(int index)
        {
            ComponentEntry c = this.FindComponent(index);
            if (index + 4 <= c.EndOffset)
            {
                return c.Buffer.GetInt(index - c.Offset);
            }
            else if (this.Order == ByteOrder.BigEndian)
            {
                return this._GetShort(index) << 16 | (ushort)this._GetShort(index + 2);
            }
            else
            {
                return (ushort)this._GetShort(index) | this._GetShort(index + 2) << 16;
            }
        }

        protected override int _GetMedium(int index)
        {
            ComponentEntry c = this.FindComponent(index);
            if (index + 3 <= c.EndOffset)
            {
                return c.Buffer.GetMedium(index - c.Offset);
            }
            else if (this.Order == ByteOrder.BigEndian)
            {
                return this._GetShort(index) << 8 | this._GetByte(index + 2);
            }
            else
            {
                return (ushort)this._GetShort(index) | (sbyte)this._GetByte(index + 2) << 16;
            }
        }

        protected override long _GetLong(int index)
        {
            ComponentEntry c = this.FindComponent(index);
            if (index + 8 <= c.EndOffset)
            {
                return c.Buffer.GetLong(index - c.Offset);
            }
            else if (this.Order == ByteOrder.BigEndian)
            {
                return (long)this._GetInt(index) << 32 | (uint)this._GetInt(index + 4);
            }
            else
            {
                return (uint)this._GetInt(index) | (((long)this._GetInt(index + 4)) << 32);
            }
        }

        public override IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            this.CheckDstIndex(index, length, dstIndex, dst.Length);
            if (length == 0)
            {
                return this;
            }

            int i = this.ToComponentIndex(index);
            while (length > 0)
            {
                ComponentEntry c = this.components[i];
                IByteBuffer s = c.Buffer;
                int adjustment = c.Offset;
                int localLength = Math.Min(length, s.Capacity - (index - adjustment));
                s.GetBytes(index - adjustment, dst, dstIndex, localLength);
                index += localLength;
                dstIndex += localLength;
                length -= localLength;
                i++;
            }
            return this;
        }

        public override IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            this.CheckIndex(index, length);
            if (length == 0)
            {
                return this;
            }

            int i = this.ToComponentIndex(index);
            while (length > 0)
            {
                ComponentEntry c = this.components[i];
                IByteBuffer s = c.Buffer;
                int adjustment = c.Offset;
                int localLength = Math.Min(length, s.Capacity - (index - adjustment));
                s.GetBytes(index - adjustment, destination, localLength);
                index += localLength;
                length -= localLength;
                i++;
            }
            return this;
        }

        public override IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            this.CheckDstIndex(index, length, dstIndex, dst.Capacity);
            if (length == 0)
            {
                return this;
            }

            int i = this.ToComponentIndex(index);
            while (length > 0)
            {
                ComponentEntry c = this.components[i];
                IByteBuffer s = c.Buffer;
                int adjustment = c.Offset;
                int localLength = Math.Min(length, s.Capacity - (index - adjustment));
                s.GetBytes(index - adjustment, dst, dstIndex, localLength);
                index += localLength;
                dstIndex += localLength;
                length -= localLength;
                i++;
            }
            return this;
        }

        public override IByteBuffer SetByte(int index, int value)
        {
            ComponentEntry c = this.FindComponent(index);
            c.Buffer.SetByte(index - c.Offset, value);
            return this;
        }

        protected override void _SetByte(int index, int value) => this.SetByte(index, value);

        protected override void _SetShort(int index, int value)
        {
            ComponentEntry c = this.FindComponent(index);
            if (index + 2 <= c.EndOffset)
            {
                c.Buffer.SetShort(index - c.Offset, value);
            }
            else if (this.Order == ByteOrder.BigEndian)
            {
                this._SetByte(index, (byte)((uint)value >> 8));
                this._SetByte(index + 1, (byte)value);
            }
            else
            {
                this._SetByte(index, (byte)value);
                this._SetByte(index + 1, (byte)((uint)value >> 8));
            }
        }

        protected override void _SetMedium(int index, int value)
        {
            ComponentEntry c = this.FindComponent(index);
            if (index + 3 <= c.EndOffset)
            {
                c.Buffer.SetMedium(index - c.Offset, value);
            }
            else if (this.Order == ByteOrder.BigEndian)
            {
                this._SetShort(index, (short)(value >> 8));
                this._SetByte(index + 2, (byte)value);
            }
            else
            {
                this._SetShort(index, (short)value);
                this._SetByte(index + 2, (byte)(value.RightUShift(16)));
            }
        }

        protected override void _SetInt(int index, int value)
        {
            ComponentEntry c = this.FindComponent(index);
            if (index + 4 <= c.EndOffset)
            {
                c.Buffer.SetInt(index - c.Offset, value);
            }
            else if (this.Order == ByteOrder.BigEndian)
            {
                this._SetShort(index, (short)((uint)value >> 16));
                this._SetShort(index + 2, (short)value);
            }
            else
            {
                this._SetShort(index, (short)value);
                this._SetShort(index + 2, (short)((uint)value >> 16));
            }
        }

        protected override void _SetLong(int index, long value)
        {
            ComponentEntry c = this.FindComponent(index);
            if (index + 8 <= c.EndOffset)
            {
                c.Buffer.SetLong(index - c.Offset, value);
            }
            else if (this.Order == ByteOrder.BigEndian)
            {
                this._SetInt(index, (int)((ulong)value >> 32));
                this._SetInt(index + 4, (int)value);
            }
            else
            {
                this._SetInt(index, (int)value);
                this._SetInt(index + 4, (int)((ulong)value >> 32));
            }
        }

        public override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            this.CheckSrcIndex(index, length, srcIndex, src.Length);
            if (length == 0)
            {
                return this;
            }

            int i = this.ToComponentIndex(index);
            while (length > 0)
            {
                ComponentEntry c = this.components[i];
                IByteBuffer s = c.Buffer;
                int adjustment = c.Offset;
                int localLength = Math.Min(length, s.Capacity - (index - adjustment));
                s.SetBytes(index - adjustment, src, srcIndex, localLength);
                index += localLength;
                srcIndex += localLength;
                length -= localLength;
                i++;
            }
            return this;
        }

        public override async Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            this.CheckIndex(index, length);
            if (length == 0)
            {
                return 0;
                //return src.Read(EmptyArrays.EMPTY_BYTES);
            }

            int i = this.ToComponentIndex(index);
            int readBytes = 0;

            do
            {
                ComponentEntry c = this.components[i];
                IByteBuffer s = c.Buffer;
                int adjustment = c.Offset;
                int localLength = Math.Min(length, s.Capacity - (index - adjustment));
                int localReadBytes = await s.SetBytesAsync(index - adjustment, src, localLength, cancellationToken);
                if (localReadBytes < 0)
                {
                    if (readBytes == 0)
                    {
                        return -1;
                    }
                    else
                    {
                        break;
                    }
                }

                if (localReadBytes == localLength)
                {
                    index += localLength;
                    length -= localLength;
                    readBytes += localLength;
                    i++;
                }
                else
                {
                    index += localReadBytes;
                    length -= localReadBytes;
                    readBytes += localReadBytes;
                }
            }
            while (length > 0);

            return readBytes;
        }

        public override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            this.CheckSrcIndex(index, length, srcIndex, src.Capacity);
            if (length == 0)
            {
                return this;
            }

            int i = this.ToComponentIndex(index);
            while (length > 0)
            {
                ComponentEntry c = this.components[i];
                IByteBuffer s = c.Buffer;
                int adjustment = c.Offset;
                int localLength = Math.Min(length, s.Capacity - (index - adjustment));
                s.SetBytes(index - adjustment, src, srcIndex, localLength);
                index += localLength;
                srcIndex += localLength;
                length -= localLength;
                i++;
            }
            return this;
        }

        public override IByteBuffer SetZero(int index, int length)
        {
            this.CheckIndex(index, length);
            if (length == 0)
            {
                return this;
            }

            int i = this.ToComponentIndex(index);
            while (length > 0)
            {
                ComponentEntry c = this.components[i];
                IByteBuffer s = c.Buffer;
                int adjustment = c.Offset;
                int localLength = Math.Min(length, s.Capacity - (index - adjustment));
                s.SetZero(index - adjustment, localLength);
                index += localLength;
                length -= localLength;
                i++;
            }
            return this;
        }

        public override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            IByteBuffer dst = Unpooled.Buffer(length);
            if (length != 0)
            {
                this.CopyTo(index, length, this.ToComponentIndex(index), dst);
            }
            return dst;
        }

        void CopyTo(int index, int length, int componentId, IByteBuffer dst)
        {
            int dstIndex = 0;
            int i = componentId;

            while (length > 0)
            {
                ComponentEntry c = this.components[i];
                IByteBuffer s = c.Buffer;
                int adjustment = c.Offset;
                int localLength = Math.Min(length, s.Capacity - (index - adjustment));
                s.GetBytes(index - adjustment, dst, dstIndex, localLength);
                index += localLength;
                dstIndex += localLength;
                length -= localLength;
                i++;
            }

            dst.SetWriterIndex(dst.Capacity);
        }

        /// <summary>
        ///     Return the {@link IByteBuffer} on the specified index
        ///     @param cIndex the index for which the {@link IByteBuffer} should be returned
        ///     @return buffer the {@link IByteBuffer} on the specified index
        /// </summary>
        public IByteBuffer this[int cIndex] => this.InternalComponent(cIndex).Duplicate();

        /// <summary>
        ///     Return the {@link IByteBuffer} on the specified index
        ///     @param offset the offset for which the {@link IByteBuffer} should be returned
        ///     @return the {@link IByteBuffer} on the specified index
        /// </summary>
        public IByteBuffer ComponentAtOffset(int offset) => this.InternalComponentAtOffset(offset).Duplicate();

        /// <summary>
        ///     Return the internal {@link IByteBuffer} on the specified index. Note that updating the indexes of the returned
        ///     buffer will lead to an undefined behavior of this buffer.
        ///     @param cIndex the index for which the {@link IByteBuffer} should be returned
        /// </summary>
        public IByteBuffer InternalComponent(int cIndex)
        {
            this.CheckComponentIndex(cIndex);
            return this.components[cIndex].Buffer;
        }

        /// <summary>
        ///     Return the internal {@link IByteBuffer} on the specified offset. Note that updating the indexes of the returned
        ///     buffer will lead to an undefined behavior of this buffer.
        ///     @param offset the offset for which the {@link IByteBuffer} should be returned
        /// </summary>
        public IByteBuffer InternalComponentAtOffset(int offset) => this.FindComponent(offset).Buffer;

        ComponentEntry FindComponent(int offset)
        {
            this.CheckIndex(offset);

            for (int low = 0, high = this.components.Count; low <= high;)
            {
                int mid = (low + high).RightUShift(1);
                ComponentEntry c = this.components[mid];
                if (offset >= c.EndOffset)
                {
                    low = mid + 1;
                }
                else if (offset < c.Offset)
                {
                    high = mid - 1;
                }
                else
                {
                    Contract.Assert(c.Length != 0);
                    return c;
                }
            }

            throw new Exception("should not reach here");
        }

        /// <summary>
        ///     Consolidate the composed {@link IByteBuffer}s
        /// </summary>
        public CompositeByteBuffer Consolidate()
        {
            this.EnsureAccessible();
            int numComponents = this.NumComponents;
            if (numComponents <= 1)
            {
                return this;
            }

            ComponentEntry last = this.components[numComponents - 1];
            int capacity = last.EndOffset;
            IByteBuffer consolidated = this.AllocateBuffer(capacity);

            for (int i = 0; i < numComponents; i++)
            {
                ComponentEntry c = this.components[i];
                IByteBuffer b = c.Buffer;
                consolidated.WriteBytes(b);
                c.FreeIfNecessary();
            }

            this.components.Clear();
            this.components.Add(new ComponentEntry(consolidated));
            this.UpdateComponentOffsets(0);
            return this;
        }

        /// <summary>
        ///     Consolidate the composed {@link IByteBuffer}s
        ///     @param cIndex the index on which to start to compose
        ///     @param numComponents the number of components to compose
        /// </summary>
        public CompositeByteBuffer Consolidate(int cIndex, int numComponents)
        {
            this.CheckComponentIndex(cIndex, numComponents);
            if (numComponents <= 1)
            {
                return this;
            }

            int endCIndex = cIndex + numComponents;
            ComponentEntry last = this.components[endCIndex - 1];
            int capacity = last.EndOffset - this.components[cIndex].Offset;
            IByteBuffer consolidated = this.AllocateBuffer(capacity);

            for (int i = cIndex; i < endCIndex; i++)
            {
                ComponentEntry c = this.components[i];
                IByteBuffer b = c.Buffer;
                consolidated.WriteBytes(b);
                c.FreeIfNecessary();
            }

            this.components.RemoveRange(cIndex + 1, endCIndex - cIndex);
            this.components[cIndex] = new ComponentEntry(consolidated);
            this.UpdateComponentOffsets(cIndex);
            return this;
        }

        /// <summary>
        ///     Discard all {@link IByteBuffer}s which are read.
        /// </summary>
        public CompositeByteBuffer DiscardReadComponents()
        {
            this.EnsureAccessible();
            int readerIndex = this.ReaderIndex;
            if (readerIndex == 0)
            {
                return this;
            }

            // Discard everything if (readerIndex = writerIndex = capacity).
            int writerIndex = this.WriterIndex;
            if (readerIndex == writerIndex && writerIndex == this.Capacity)
            {
                foreach (ComponentEntry c in this.components)
                {
                    c.FreeIfNecessary();
                }
                this.components.Clear();
                this.SetIndex(0, 0);
                this.AdjustMarkers(readerIndex);
                return this;
            }

            // Remove read components.
            int firstComponentId = this.ToComponentIndex(readerIndex);
            for (int i = 0; i < firstComponentId; i++)
            {
                this.components[i].FreeIfNecessary();
            }
            this.components.RemoveRange(0, firstComponentId);

            // Update indexes and markers.
            ComponentEntry first = this.components[0];
            int offset = first.Offset;
            this.UpdateComponentOffsets(0);
            this.SetIndex(readerIndex - offset, writerIndex - offset);
            this.AdjustMarkers(offset);
            return this;
        }

        public override IByteBuffer DiscardReadBytes()
        {
            this.EnsureAccessible();
            int readerIndex = this.ReaderIndex;
            if (readerIndex == 0)
            {
                return this;
            }

            // Discard everything if (readerIndex = writerIndex = capacity).
            int writerIndex = this.WriterIndex;
            if (readerIndex == writerIndex && writerIndex == this.Capacity)
            {
                foreach (ComponentEntry c1 in this.components)
                {
                    c1.FreeIfNecessary();
                }
                this.components.Clear();
                this.SetIndex(0, 0);
                this.AdjustMarkers(readerIndex);
                return this;
            }

            // Remove read components.
            int firstComponentId = this.ToComponentIndex(readerIndex);
            for (int i = 0; i < firstComponentId; i++)
            {
                this.components[i].FreeIfNecessary();
            }
            this.components.RemoveRange(0, firstComponentId);

            // Remove or replace the first readable component with a new slice.
            ComponentEntry c = this.components[0];
            int adjustment = readerIndex - c.Offset;
            if (adjustment == c.Length)
            {
                // new slice would be empty, so remove instead
                this.components.RemoveAt(0);
            }
            else
            {
                var newC = new ComponentEntry(c.Buffer.Slice(adjustment, c.Length - adjustment));
                this.components[0] = newC;
            }

            // Update indexes and markers.
            this.UpdateComponentOffsets(0);
            this.SetIndex(0, writerIndex - readerIndex);
            this.AdjustMarkers(readerIndex);
            return this;
        }

        IByteBuffer AllocateBuffer(int capacity) => this.Allocator.Buffer(capacity);

        public override string ToString()
        {
            string result = base.ToString();
            result = result.Substring(0, result.Length - 1);
            return $"{result}, components={this.components.Count})";
        }

        public override IReferenceCounted Touch()
        {
            this.leak?.Record();
            return this;
        }

        public override IReferenceCounted Touch(object hint)
        {
            this.leak?.Record(hint);
            return this;
        }

        public override IByteBuffer DiscardSomeReadBytes() => this.DiscardReadComponents();

        protected override void Deallocate()
        {
            if (this.freed)
            {
                return;
            }

            this.freed = true;
            int size = this.components.Count;
            // We're not using foreach to avoid creating an iterator.
            // see https://github.com/netty/netty/issues/2642
            for (int i = 0; i < size; i++)
            {
                this.components[i].FreeIfNecessary();
            }

            this.leak?.Close();
        }

        public override IByteBuffer Unwrap() => null;
    }
}