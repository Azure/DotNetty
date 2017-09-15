// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    class UnpooledSlicedByteBuffer : AbstractUnpooledSlicedByteBuffer
    {
        internal UnpooledSlicedByteBuffer(AbstractByteBuffer buffer, int index, int length)
            : base(buffer, index, length)
        {
        }

        public override int Capacity => this.MaxCapacity;

        protected AbstractByteBuffer UnwrapCore() => (AbstractByteBuffer)this.Unwrap();

        protected internal override byte _GetByte(int index) => this.UnwrapCore()._GetByte(this.Idx(index));

        protected internal override short _GetShort(int index) => this.UnwrapCore()._GetShort(this.Idx(index));

        protected internal override short _GetShortLE(int index) => this.UnwrapCore()._GetShortLE(this.Idx(index));

        protected internal override int _GetUnsignedMedium(int index) => this.UnwrapCore()._GetUnsignedMedium(this.Idx(index));

        protected internal override int _GetUnsignedMediumLE(int index) => this.UnwrapCore()._GetUnsignedMediumLE(this.Idx(index));

        protected internal override int _GetInt(int index) => this.UnwrapCore()._GetInt(this.Idx(index));

        protected internal override int _GetIntLE(int index) => this.UnwrapCore()._GetIntLE(this.Idx(index));

        protected internal override long _GetLong(int index) => this.UnwrapCore()._GetLong(this.Idx(index));

        protected internal override long _GetLongLE(int index) => this.UnwrapCore()._GetLongLE(this.Idx(index));

        protected internal override void _SetByte(int index, int value) => this.UnwrapCore()._SetByte(this.Idx(index), value);

        protected internal override void _SetShort(int index, int value) => this.UnwrapCore()._SetShort(this.Idx(index), value);

        protected internal override void _SetShortLE(int index, int value) => this.UnwrapCore()._SetShortLE(this.Idx(index), value);

        protected internal override void _SetMedium(int index, int value) => this.UnwrapCore()._SetMedium(this.Idx(index), value);

        protected internal override void _SetMediumLE(int index, int value) => this.UnwrapCore()._SetMediumLE(this.Idx(index), value);

        protected internal override void _SetInt(int index, int value) => this.UnwrapCore()._SetInt(this.Idx(index), value);

        protected internal override void _SetIntLE(int index, int value) => this.UnwrapCore()._SetIntLE(this.Idx(index), value);

        protected internal override void _SetLong(int index, long value) => this.UnwrapCore()._SetLong(this.Idx(index), value);

        protected internal override void _SetLongLE(int index, long value) => this.UnwrapCore()._SetLongLE(this.Idx(index), value);
    }
}
