// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Diagnostics;
    using DotNetty.Common;

    abstract class AbstractPooledDerivedByteBuffer : AbstractReferenceCountedByteBuffer
    {
        readonly ThreadLocalPool.Handle recyclerHandle;
        AbstractByteBuffer rootParent;
          
        // Deallocations of a pooled derived buffer should always propagate through the entire chain of derived buffers.
        // This is because each pooled derived buffer maintains its own reference count and we should respect each one.
        // If deallocations cause a release of the "root parent" then then we may prematurely release the underlying
        // content before all the derived buffers have been released.
        //
        IByteBuffer parent;

        protected AbstractPooledDerivedByteBuffer(ThreadLocalPool.Handle recyclerHandle) 
            : base(0)
        {
            this.recyclerHandle = recyclerHandle;
        }

        // Called from within SimpleLeakAwareByteBuf and AdvancedLeakAwareByteBuffer.
        internal void Parent(IByteBuffer newParent)
        {
            Debug.Assert(newParent is SimpleLeakAwareByteBuffer);
            this.parent = newParent;
        }

        public sealed override IByteBuffer Unwrap() => this.UnwrapCore();

        protected AbstractByteBuffer UnwrapCore() => this.rootParent;

        internal T Init<T>(
            AbstractByteBuffer unwrapped, IByteBuffer wrapped, int readerIndex, int writerIndex, int maxCapacity)
            where T : AbstractPooledDerivedByteBuffer
        {
            wrapped.Retain(); // Retain up front to ensure the parent is accessible before doing more work.
            this.parent = wrapped;
            this.rootParent = unwrapped;

            try
            {
                this.SetMaxCapacity(maxCapacity);
                this.SetIndex0(readerIndex, writerIndex); // It is assumed the bounds checking is done by the caller.
                this.SetReferenceCount(1);
                
                wrapped = null;
                return (T)this;
            }
            finally
            {
                if (wrapped != null)
                {
                    this.parent = this.rootParent = null;
                    wrapped.Release();
                }
            }
        }

        protected internal sealed override void Deallocate()
        {
            // We need to first store a reference to the parent before recycle this instance. This is needed as
            // otherwise it is possible that the same AbstractPooledDerivedByteBuf is again obtained and init(...) is
            // called before we actually have a chance to call release(). This leads to call release() on the wrong parent.
            IByteBuffer parentBuf = this.parent;
            this.recyclerHandle.Release(this);
            parentBuf.Release();
        }

        public sealed override IByteBufferAllocator Allocator => this.Unwrap().Allocator;

        public sealed override bool IsDirect => this.Unwrap().IsDirect;

        public override bool HasArray => this.Unwrap().HasArray;

        public override byte[] Array => this.Unwrap().Array;

        public override bool HasMemoryAddress => this.Unwrap().HasMemoryAddress;

        public sealed override int IoBufferCount => this.Unwrap().IoBufferCount;

        public sealed override IByteBuffer RetainedSlice()
        {
            int index = this.ReaderIndex;
            return base.RetainedSlice(index, this.WriterIndex - index);
        }

        public override IByteBuffer Slice(int index, int length)
        {
            // All reference count methods should be inherited from this object (this is the "parent").
            return new PooledNonRetainedSlicedByteBuffer(this, (AbstractByteBuffer)this.Unwrap(), index, length);
        }

        protected IByteBuffer Duplicate0()
        {
            // All reference count methods should be inherited from this object (this is the "parent").
            return new PooledNonRetainedDuplicateByteBuffer(this, (AbstractByteBuffer)this.Unwrap());
        }

        sealed class PooledNonRetainedDuplicateByteBuffer : UnpooledDuplicatedByteBuffer
        {
            readonly IReferenceCounted referenceCountDelegate;

            internal PooledNonRetainedDuplicateByteBuffer(IReferenceCounted referenceCountDelegate, AbstractByteBuffer buffer)
                : base(buffer)
            {
                this.referenceCountDelegate = referenceCountDelegate;
            }

            protected override int ReferenceCount0() => this.referenceCountDelegate.ReferenceCount;

            protected override IByteBuffer Retain0()
            {
                this.referenceCountDelegate.Retain();
                return this;
            }

            protected override IByteBuffer Retain0(int increment)
            {
                this.referenceCountDelegate.Retain(increment);
                return this;
            }

            protected override IByteBuffer Touch0()
            {
                this.referenceCountDelegate.Touch();
                return this;
            }

            protected override IByteBuffer Touch0(object hint)
            {
                this.referenceCountDelegate.Touch(hint);
                return this;
            }

            protected override bool Release0() => this.referenceCountDelegate.Release();

            protected override bool Release0(int decrement) => this.referenceCountDelegate.Release(decrement);

            public override IByteBuffer Duplicate() => new PooledNonRetainedDuplicateByteBuffer(this.referenceCountDelegate, this);

            public override IByteBuffer RetainedDuplicate() => PooledDuplicatedByteBuffer.NewInstance(this.UnwrapCore(), this, this.ReaderIndex, this.WriterIndex);

            public override IByteBuffer Slice(int index, int length)
            {
                this.CheckIndex0(index, length);
                return new PooledNonRetainedSlicedByteBuffer(this.referenceCountDelegate, (AbstractByteBuffer)this.Unwrap(), index, length);
            }

            // Capacity is not allowed to change for a sliced ByteBuf, so length == capacity()
            public override IByteBuffer RetainedSlice() => this.RetainedSlice(this.ReaderIndex, this.Capacity);

            public override IByteBuffer RetainedSlice(int index, int length) => PooledSlicedByteBuffer.NewInstance(this.UnwrapCore(), this, index, length);
        }

        sealed class PooledNonRetainedSlicedByteBuffer : UnpooledSlicedByteBuffer
        {
            readonly IReferenceCounted referenceCountDelegate;

            public PooledNonRetainedSlicedByteBuffer(IReferenceCounted referenceCountDelegate, AbstractByteBuffer buffer, int index, int length)
                : base(buffer, index, length)
            {
                this.referenceCountDelegate = referenceCountDelegate;
            }

            protected override int ReferenceCount0() => this.referenceCountDelegate.ReferenceCount;

            protected override IByteBuffer Retain0()
            {
                this.referenceCountDelegate.Retain();
                return this;
            }

            protected override IByteBuffer Retain0(int increment)
            {
                this.referenceCountDelegate.Retain(increment);
                return this;
            }

            protected override IByteBuffer Touch0()
            {
                this.referenceCountDelegate.Touch();
                return this;
            }

            protected override IByteBuffer Touch0(object hint)
            {
                this.referenceCountDelegate.Touch(hint);
                return this;
            }

            protected override bool Release0() => this.referenceCountDelegate.Release();

            protected override bool Release0(int decrement) => this.referenceCountDelegate.Release(decrement);

            public override IByteBuffer Duplicate() => 
                new PooledNonRetainedDuplicateByteBuffer(this.referenceCountDelegate, this.UnwrapCore())
                    .SetIndex(this.Idx(this.ReaderIndex), this.Idx(this.WriterIndex));

            public override IByteBuffer RetainedDuplicate() => PooledDuplicatedByteBuffer.NewInstance(this.UnwrapCore(), this, this.Idx(this.ReaderIndex), this.Idx(this.WriterIndex));
            
            public override IByteBuffer Slice(int index, int length)
            {
                this.CheckIndex0(index, length);
                return new PooledNonRetainedSlicedByteBuffer(this.referenceCountDelegate, this.UnwrapCore(), this.Idx(index), length);
            }

            public override IByteBuffer RetainedSlice(int index, int length) => PooledSlicedByteBuffer.NewInstance(this.UnwrapCore(), this, this.Idx(index), length);
        }
    }
}
