// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using DotNetty.Common;

    class SimpleLeakAwareCompositeByteBuffer : WrappedCompositeByteBuffer
    {
        protected readonly IResourceLeakTracker Leak;

        internal SimpleLeakAwareCompositeByteBuffer(CompositeByteBuffer wrapped, IResourceLeakTracker leak) : base(wrapped)
        {
            Contract.Requires(leak != null);
            this.Leak = leak;
        }

        public override bool Release()
        {
            // Call unwrap() before just in case that super.release() will change the ByteBuf instance that is returned
            // by unwrap().
            IByteBuffer unwrapped = this.Unwrap();
            if (base.Release())
            {
                this.CloseLeak(unwrapped);
                return true;
            }

            return false;
        }

        public override bool Release(int decrement)
        {
            // Call unwrap() before just in case that super.release() will change the ByteBuf instance that is returned
            // by unwrap().
            IByteBuffer unwrapped = this.Unwrap();
            if (base.Release(decrement))
            {
                this.CloseLeak(unwrapped);
                return true;
            }

            return false;
        }

        void CloseLeak(IByteBuffer trackedByteBuf)
        {
            // Close the ResourceLeakTracker with the tracked ByteBuf as argument. This must be the same that was used when
            // calling DefaultResourceLeak.track(...).
            bool closed = this.Leak.Close(trackedByteBuf);
            Debug.Assert(closed);
        }

        public override IByteBuffer Slice() => this.NewLeakAwareByteBuffer(base.Slice());

        public override IByteBuffer Slice(int index, int length) => this.NewLeakAwareByteBuffer(base.Slice(index, length));

        public override IByteBuffer Duplicate() => this.NewLeakAwareByteBuffer(base.Duplicate());

        public override IByteBuffer ReadSlice(int length) => this.NewLeakAwareByteBuffer(base.ReadSlice(length));

        public override IByteBuffer RetainedSlice() => this.NewLeakAwareByteBuffer(base.RetainedSlice());

        public override IByteBuffer RetainedSlice(int index, int length) => this.NewLeakAwareByteBuffer(base.RetainedSlice(index, length));

        public override IByteBuffer RetainedDuplicate() => this.NewLeakAwareByteBuffer(base.RetainedDuplicate());

        public override IByteBuffer ReadRetainedSlice(int length) => this.NewLeakAwareByteBuffer(base.ReadRetainedSlice(length));

        SimpleLeakAwareByteBuffer NewLeakAwareByteBuffer(IByteBuffer wrapped) => this.NewLeakAwareByteBuffer(wrapped, this.Unwrap(), this.Leak);

        protected virtual SimpleLeakAwareByteBuffer NewLeakAwareByteBuffer(IByteBuffer wrapped, IByteBuffer trackedByteBuf, IResourceLeakTracker leakTracker) =>
            new SimpleLeakAwareByteBuffer(wrapped, trackedByteBuf, leakTracker);
    }
}
