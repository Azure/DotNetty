// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using DotNetty.Common;

    sealed class SimpleLeakAwareByteBuffer : WrappedByteBuffer
    {
        readonly IResourceLeak leak;

        internal SimpleLeakAwareByteBuffer(IByteBuffer buf, IResourceLeak leak)
            : base(buf)
        {
            this.leak = leak;
        }

        public override IReferenceCounted Touch()
        {
            return this;
        }

        public override IReferenceCounted Touch(object hint)
        {
            return this;
        }

        public override bool Release()
        {
            bool deallocated = base.Release();
            if (deallocated)
            {
                this.leak.Close();
            }
            return deallocated;
        }

        public override bool Release(int decrement)
        {
            bool deallocated = base.Release(decrement);
            if (deallocated)
            {
                this.leak.Close();
            }
            return deallocated;
        }

        public override IByteBuffer WithOrder(ByteOrder endianness)
        {
            this.leak.Record();
            if (this.Order == endianness)
            {
                return this;
            }
            else
            {
                return new SimpleLeakAwareByteBuffer(base.WithOrder(endianness), this.leak);
            }
        }

        public override IByteBuffer Slice()
        {
            return new SimpleLeakAwareByteBuffer(base.Slice(), this.leak);
        }

        public override IByteBuffer Slice(int index, int length)
        {
            return new SimpleLeakAwareByteBuffer(base.Slice(index, length), this.leak);
        }

        public override IByteBuffer Duplicate()
        {
            return new SimpleLeakAwareByteBuffer(base.Duplicate(), this.leak);
        }

        public override IByteBuffer ReadSlice(int length)
        {
            return new SimpleLeakAwareByteBuffer(base.ReadSlice(length), this.leak);
        }
    }
}