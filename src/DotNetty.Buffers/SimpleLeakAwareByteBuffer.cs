// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using DotNetty.Common;

    class SimpleLeakAwareByteBuffer : WrappedByteBuffer
    {
        protected readonly IResourceLeakTracker Leak;
        readonly IByteBuffer trackedByteBuf;

        internal SimpleLeakAwareByteBuffer(IByteBuffer wrapped, IByteBuffer trackedByteBuf, IResourceLeakTracker leak)
            : base(wrapped)
        {
            Contract.Requires(trackedByteBuf != null);
            Contract.Requires(leak != null);

            this.trackedByteBuf = trackedByteBuf;
            this.Leak = leak;
        }

        internal SimpleLeakAwareByteBuffer(IByteBuffer wrapped, IResourceLeakTracker leak)
            : this(wrapped, wrapped, leak)
        {
        }

        public override IByteBuffer Slice() => this.NewSharedLeakAwareByteBuffer(base.Slice());

        public override IByteBuffer RetainedSlice() => this.UnwrappedDerived(base.RetainedSlice());

        public override IByteBuffer RetainedSlice(int index, int length) => this.UnwrappedDerived(base.RetainedSlice(index, length));

        public override IByteBuffer RetainedDuplicate() => this.UnwrappedDerived(base.RetainedDuplicate());

        public override IByteBuffer ReadRetainedSlice(int length) => this.UnwrappedDerived(base.ReadRetainedSlice(length));
        
        public override IByteBuffer Slice(int index, int length) => this.NewSharedLeakAwareByteBuffer(base.Slice(index, length));

        public override IByteBuffer Duplicate() => this.NewSharedLeakAwareByteBuffer(base.Duplicate());

        public override IByteBuffer ReadSlice(int length) => this.NewSharedLeakAwareByteBuffer(base.ReadSlice(length));

        public override IReferenceCounted Touch() => this;

        public override IReferenceCounted Touch(object hint) => this;

        public override bool Release()
        {
            if (base.Release())
            {
                this.CloseLeak();
                return true;
            }

            return false;
        }

        public override bool Release(int decrement)
        {
            if (base.Release(decrement))
            {
                this.CloseLeak();
                return true;
            }
            return false;
        }

        void CloseLeak()
        {
            // Close the ResourceLeakTracker with the tracked ByteBuf as argument. This must be the same that was used when
            // calling DefaultResourceLeak.track(...).
            bool closed = this.Leak.Close(this.trackedByteBuf);
            Debug.Assert(closed);
        }

        IByteBuffer UnwrappedDerived(IByteBuffer derived)
        {
            if (derived is AbstractPooledDerivedByteBuffer buffer) {
                // Update the parent to point to this buffer so we correctly close the ResourceLeakTracker.
                buffer.Parent(this);

                IResourceLeakTracker newLeak = AbstractByteBuffer.LeakDetector.Track(buffer);
                if (newLeak == null)
                {
                    // No leak detection, just return the derived buffer.
                    return derived;
                }

                return this.NewLeakAwareByteBuffer(buffer, newLeak);
            }

            return this.NewSharedLeakAwareByteBuffer(derived);
        }

        SimpleLeakAwareByteBuffer NewSharedLeakAwareByteBuffer(IByteBuffer wrapped) => this.NewLeakAwareByteBuffer(wrapped, this.trackedByteBuf, this.Leak);
        
        SimpleLeakAwareByteBuffer NewLeakAwareByteBuffer(IByteBuffer wrapped, IResourceLeakTracker leakTracker) => this.NewLeakAwareByteBuffer(wrapped, wrapped, leakTracker);

        protected virtual SimpleLeakAwareByteBuffer NewLeakAwareByteBuffer(IByteBuffer buf, IByteBuffer trackedBuf, IResourceLeakTracker leakTracker) =>
            new SimpleLeakAwareByteBuffer(buf, trackedBuf, leakTracker);
    }
}