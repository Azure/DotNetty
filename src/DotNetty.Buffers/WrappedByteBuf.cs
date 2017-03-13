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

    public class WrappedByteBuffer : IByteBuffer
    {
        protected readonly IByteBuffer Buf;

        protected WrappedByteBuffer(IByteBuffer buf)
        {
            Contract.Requires(buf != null);

            this.Buf = buf;
        }

        public int Capacity => this.Buf.Capacity;

        public virtual IByteBuffer AdjustCapacity(int newCapacity)
        {
            this.Buf.AdjustCapacity(newCapacity);
            return this;
        }

        public int MaxCapacity => this.Buf.MaxCapacity;

        public IByteBufferAllocator Allocator => this.Buf.Allocator;

        public ByteOrder Order => this.Buf.Order;

        public virtual IByteBuffer WithOrder(ByteOrder endianness) => this.Buf.WithOrder(endianness);

        public IByteBuffer Unwrap() => this.Buf;

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

        public virtual ushort GetUnsignedShort(int index) => this.Buf.GetUnsignedShort(index);

        public virtual int GetInt(int index) => this.Buf.GetInt(index);

        public virtual uint GetUnsignedInt(int index) => this.Buf.GetUnsignedInt(index);

        public virtual long GetLong(int index) => this.Buf.GetLong(index);

        public virtual int GetMedium(int index) => this.Buf.GetMedium(index);

        public virtual int GetUnsignedMedium(int index) => this.Buf.GetUnsignedMedium(index);

        public virtual char GetChar(int index) => this.Buf.GetChar(index);

        public virtual float GetFloat(int index) => this.Buf.GetFloat(index);

        public virtual double GetDouble(int index) => this.Buf.GetDouble(index);

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

        public virtual IByteBuffer SetUnsignedShort(int index, ushort value) => this.Buf.SetUnsignedShort(index, value);

        public virtual IByteBuffer SetInt(int index, int value)
        {
            this.Buf.SetInt(index, value);
            return this;
        }

        public virtual IByteBuffer SetMedium(int index, int value)
        {
            this.Buf.SetMedium(index, value);
            return this;
        }

        public virtual IByteBuffer SetUnsignedInt(int index, uint value) => this.Buf.SetUnsignedInt(index, value);

        public virtual IByteBuffer SetLong(int index, long value)
        {
            this.Buf.SetLong(index, value);
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

        public virtual IByteBuffer SetDouble(int index, double value)
        {
            this.Buf.SetDouble(index, value);
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

        public virtual IByteBuffer SetZero(int index, int length)
        {
            this.Buf.SetZero(index, length);
            return this;
        }

        public virtual bool ReadBoolean() => this.Buf.ReadBoolean();

        public virtual byte ReadByte() => this.Buf.ReadByte();

        public virtual short ReadShort() => this.Buf.ReadShort();

        public virtual ushort ReadUnsignedShort() => this.Buf.ReadUnsignedShort();

        public virtual int ReadInt() => this.Buf.ReadInt();

        public virtual uint ReadUnsignedInt() => this.Buf.ReadUnsignedInt();

        public virtual long ReadLong() => this.Buf.ReadLong();

        public virtual int ReadMedium() => this.Buf.ReadMedium();

        public virtual int ReadUnsignedMedium() => this.Buf.ReadUnsignedMedium();

        public virtual char ReadChar() => this.Buf.ReadChar();

        public virtual float ReadFloat() => this.Buf.ReadFloat();

        public virtual double ReadDouble() => this.Buf.ReadDouble();

        public virtual IByteBuffer ReadBytes(int length) => this.Buf.ReadBytes(length);

        public virtual IByteBuffer ReadSlice(int length) => this.Buf.ReadSlice(length);

        public virtual Task WriteBytesAsync(Stream stream, int length) => this.Buf.WriteBytesAsync(stream, length);

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

        public virtual IByteBuffer WriteUnsignedShort(ushort value) => this.Buf.WriteUnsignedShort(value);

        public virtual IByteBuffer WriteInt(int value)
        {
            this.Buf.WriteInt(value);
            return this;
        }

        public virtual IByteBuffer WriteUnsignedInt(uint value) => this.Buf.WriteUnsignedInt(value);

        public virtual IByteBuffer WriteLong(long value)
        {
            this.Buf.WriteLong(value);
            return this;
        }

        public virtual IByteBuffer WriteChar(char value)
        {
            this.Buf.WriteChar(value);
            return this;
        }

        public virtual IByteBuffer WriteUnsignedMedium(int value) => this.Buf.WriteUnsignedMedium(value);

        public virtual IByteBuffer WriteMedium(int value)
        {
            this.Buf.WriteMedium(value);
            return this;
        }

        public virtual IByteBuffer WriteFloat(float value)
        {
            this.Buf.WriteFloat(value);
            return this;
        }

        public virtual IByteBuffer WriteDouble(double value)
        {
            this.Buf.WriteDouble(value);
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

        public int IoBufferCount => this.Buf.IoBufferCount;

        public ArraySegment<byte> GetIoBuffer() => this.Buf.GetIoBuffer();

        public ArraySegment<byte> GetIoBuffer(int index, int length) => this.Buf.GetIoBuffer(index, length);

        public ArraySegment<byte>[] GetIoBuffers() => this.Buf.GetIoBuffers();

        public ArraySegment<byte>[] GetIoBuffers(int index, int length) => this.Buf.GetIoBuffers(index, length);

        public virtual Task WriteBytesAsync(Stream input, int length, CancellationToken cancellationToken) => this.Buf.WriteBytesAsync(input, length, cancellationToken);

        public virtual IByteBuffer WriteZero(int length)
        {
            this.Buf.WriteZero(length);
            return this;
        }

        //public virtual int IndexOf(int fromIndex, int toIndex, byte value)
        //{
        //    return this.buf.IndexOf(fromIndex, toIndex, value);
        //}

        //public virtual int BytesBefore(byte value)
        //{
        //    return this.buf.BytesBefore(value);
        //}

        //public virtual int BytesBefore(int length, byte value)
        //{
        //    return this.buf.BytesBefore(length, value);
        //}

        //public virtual int BytesBefore(int index, int length, byte value)
        //{
        //    return this.buf.BytesBefore(index, length, value);
        //}

        public virtual IByteBuffer Copy() => this.Buf.Copy();

        public virtual IByteBuffer Copy(int index, int length) => this.Buf.Copy(index, length);

        public virtual IByteBuffer Slice() => this.Buf.Slice();

        public virtual IByteBuffer Slice(int index, int length) => this.Buf.Slice(index, length);

        public virtual byte[] ToArray() => this.Buf.ToArray();

        public virtual IByteBuffer Duplicate() => this.Buf.Duplicate();

        public virtual bool HasArray => this.Buf.HasArray;

        public virtual byte[] Array => this.Buf.Array;

        public virtual int ArrayOffset => this.Buf.ArrayOffset;

        public override int GetHashCode() => this.Buf.GetHashCode();

        public override bool Equals(object obj) => this.Buf.Equals(obj);

        public bool Equals(IByteBuffer buffer) => this.Buf.Equals(buffer);

        public virtual int CompareTo(IByteBuffer buffer) => this.Buf.CompareTo(buffer);

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

        public int ForEachByte(ByteProcessor processor) => this.Buf.ForEachByte(processor);

        public int ForEachByte(int index, int length, ByteProcessor processor) => this.Buf.ForEachByte(index, length, processor);

        public int ForEachByteDesc(ByteProcessor processor) => this.Buf.ForEachByteDesc(processor);

        public int ForEachByteDesc(int index, int length, ByteProcessor processor) => this.Buf.ForEachByteDesc(processor);

        public virtual string ToString(Encoding encoding) => this.Buf.ToString(encoding);

        public virtual string ToString(int index, int length, Encoding encoding) => this.Buf.ToString(index, length, encoding);
    }
}