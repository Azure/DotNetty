
namespace DotNetty.Buffers
{
    using DotNetty.Common;

    sealed class UnreleasableByteBuffer : WrappedByteBuffer
    {
        SwappedByteBuffer swappedBuffer;

        internal UnreleasableByteBuffer(IByteBuffer buf)
            : base(buf)
        {}

        public override IByteBuffer WithOrder(ByteOrder endianness)
        {
            if (this.Order == endianness)
            {
                return this;

            }

            SwappedByteBuffer buffer = this.swappedBuffer;
            if (buffer != null)
            {
                return this.swappedBuffer;
            }

            buffer = new SwappedByteBuffer(this);
            this.swappedBuffer = buffer;

            return this.swappedBuffer;
        }

        public override IByteBuffer ReadSlice(int length) => new UnreleasableByteBuffer(this.Buf.ReadSlice(length));

        public override IByteBuffer Slice() => new UnreleasableByteBuffer(this.Buf.Slice());

        public override IByteBuffer Slice(int index, int length) => new UnreleasableByteBuffer(this.Buf.Slice(index, length));

        public override IByteBuffer Duplicate() => new UnreleasableByteBuffer(this.Buf.Duplicate());

        public override IReferenceCounted Retain() => this;

        public override IReferenceCounted Retain(int increment) => this;

        public override IReferenceCounted Touch() => this;

        public override IReferenceCounted Touch(object hint) => this;

        public override bool Release() => false;

        public override bool Release(int decrement) => false;
    }
}
