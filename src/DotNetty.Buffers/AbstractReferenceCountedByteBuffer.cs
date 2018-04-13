// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable 420

namespace DotNetty.Buffers
{
    using System.Diagnostics.Contracts;
    using System.Threading;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    public abstract class AbstractReferenceCountedByteBuffer : AbstractByteBuffer
    {
        volatile int referenceCount = 1;

        protected AbstractReferenceCountedByteBuffer(int maxCapacity)
            : base(maxCapacity)
        {
        }

        public override int ReferenceCount => this.referenceCount;

        //An unsafe operation intended for use by a subclass that sets the reference count of the buffer directly
        protected internal void SetReferenceCount(int value) => this.referenceCount = value;

        public override IReferenceCounted Retain() => this.Retain0(1);

        public override IReferenceCounted Retain(int increment)
        {
            Contract.Requires(increment > 0);

            return this.Retain0(increment);
        }

        IReferenceCounted Retain0(int increment)
        {
            while (true)
            {
                int refCnt = this.referenceCount;
                int nextCnt = refCnt + increment;

                // Ensure we not resurrect (which means the refCnt was 0) and also that we encountered an overflow.
                if (nextCnt <= increment)
                {
                    throw new IllegalReferenceCountException(refCnt, increment);
                }
                if (Interlocked.CompareExchange(ref this.referenceCount, refCnt + increment, refCnt) == refCnt)
                {
                    break;
                }
            }

            return this;
        }

        public override IReferenceCounted Touch() => this;

        public override IReferenceCounted Touch(object hint) => this;

        public override bool Release() => this.Release0(1);

        public override bool Release(int decrement)
        {
            Contract.Requires(decrement > 0);

            return this.Release0(decrement);
        }

        bool Release0(int decrement)
        {
            while (true)
            {
                int refCnt = this.ReferenceCount;
                if (refCnt < decrement)
                {
                    throw new IllegalReferenceCountException(refCnt, -decrement);
                }

                if (Interlocked.CompareExchange(ref this.referenceCount, refCnt - decrement, refCnt) == refCnt)
                {
                    if (refCnt == decrement)
                    {
                        this.Deallocate();
                        return true;
                    }

                    return false;
                }
            }
        }

        protected internal abstract void Deallocate();
    }
}