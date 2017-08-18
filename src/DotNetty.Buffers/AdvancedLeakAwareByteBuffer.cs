// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;

    class AdvancedLeakAwareByteBuffer : SimpleLeakAwareByteBuffer
    {
        static readonly string PropAcquireAndReleaseOnly = "io.netty.leakDetection.acquireAndReleaseOnly";
        static readonly bool AcquireAndReleaseOnly;

        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<AdvancedLeakAwareByteBuffer>();

        static AdvancedLeakAwareByteBuffer()
        {
            AcquireAndReleaseOnly = SystemPropertyUtil.GetBoolean(PropAcquireAndReleaseOnly, false);

            if (Logger.DebugEnabled)
            {
                Logger.Debug("-D{}: {}", PropAcquireAndReleaseOnly, AcquireAndReleaseOnly);
            }
        }

        internal AdvancedLeakAwareByteBuffer(IByteBuffer buf, IResourceLeak leak)
            : base(buf, leak)
        {
        }

        void RecordLeakNonRefCountingOperation()
        {
            if (!AcquireAndReleaseOnly)
            {
                this.leak.Record();
            }
        }

        public override IByteBuffer WithOrder(ByteOrder endianness)
        {
            this.RecordLeakNonRefCountingOperation();
            if (this.Order == endianness)
            {
                return this;
            }
            else
            {
                return new AdvancedLeakAwareByteBuffer(base.WithOrder(endianness), this.leak);
            }
        }

        public override IByteBuffer Slice()
        {
            this.RecordLeakNonRefCountingOperation();
            return new AdvancedLeakAwareByteBuffer(base.Slice(), this.leak);
        }

        public override IByteBuffer Slice(int index, int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return new AdvancedLeakAwareByteBuffer(base.Slice(index, length), this.leak);
        }

        public override IByteBuffer Duplicate()
        {
            this.RecordLeakNonRefCountingOperation();
            return new AdvancedLeakAwareByteBuffer(base.Duplicate(), this.leak);
        }

        public override IByteBuffer ReadSlice(int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return new AdvancedLeakAwareByteBuffer(base.ReadSlice(length), this.leak);
        }

        public override IByteBuffer DiscardReadBytes()
        {
            this.RecordLeakNonRefCountingOperation();
            return base.DiscardReadBytes();
        }

        public override IByteBuffer DiscardSomeReadBytes()
        {
            this.RecordLeakNonRefCountingOperation();
            return base.DiscardSomeReadBytes();
        }

        public override IByteBuffer EnsureWritable(int minWritableBytes)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.EnsureWritable(minWritableBytes);
        }

        public override int EnsureWritable(int minWritableBytes, bool force)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.EnsureWritable(minWritableBytes, force);
        }

        public override bool GetBoolean(int index)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.GetBoolean(index);
        }

        public override byte GetByte(int index)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.GetByte(index);
        }

        public override int GetMedium(int index)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.GetMedium(index);
        }

        public override int GetUnsignedMedium(int index)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.GetUnsignedMedium(index);
        }

        public override short GetShort(int index)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.GetShort(index);
        }

        public override ushort GetUnsignedShort(int index)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.GetUnsignedShort(index);
        }

        public override int GetInt(int index)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.GetInt(index);
        }

        public override uint GetUnsignedInt(int index)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.GetUnsignedInt(index);
        }

        public override long GetLong(int index)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.GetLong(index);
        }

        public override char GetChar(int index)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.GetChar(index);
        }

        public override float GetFloat(int index)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.GetFloat(index);
        }

        public override double GetDouble(int index)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.GetDouble(index);
        }

        public override IByteBuffer GetBytes(int index, IByteBuffer dst)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.GetBytes(index, dst);
        }

        public override IByteBuffer GetBytes(int index, IByteBuffer dst, int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.GetBytes(index, dst, length);
        }

        public override IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.GetBytes(index, dst, dstIndex, length);
        }

        public override IByteBuffer GetBytes(int index, byte[] dst)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.GetBytes(index, dst);
        }

        public override IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.GetBytes(index, dst, dstIndex, length);
        }

        public override IByteBuffer GetBytes(int index, Stream output, int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.GetBytes(index, output, length);
        }

        public override IByteBuffer SetBoolean(int index, bool value)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.SetBoolean(index, value);
        }

        public override IByteBuffer SetByte(int index, int value)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.SetByte(index, value);
        }

        public override IByteBuffer SetMedium(int index, int value)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.SetMedium(index, value);
        }

        public override IByteBuffer SetShort(int index, int value)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.SetShort(index, value);
        }

        public override IByteBuffer SetInt(int index, int value)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.SetInt(index, value);
        }

        public override IByteBuffer SetLong(int index, long value)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.SetLong(index, value);
        }

        public override IByteBuffer SetChar(int index, char value)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.SetChar(index, value);
        }

        public override IByteBuffer SetFloat(int index, float value)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.SetFloat(index, value);
        }

        public override IByteBuffer SetDouble(int index, double value)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.SetDouble(index, value);
        }

        public override IByteBuffer SetBytes(int index, IByteBuffer src)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.SetBytes(index, src);
        }

        public override IByteBuffer SetBytes(int index, IByteBuffer src, int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.SetBytes(index, src, length);
        }

        public override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.SetBytes(index, src, srcIndex, length);
        }

        public override IByteBuffer SetBytes(int index, byte[] src)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.SetBytes(index, src);
        }

        public override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.SetBytes(index, src, srcIndex, length);
        }

        public override Task<int> SetBytesAsync(int index, Stream input, int length, CancellationToken cancellationToken)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.SetBytesAsync(index, input, length, cancellationToken);
        }

        public override IByteBuffer SetZero(int index, int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.SetZero(index, length);
        }

        public override bool ReadBoolean()
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ReadBoolean();
        }

        public override byte ReadByte()
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ReadByte();
        }

        public override short ReadShort()
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ReadShort();
        }

        public override ushort ReadUnsignedShort()
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ReadUnsignedShort();
        }

        public override int ReadMedium()
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ReadMedium();
        }

        public override int ReadUnsignedMedium()
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ReadUnsignedMedium();
        }

        public override int ReadInt()
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ReadInt();
        }

        public override uint ReadUnsignedInt()
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ReadUnsignedInt();
        }

        public override long ReadLong()
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ReadLong();
        }

        public override char ReadChar()
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ReadChar();
        }

        public override float ReadFloat()
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ReadFloat();
        }

        public override double ReadDouble()
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ReadDouble();
        }

        public override IByteBuffer ReadBytes(int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ReadBytes(length);
        }

        public override IByteBuffer ReadBytes(IByteBuffer dst)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ReadBytes(dst);
        }

        public override IByteBuffer ReadBytes(IByteBuffer dst, int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ReadBytes(dst, length);
        }

        public override IByteBuffer ReadBytes(IByteBuffer dst, int dstIndex, int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ReadBytes(dst, dstIndex, length);
        }

        public override IByteBuffer ReadBytes(byte[] dst)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ReadBytes(dst);
        }

        public override IByteBuffer ReadBytes(byte[] dst, int dstIndex, int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ReadBytes(dst, dstIndex, length);
        }

        public override IByteBuffer ReadBytes(Stream output, int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ReadBytes(output, length);
        }

        public override IByteBuffer SkipBytes(int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.SkipBytes(length);
        }

        public override IByteBuffer WriteBoolean(bool value)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.WriteBoolean(value);
        }

        public override IByteBuffer WriteByte(int value)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.WriteByte(value);
        }

        public override IByteBuffer WriteShort(int value)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.WriteShort(value);
        }

        public override IByteBuffer WriteInt(int value)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.WriteInt(value);
        }

        public override IByteBuffer WriteMedium(int value)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.WriteMedium(value);
        }

        public override IByteBuffer WriteLong(long value)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.WriteLong(value);
        }

        public override IByteBuffer WriteChar(char value)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.WriteChar(value);
        }

        public override IByteBuffer WriteFloat(float value)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.WriteFloat(value);
        }

        public override IByteBuffer WriteDouble(double value)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.WriteDouble(value);
        }

        public override IByteBuffer WriteBytes(IByteBuffer src)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.WriteBytes(src);
        }

        public override IByteBuffer WriteBytes(IByteBuffer src, int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.WriteBytes(src, length);
        }

        public override IByteBuffer WriteBytes(IByteBuffer src, int srcIndex, int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.WriteBytes(src, srcIndex, length);
        }

        public override IByteBuffer WriteBytes(byte[] src)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.WriteBytes(src);
        }

        public override IByteBuffer WriteBytes(byte[] src, int srcIndex, int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.WriteBytes(src, srcIndex, length);
        }

        public override Task WriteBytesAsync(Stream input, int length, CancellationToken cancellationToken)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.WriteBytesAsync(input, length, cancellationToken);
        }

        public override IByteBuffer WriteZero(int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.WriteZero(length);
        }

        //public override int indexOf(int fromIndex, int toIndex, byte value)
        //{
        //    this.RecordLeakNonRefCountingOperation();
        //    return base.indexOf(fromIndex, toIndex, value);
        //}

        //public override int bytesBefore(byte value)
        //{
        //    this.RecordLeakNonRefCountingOperation();
        //    return base.bytesBefore(value);
        //}

        //public override int bytesBefore(int length, byte value)
        //{
        //    this.RecordLeakNonRefCountingOperation();
        //    return base.bytesBefore(length, value);
        //}

        //public override int bytesBefore(int index, int length, byte value)
        //{
        //    this.RecordLeakNonRefCountingOperation();
        //    return base.bytesBefore(index, length, value);
        //}

        //public override int forEachByte(ByteProcessor processor)
        //{
        //    this.RecordLeakNonRefCountingOperation();
        //    return base.forEachByte(processor);
        //}

        //public override int forEachByte(int index, int length, ByteProcessor processor)
        //{
        //    this.RecordLeakNonRefCountingOperation();
        //    return base.forEachByte(index, length, processor);
        //}

        //public override int forEachByteDesc(ByteProcessor processor)
        //{
        //    this.RecordLeakNonRefCountingOperation();
        //    this.RecordLeakNonRefCountingOperation();
        //    return base.forEachByteDesc(processor);
        //}

        //public override int forEachByteDesc(int index, int length, ByteProcessor processor)
        //{
        //    this.RecordLeakNonRefCountingOperation();
        //    return base.forEachByteDesc(index, length, processor);
        //}

        public override IByteBuffer Copy()
        {
            this.RecordLeakNonRefCountingOperation();
            return base.Copy();
        }

        public override IByteBuffer Copy(int index, int length)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.Copy(index, length);
        }

        // todo: port: complete
        //public override string toString(Charset charset)
        //{
        //    this.RecordLeakNonRefCountingOperation();
        //    return base.ToString(charset);
        //}

        //public override string toString(int index, int length, Charset charset)
        //{
        //    this.RecordLeakNonRefCountingOperation();
        //    return base.ToString(index, length, charset);
        //}

        public override IByteBuffer AdjustCapacity(int newCapacity)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.AdjustCapacity(newCapacity);
        }

        public override IReferenceCounted Retain()
        {
            this.leak.Record();
            return base.Retain();
        }

        public override IReferenceCounted Retain(int increment)
        {
            this.leak.Record();
            return base.Retain(increment);
        }

        public override IReferenceCounted Touch()
        {
            this.leak.Record();
            return this;
        }

        public override IReferenceCounted Touch(object hint)
        {
            this.leak.Record(hint);
            return this;
        }

        public override bool Release()
        {
            this.leak.Record();
            return base.Release();
        }

        public override bool Release(int decrement)
        {
            this.leak.Record();
            return base.Release(decrement);
        }

        public override string ToString(Encoding encoding)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ToString(encoding);
        }

        public override string ToString(int index, int length, Encoding encoding)
        {
            this.RecordLeakNonRefCountingOperation();
            return base.ToString(index, length, encoding);
        }
    }
}