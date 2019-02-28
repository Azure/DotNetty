// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    /// Wraps another <see cref="IByteBuffer"/>.
    /// 
    /// It's important that the {@link #readerIndex()} and {@link #writerIndex()} will not do any adjustments on the
    /// indices on the fly because of internal optimizations made by {@link ByteBufUtil#writeAscii(ByteBuf, CharSequence)}
    /// and {@link ByteBufUtil#writeUtf8(ByteBuf, CharSequence)}.
    class WrappedByteBuffer : IByteBuffer
    {
        protected readonly IByteBuffer Buf;

        protected WrappedByteBuffer(IByteBuffer buf)
        {
            Contract.Requires(buf != null);

            this.Buf = buf;
        }

        public bool HasMemoryAddress => this.Buf.HasMemoryAddress;

        public ref byte GetPinnableMemoryAddress() => ref this.Buf.GetPinnableMemoryAddress();

        public IntPtr AddressOfPinnedMemory() => this.Buf.AddressOfPinnedMemory();

        public int Capacity => this.Buf.Capacity;

        public virtual IByteBuffer AdjustCapacity(int newCapacity)
        {
            this.Buf.AdjustCapacity(newCapacity);
            return this;
        }

        public int MaxCapacity => this.Buf.MaxCapacity;

        public IByteBufferAllocator Allocator => this.Buf.Allocator;

        public IByteBuffer Unwrap() => this.Buf;

        public bool IsDirect => this.Buf.IsDirect;

        public int ReaderIndex => this.Buf.ReaderIndex;

        public IByteBuffer SetReaderIndex(int readerIndex)
        {
            this.Buf.SetReaderIndex(readerIndex);
            return this;
        }

        public int WriterIndex => this.Buf.WriterIndex;

        public IByteBuffer SetWriterIndex(int writerIndex)
        {
            this.Buf.SetWriterIndex(writerIndex);
            return this;
        }

        public virtual IByteBuffer SetIndex(int readerIndex, int writerIndex)
        {
            this.Buf.SetIndex(readerIndex, writerIndex);
            return this;
        }

        public int ReadableBytes => this.Buf.ReadableBytes;

        public int WritableBytes => this.Buf.WritableBytes;

        public int MaxWritableBytes => this.Buf.MaxWritableBytes;

        public bool IsReadable() => this.Buf.IsReadable();

        public bool IsWritable() => this.Buf.IsWritable();

        public IByteBuffer Clear()
        {
            this.Buf.Clear();
            return this;
        }

        public IByteBuffer MarkReaderIndex()
        {
            this.Buf.MarkReaderIndex();
            return this;
        }

        public IByteBuffer ResetReaderIndex()
        {
            this.Buf.ResetReaderIndex();
            return this;
        }

        public IByteBuffer MarkWriterIndex()
        {
            this.Buf.MarkWriterIndex();
            return this;
        }

        public IByteBuffer ResetWriterIndex()
        {
            this.Buf.ResetWriterIndex();
            return this;
        }

        public virtual IByteBuffer DiscardReadBytes()
        {
            this.Buf.DiscardReadBytes();
            return this;
        }

        public virtual IByteBuffer DiscardSomeReadBytes()
        {
            this.Buf.DiscardSomeReadBytes();
            return this;
        }

        public virtual IByteBuffer EnsureWritable(int minWritableBytes)
        {
            this.Buf.EnsureWritable(minWritableBytes);
            return this;
        }

        public virtual int EnsureWritable(int minWritableBytes, bool force) => this.Buf.EnsureWritable(minWritableBytes, force);

        public virtual bool GetBoolean(int index) => this.Buf.GetBoolean(index);

        public virtual byte GetByte(int index) => this.Buf.GetByte(index);

        public virtual short GetShort(int index) => this.Buf.GetShort(index);

        public virtual short GetShortLE(int index) => this.Buf.GetShortLE(index);

        public virtual ushort GetUnsignedShort(int index) => this.Buf.GetUnsignedShort(index);

        public virtual ushort GetUnsignedShortLE(int index) => this.Buf.GetUnsignedShortLE(index);

        public virtual int GetMedium(int index) => this.Buf.GetMedium(index);

        public virtual int GetMediumLE(int index) => this.Buf.GetMediumLE(index);

        public virtual int GetUnsignedMedium(int index) => this.Buf.GetUnsignedMedium(index);

        public virtual int GetUnsignedMediumLE(int index) => this.Buf.GetUnsignedMediumLE(index);

        public virtual int GetInt(int index) => this.Buf.GetInt(index);

        public virtual int GetIntLE(int index) => this.Buf.GetIntLE(index);

        public virtual uint GetUnsignedInt(int index) => this.Buf.GetUnsignedInt(index);

        public virtual uint GetUnsignedIntLE(int index) => this.Buf.GetUnsignedIntLE(index);

        public virtual long GetLong(int index) => this.Buf.GetLong(index);

        public virtual long GetLongLE(int index) => this.Buf.GetLongLE(index);

        public virtual char GetChar(int index) => this.Buf.GetChar(index);

        public virtual float GetFloat(int index) => this.Buf.GetFloat(index);

        public float GetFloatLE(int index) => this.Buf.GetFloatLE(index);

        public virtual double GetDouble(int index) => this.Buf.GetDouble(index);

        public double GetDoubleLE(int index) => this.Buf.GetDoubleLE(index);

        public virtual IByteBuffer GetBytes(int index, IByteBuffer dst)
        {
            this.Buf.GetBytes(index, dst);
            return this;
        }

        public virtual IByteBuffer GetBytes(int index, IByteBuffer dst, int length)
        {
            this.Buf.GetBytes(index, dst, length);
            return this;
        }

        public virtual IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            this.Buf.GetBytes(index, dst, dstIndex, length);
            return this;
        }

        public virtual IByteBuffer GetBytes(int index, byte[] dst)
        {
            this.Buf.GetBytes(index, dst);
            return this;
        }

        public virtual IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            this.Buf.GetBytes(index, dst, dstIndex, length);
            return this;
        }

        public virtual IByteBuffer GetBytes(int index, Stream output, int length)
        {
            this.Buf.GetBytes(index, output, length);
            return this;
        }

        public ICharSequence GetCharSequence(int index, int length, Encoding encoding) => this.Buf.GetCharSequence(index, length, encoding);

        public string GetString(int index, int length, Encoding encoding) => this.Buf.GetString(index, length, encoding);

        public virtual IByteBuffer SetBoolean(int index, bool value)
        {
            this.Buf.SetBoolean(index, value);
            return this;
        }

        public virtual IByteBuffer SetByte(int index, int value)
        {
            this.Buf.SetByte(index, value);
            return this;
        }

        public virtual IByteBuffer SetShort(int index, int value)
        {
            this.Buf.SetShort(index, value);
            return this;
        }

        public virtual IByteBuffer SetShortLE(int index, int value)
        {
            this.Buf.SetShortLE(index, value);
            return this;
        }

        public IByteBuffer SetUnsignedShort(int index, ushort value) => this.Buf.SetUnsignedShort(index, value);

        public IByteBuffer SetUnsignedShortLE(int index, ushort value) => this.Buf.SetUnsignedShortLE(index, value);

        public virtual IByteBuffer SetMedium(int index, int value)
        {
            this.Buf.SetMedium(index, value);
            return this;
        }

        public virtual IByteBuffer SetMediumLE(int index, int value)
        {
            this.Buf.SetMediumLE(index, value);
            return this;
        }

        public virtual IByteBuffer SetInt(int index, int value)
        {
            this.Buf.SetInt(index, value);
            return this;
        }

        public virtual IByteBuffer SetIntLE(int index, int value)
        {
            this.Buf.SetIntLE(index, value);
            return this;
        }

        public IByteBuffer SetUnsignedInt(int index, uint value) => this.Buf.SetUnsignedInt(index, value);

        public IByteBuffer SetUnsignedIntLE(int index, uint value) => this.Buf.SetUnsignedIntLE(index, value);

        public virtual IByteBuffer SetLong(int index, long value)
        {
            this.Buf.SetLong(index, value);
            return this;
        }

        public virtual IByteBuffer SetLongLE(int index, long value)
        {
            this.Buf.SetLongLE(index, value);
            return this;
        }

        public virtual IByteBuffer SetChar(int index, char value)
        {
            this.Buf.SetChar(index, value);
            return this;
        }

        public virtual IByteBuffer SetFloat(int index, float value)
        {
            this.Buf.SetFloat(index, value);
            return this;
        }

        public IByteBuffer SetFloatLE(int index, float value)
        {
            this.Buf.SetFloatLE(index, value);
            return this;
        }

        public virtual IByteBuffer SetDouble(int index, double value)
        {
            this.Buf.SetDouble(index, value);
            return this;
        }

        public IByteBuffer SetDoubleLE(int index, double value)
        {
            this.Buf.SetDoubleLE(index, value);
            return this;
        }

        public virtual IByteBuffer SetBytes(int index, IByteBuffer src)
        {
            this.Buf.SetBytes(index, src);
            return this;
        }

        public virtual IByteBuffer SetBytes(int index, IByteBuffer src, int length)
        {
            this.Buf.SetBytes(index, src, length);
            return this;
        }

        public virtual IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            this.Buf.SetBytes(index, src, srcIndex, length);
            return this;
        }

        public virtual IByteBuffer SetBytes(int index, byte[] src)
        {
            this.Buf.SetBytes(index, src);
            return this;
        }

        public virtual IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            this.Buf.SetBytes(index, src, srcIndex, length);
            return this;
        }

        public virtual Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken) => this.Buf.SetBytesAsync(index, src, length, cancellationToken);

        public int SetString(int index, string value, Encoding encoding) => this.Buf.SetString(index, value, encoding);

        public virtual IByteBuffer SetZero(int index, int length)
        {
            this.Buf.SetZero(index, length);
            return this;
        }

        public int SetCharSequence(int index, ICharSequence sequence, Encoding encoding) => this.Buf.SetCharSequence(index, sequence, encoding);

        public virtual bool ReadBoolean() => this.Buf.ReadBoolean();

        public virtual byte ReadByte() => this.Buf.ReadByte();

        public virtual short ReadShort() => this.Buf.ReadShort();

        public virtual short ReadShortLE() => this.Buf.ReadShortLE();

        public virtual ushort ReadUnsignedShort() => this.Buf.ReadUnsignedShort();

        public virtual ushort ReadUnsignedShortLE() => this.Buf.ReadUnsignedShortLE();

        public virtual int ReadMedium() => this.Buf.ReadMedium();

        public virtual int ReadMediumLE() => this.Buf.ReadMediumLE();

        public virtual int ReadUnsignedMedium() => this.Buf.ReadUnsignedMedium();

        public virtual int ReadUnsignedMediumLE() => this.Buf.ReadUnsignedMediumLE();

        public virtual int ReadInt() => this.Buf.ReadInt();

        public virtual int ReadIntLE() => this.Buf.ReadIntLE();

        public virtual uint ReadUnsignedInt() => this.Buf.ReadUnsignedInt();

        public virtual uint ReadUnsignedIntLE() => this.Buf.ReadUnsignedIntLE();

        public virtual long ReadLong() => this.Buf.ReadLong();

        public virtual long ReadLongLE() => this.Buf.ReadLongLE();

        public virtual char ReadChar() => this.Buf.ReadChar();

        public virtual float ReadFloat() => this.Buf.ReadFloat();

        public float ReadFloatLE() => this.Buf.ReadFloatLE();

        public virtual double ReadDouble() => this.Buf.ReadDouble();

        public double ReadDoubleLE() => this.Buf.ReadDoubleLE();

        public virtual IByteBuffer ReadBytes(int length) => this.Buf.ReadBytes(length);

        public virtual IByteBuffer ReadSlice(int length) => this.Buf.ReadSlice(length);

        public virtual IByteBuffer ReadRetainedSlice(int length) => this.Buf.ReadRetainedSlice(length);

        public Task WriteBytesAsync(Stream stream, int length) => this.Buf.WriteBytesAsync(stream, length);

        public virtual IByteBuffer ReadBytes(IByteBuffer dst)
        {
            this.Buf.ReadBytes(dst);
            return this;
        }

        public virtual IByteBuffer ReadBytes(IByteBuffer dst, int length)
        {
            this.Buf.ReadBytes(dst, length);
            return this;
        }

        public virtual IByteBuffer ReadBytes(IByteBuffer dst, int dstIndex, int length)
        {
            this.Buf.ReadBytes(dst, dstIndex, length);
            return this;
        }

        public virtual IByteBuffer ReadBytes(byte[] dst)
        {
            this.Buf.ReadBytes(dst);
            return this;
        }

        public virtual IByteBuffer ReadBytes(byte[] dst, int dstIndex, int length)
        {
            this.Buf.ReadBytes(dst, dstIndex, length);
            return this;
        }

        public virtual IByteBuffer ReadBytes(Stream output, int length)
        {
            this.Buf.ReadBytes(output, length);
            return this;
        }

        public ICharSequence ReadCharSequence(int length, Encoding encoding) => this.Buf.ReadCharSequence(length, encoding);

        public string ReadString(int length, Encoding encoding) => this.Buf.ReadString(length, encoding);

        public virtual IByteBuffer SkipBytes(int length)
        {
            this.Buf.SkipBytes(length);
            return this;
        }

        public virtual IByteBuffer WriteBoolean(bool value)
        {
            this.Buf.WriteBoolean(value);
            return this;
        }

        public virtual IByteBuffer WriteByte(int value)
        {
            this.Buf.WriteByte(value);
            return this;
        }

        public virtual IByteBuffer WriteShort(int value)
        {
            this.Buf.WriteShort(value);
            return this;
        }

        public virtual IByteBuffer WriteShortLE(int value)
        {
            this.Buf.WriteShortLE(value);
            return this;
        }

        public IByteBuffer WriteUnsignedShort(ushort value) => this.Buf.WriteUnsignedShort(value);

        public IByteBuffer WriteUnsignedShortLE(ushort value) => this.Buf.WriteUnsignedShortLE(value);

        public virtual IByteBuffer WriteMedium(int value)
        {
            this.Buf.WriteMedium(value);
            return this;
        }

        public virtual IByteBuffer WriteMediumLE(int value)
        {
            this.Buf.WriteMediumLE(value);
            return this;
        }

        public virtual IByteBuffer WriteInt(int value)
        {
            this.Buf.WriteInt(value);
            return this;
        }

        public virtual IByteBuffer WriteIntLE(int value)
        {
            this.Buf.WriteIntLE(value);
            return this;
        }

        public virtual IByteBuffer WriteLong(long value)
        {
            this.Buf.WriteLong(value);
            return this;
        }

        public virtual IByteBuffer WriteLongLE(long value)
        {
            this.Buf.WriteLongLE(value);
            return this;
        }

        public virtual IByteBuffer WriteChar(char value)
        {
            this.Buf.WriteChar(value);
            return this;
        }

        public virtual IByteBuffer WriteFloat(float value)
        {
            this.Buf.WriteFloat(value);
            return this;
        }

        public IByteBuffer WriteFloatLE(float value)
        {
            this.Buf.WriteFloatLE(value);
            return this;
        }

        public virtual IByteBuffer WriteDouble(double value)
        {
            this.Buf.WriteDouble(value);
            return this;
        }

        public IByteBuffer WriteDoubleLE(double value)
        {
            this.Buf.WriteDoubleLE(value);
            return this;
        }

        public virtual IByteBuffer WriteBytes(IByteBuffer src)
        {
            this.Buf.WriteBytes(src);
            return this;
        }

        public virtual IByteBuffer WriteBytes(IByteBuffer src, int length)
        {
            this.Buf.WriteBytes(src, length);
            return this;
        }

        public virtual IByteBuffer WriteBytes(IByteBuffer src, int srcIndex, int length)
        {
            this.Buf.WriteBytes(src, srcIndex, length);
            return this;
        }

        public virtual IByteBuffer WriteBytes(byte[] src)
        {
            this.Buf.WriteBytes(src);
            return this;
        }

        public virtual IByteBuffer WriteBytes(byte[] src, int srcIndex, int length)
        {
            this.Buf.WriteBytes(src, srcIndex, length);
            return this;
        }

        public virtual Task WriteBytesAsync(Stream input, int length, CancellationToken cancellationToken) => this.Buf.WriteBytesAsync(input, length, cancellationToken);

        public virtual IByteBuffer WriteZero(int length)
        {
            this.Buf.WriteZero(length);
            return this;
        }

        public int WriteCharSequence(ICharSequence sequence, Encoding encoding) => this.Buf.WriteCharSequence(sequence, encoding);

        public int WriteString(string value, Encoding encoding) => this.Buf.WriteString(value, encoding);

        public virtual int IndexOf(int fromIndex, int toIndex, byte value) => this.Buf.IndexOf(fromIndex, toIndex, value);

        public virtual int BytesBefore(byte value) => this.Buf.BytesBefore(value);

        public virtual int BytesBefore(int length, byte value) => this.Buf.BytesBefore(length, value);

        public virtual int BytesBefore(int index, int length, byte value) => this.Buf.BytesBefore(index, length, value);

        public virtual int ForEachByte(IByteProcessor processor) => this.Buf.ForEachByte(processor);

        public virtual int ForEachByte(int index, int length, IByteProcessor processor) => this.Buf.ForEachByte(index, length, processor);

        public virtual int ForEachByteDesc(IByteProcessor processor) => this.Buf.ForEachByteDesc(processor);

        public virtual int ForEachByteDesc(int index, int length, IByteProcessor processor) => this.Buf.ForEachByteDesc(index, length, processor);

        public virtual IByteBuffer Copy() => this.Buf.Copy();

        public virtual IByteBuffer Copy(int index, int length) => this.Buf.Copy(index, length);

        public virtual IByteBuffer Slice() => this.Buf.Slice();

        public virtual IByteBuffer RetainedSlice() => this.Buf.RetainedSlice();
        
        public virtual IByteBuffer Slice(int index, int length) => this.Buf.Slice(index, length);

        public virtual IByteBuffer RetainedSlice(int index, int length) => this.Buf.RetainedSlice(index, length);

        public virtual IByteBuffer Duplicate() => this.Buf.Duplicate();

        public virtual IByteBuffer RetainedDuplicate() => this.Buf.RetainedDuplicate();

        public virtual int IoBufferCount => this.Buf.IoBufferCount;

        public virtual ArraySegment<byte> GetIoBuffer() => this.Buf.GetIoBuffer();

        public virtual ArraySegment<byte> GetIoBuffer(int index, int length) => this.Buf.GetIoBuffer(index, length);

        public virtual ArraySegment<byte>[] GetIoBuffers() => this.Buf.GetIoBuffers();

        public virtual ArraySegment<byte>[] GetIoBuffers(int index, int length) => this.Buf.GetIoBuffers(index, length);

        public bool HasArray => this.Buf.HasArray;

        public int ArrayOffset => this.Buf.ArrayOffset;

        public byte[] Array => this.Buf.Array;

        public virtual string ToString(Encoding encoding) => this.Buf.ToString(encoding);

        public virtual string ToString(int index, int length, Encoding encoding) => this.Buf.ToString(index, length, encoding);

        public override int GetHashCode() => this.Buf.GetHashCode();

        public override bool Equals(object obj) => this.Buf.Equals(obj);

        public bool Equals(IByteBuffer buffer) => this.Buf.Equals(buffer);

        public int CompareTo(IByteBuffer buffer) => this.Buf.CompareTo(buffer);

        public override string ToString() => this.GetType().Name + '(' + this.Buf + ')';

        public virtual IReferenceCounted Retain(int increment)
        {
            this.Buf.Retain(increment);
            return this;
        }

        public virtual IReferenceCounted Retain()
        {
            this.Buf.Retain();
            return this;
        }

        public virtual IReferenceCounted Touch()
        {
            this.Buf.Touch();
            return this;
        }

        public virtual IReferenceCounted Touch(object hint)
        {
            this.Buf.Touch(hint);
            return this;
        }

        public bool IsReadable(int size) => this.Buf.IsReadable(size);

        public bool IsWritable(int size) => this.Buf.IsWritable(size);

        public int ReferenceCount => this.Buf.ReferenceCount;

        public virtual bool Release() => this.Buf.Release();

        public virtual bool Release(int decrement) => this.Buf.Release(decrement);
    }
}