// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using DotNetty.Common;

    /// <summary>
    ///     Abstract base class for <see cref="IByteBuffer" /> implementations that wrap another
    ///     <see cref="IByteBuffer" />.
    /// </summary>
    public abstract class AbstractDerivedByteBuffer : AbstractByteBuffer
    {
        protected AbstractDerivedByteBuffer(int maxCapacity)
            : base(maxCapacity)
        {
        }

        public sealed override int ReferenceCount
        {
            get { return this.Unwrap().ReferenceCount; }
        }

        public sealed override IReferenceCounted Retain()
        {
            this.Unwrap().Retain();
            return this;
        }

        public sealed override IReferenceCounted Retain(int increment)
        {
            this.Unwrap().Retain(increment);
            return this;
        }

        public sealed override IReferenceCounted Touch()
        {
            this.Unwrap().Touch();
            return this;
        }

        public sealed override IReferenceCounted Touch(object hint)
        {
            this.Unwrap().Touch(hint);
            return this;
        }

        public sealed override bool Release()
        {
            return this.Unwrap().Release();
        }

        public sealed override bool Release(int decrement)
        {
            return this.Unwrap().Release(decrement);
        }
    }
}