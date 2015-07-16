// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using DotNetty.Common;

    /// <summary>
    /// Represents an empty byte buffer
    /// </summary>
    public sealed class EmptyByteBuffer : AbstractByteBuffer
    {
        readonly IByteBufferAllocator allocator;

        public EmptyByteBuffer(IByteBufferAllocator allocator)
            : base(0)
        {
            this.allocator = allocator;
        }

        public override int Capacity
        {
            get { return 0; }
        }

        public override IByteBuffer AdjustCapacity(int newCapacity)
        {
            throw new NotSupportedException();
        }

        public override IByteBufferAllocator Allocator
        {
            get { return this.allocator; }
        }

        protected override byte _GetByte(int index)
        {
            throw new IndexOutOfRangeException();
        }

        protected override short _GetShort(int index)
        {
            throw new IndexOutOfRangeException();
        }

        protected override int _GetInt(int index)
        {
            throw new IndexOutOfRangeException();
        }

        protected override long _GetLong(int index)
        {
            throw new IndexOutOfRangeException();
        }

        public override IByteBuffer GetBytes(int index, IByteBuffer destination, int dstIndex, int length)
        {
            throw new IndexOutOfRangeException();
        }

        public override IByteBuffer GetBytes(int index, byte[] destination, int dstIndex, int length)
        {
            throw new IndexOutOfRangeException();
        }

        protected override void _SetByte(int index, int value)
        {
            throw new IndexOutOfRangeException();
        }

        protected override void _SetShort(int index, int value)
        {
            throw new IndexOutOfRangeException();
        }

        protected override void _SetInt(int index, int value)
        {
            throw new IndexOutOfRangeException();
        }

        protected override void _SetLong(int index, long value)
        {
            throw new IndexOutOfRangeException();
        }

        public override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            throw new IndexOutOfRangeException();
        }

        public override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            throw new IndexOutOfRangeException();
        }

        public override bool HasArray
        {
            get { return false; }
        }

        public override byte[] Array
        {
            get { throw new NotSupportedException(); }
        }

        public override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            return this;
        }

        public override int ArrayOffset
        {
            get { throw new NotSupportedException(); }
        }

        public override IByteBuffer Unwrap()
        {
            return null;
        }

        public override int ReferenceCount
        {
            get { return 1; }
        }

        public override IReferenceCounted Retain()
        {
            return this;
        }

        public override IReferenceCounted Retain(int increment)
        {
            return this;
        }

        public override bool Release()
        {
            return false;
        }

        public override bool Release(int decrement)
        {
            return false;
        }
    }
}