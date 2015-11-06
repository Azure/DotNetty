// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Threading;
    using DotNetty.Common;

    public abstract class AbstractReferenceCountedByteBuffer : AbstractByteBuffer
    {
#pragma warning disable 420
        volatile int referenceCount = 1;

        protected AbstractReferenceCountedByteBuffer(int maxCapacity)
            : base(maxCapacity)
        {
        }

        public override int ReferenceCount
        {
            get { return this.referenceCount; }
        }

        protected void SetReferenceCount(int value)
        {
            this.referenceCount = value;
        }

        public override IReferenceCounted Retain()
        {
            while (true)
            {
                int refCnt = this.referenceCount;
                if (refCnt == 0)
                {
                    throw new IllegalReferenceCountException(0, 1);
                }
                if (refCnt == int.MaxValue)
                {
                    throw new IllegalReferenceCountException(int.MaxValue, 1);
                }

                if (Interlocked.CompareExchange(ref this.referenceCount, refCnt + 1, refCnt) == refCnt)
                {
                    break;
                }
            }
            return this;
        }

        public override IReferenceCounted Retain(int increment)
        {
            if (increment <= 0)
            {
                throw new ArgumentOutOfRangeException("increment: " + increment + " (expected: > 0)");
            }

            while (true)
            {
                int refCnt = this.referenceCount;
                if (refCnt == 0)
                {
                    throw new IllegalReferenceCountException(0, increment);
                }
                if (refCnt > int.MaxValue - increment)
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

        public override bool Release()
        {
            while (true)
            {
                int refCnt = this.referenceCount;
                if (refCnt == 0)
                {
                    throw new IllegalReferenceCountException(0, -1);
                }

                if (Interlocked.CompareExchange(ref this.referenceCount, refCnt - 1, refCnt) == refCnt)
                {
                    if (refCnt == 1)
                    {
                        this.Deallocate();
                        return true;
                    }
                    return false;
                }
            }
        }

        public override bool Release(int decrement)
        {
            if (decrement <= 0)
            {
                throw new ArgumentOutOfRangeException("decrement: " + decrement + " (expected: > 0)");
            }

            while (true)
            {
                int refCnt = this.referenceCount;
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

        public override IReferenceCounted Touch()
        {
            return this;
        }

        public override IReferenceCounted Touch(object hint)
        {
            return this;
        }

        protected abstract void Deallocate();
    }
}