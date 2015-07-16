// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using DotNetty.Common;

    public abstract class AbstractDerivedByteBuffer : AbstractByteBuffer
    {
        protected AbstractDerivedByteBuffer(int maxCapacity)
            : base(maxCapacity)
        {
        }

        public override int ReferenceCount
        {
            get { return this.Unwrap().ReferenceCount; }
        }

        public override IReferenceCounted Retain()
        {
            this.Unwrap().Retain();
            return this;
        }

        public override IReferenceCounted Retain(int increment)
        {
            this.Unwrap().Retain(increment);
            return this;
        }

        public override bool Release()
        {
            return this.Unwrap().Release();
        }

        public override bool Release(int decrement)
        {
            return this.Unwrap().Release(decrement);
        }
    }
}