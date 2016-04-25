// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
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

        public int Capacity
        {
            get { return this.Buf.Capacity; }
        }

        public virtual IByteBuffer AdjustCapacity(int newCapacity)
        {
            this.Buf.AdjustCapacity(newCapacity);
            return this;
        }

        public int MaxCapacity
        {
            get { return this.Buf.MaxCapacity; }
        }

        public IByteBufferAllocator Allocator
        {
            get { return this.Buf.Allocator; }
        }

        public ByteOrder Order
        {
            get { return this.Buf.Order; }
        }

        public virtual IByteBuffer WithOrder(ByteOrder endianness)
        {
            return this.Buf.WithOrder(endianness);
        }

        public IByteBuffer Unwrap()
        {
            return this.Buf;
        }

        public int ReaderIndex
        {
            get { return this.Buf.ReaderIndex; }
        }

        public IByteBuffer SetReaderIndex(int readerIndex)
        {
            this.Buf.SetReaderIndex(readerIndex);
            return this;
        }

        public int WriterIndex
        {
            get { return this.Buf.WriterIndex; }
        }

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

        public int ReadableBytes
        {
            get { return this.Buf.ReadableBytes; }
        }

        public int WritableBytes
        {
            get { return this.Buf.WritableBytes; }
        }

        public int MaxWritableBytes
        {
            get { return this.Buf.MaxWritableBytes; }
        }

        public bool IsReadable()
        {
            return this.Buf.IsReadable();
        }

        public bool IsWritable()
        {
            return this.Buf.IsWritable();
        }

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

        public virtual int EnsureWritable(int minWritableBytes, bool force)
        {
            return this.Buf.EnsureWritable(minWritableBytes, force);
        }

        public virtual bool GetBoolean(int index)
        {
            return this.Buf.GetBoolean(index);
        }

        public virtual byte GetByte(int index)
        {
            return this.Buf.GetByte(index);
        }

        public virtual short GetShort(int index)
        {
            return this.Buf.GetShort(index);
        }

        public virtual ushort GetUnsignedShort(int index)
        {
            return this.Buf.GetUnsignedShort(index);
        }

        public virtual int GetInt(int index)
        {
            return this.Buf.GetInt(index);
        }

        public virtual uint GetUnsignedInt(int index)
        {
            return this.Buf.GetUnsignedInt(index);
        }

        public virtual long GetLong(int index)
        {
            return this.Buf.GetLong(index);
        }

        public virtual char GetChar(int index)
        {
            return this.Buf.GetChar(index);
        }

        // todo: port: complete
        //public virtual float GetFloat(int index)
        //{
        //    return this.buf.GetFloat(index);
        //}

        public virtual double GetDouble(int index)
        {
            return this.Buf.GetDouble(index);
        }

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

        public virtual IByteBuffer SetUnsignedShort(int index, ushort value)
        {
            return this.Buf.SetUnsignedShort(index, value);
        }

        public virtual IByteBuffer SetInt(int index, int value)
        {
            this.Buf.SetInt(index, value);
            return this;
        }

        public virtual IByteBuffer SetUnsignedInt(int index, uint value)
        {
            return this.Buf.SetUnsignedInt(index, value);
        }

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

        // todo: port: complete
        //public virtual IByteBuffer SetFloat(int index, float value)
        //{
        //    buf.SetFloat(index, value);
        //    return this;
        //}

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

        public virtual Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            return this.Buf.SetBytesAsync(index, src, length, cancellationToken);
        }

        // todo: port: complete
        //public virtual IByteBuffer SetZero(int index, int length)
        //{
        //    buf.SetZero(index, length);
        //    return this;
        //}

        public virtual bool ReadBoolean()
        {
            return this.Buf.ReadBoolean();
        }

        public virtual byte ReadByte()
        {
            return this.Buf.ReadByte();
        }

        public virtual short ReadShort()
        {
            return this.Buf.ReadShort();
        }

        public virtual ushort ReadUnsignedShort()
        {
            return this.Buf.ReadUnsignedShort();
        }

        public virtual int ReadInt()
        {
            return this.Buf.ReadInt();
        }

        public virtual uint ReadUnsignedInt()
        {
            return this.Buf.ReadUnsignedInt();
        }

        public virtual long ReadLong()
        {
            return this.Buf.ReadLong();
        }

        public virtual char ReadChar()
        {
            return this.Buf.ReadChar();
        }

        // todo: port: complete
        //public virtual float ReadFloat()
        //{
        //    return buf.ReadFloat();
        //}

        public virtual double ReadDouble()
        {
            return this.Buf.ReadDouble();
        }

        public virtual IByteBuffer ReadBytes(int length)
        {
            return this.Buf.ReadBytes(length);
        }

        public virtual IByteBuffer ReadSlice(int length)
        {
            return this.Buf.ReadSlice(length);
        }

        public virtual Task WriteBytesAsync(Stream stream, int length)
        {
            return this.Buf.WriteBytesAsync(stream, length);
        }

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

        public virtual IByteBuffer WriteUnsignedShort(ushort value)
        {
            return this.Buf.WriteUnsignedShort(value);
        }

        public virtual IByteBuffer WriteInt(int value)
        {
            this.Buf.WriteInt(value);
            return this;
        }

        public virtual IByteBuffer WriteUnsignedInt(uint value)
        {
            return this.Buf.WriteUnsignedInt(value);
        }

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

        // todo: port: complete
        //public virtual IByteBuffer WriteFloat(float value)
        //{
        //    buf.WriteFloat(value);
        //    return this;
        //}

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

        public virtual Task WriteBytesAsync(Stream input, int length, CancellationToken cancellationToken)
        {
            return this.Buf.WriteBytesAsync(input, length, cancellationToken);
        }

        // todo: port: complete
        //public virtual IByteBuffer WriteZero(int length)
        //{
        //    buf.WriteZero(length);
        //    return this;
        //}

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

        //public virtual int ForEachByte(ByteProcessor processor)
        //{
        //    return this.buf.ForEachByte(processor);
        //}

        //public virtual int ForEachByte(int index, int length, ByteProcessor processor)
        //{
        //    return this.buf.ForEachByte(index, length, processor);
        //}

        //public virtual int ForEachByteDesc(ByteProcessor processor)
        //{
        //    return this.buf.ForEachByteDesc(processor);
        //}

        //public virtual int ForEachByteDesc(int index, int length, ByteProcessor processor)
        //{
        //    return this.buf.ForEachByteDesc(index, length, processor);
        //}

        public virtual IByteBuffer Copy()
        {
            return this.Buf.Copy();
        }

        public virtual IByteBuffer Copy(int index, int length)
        {
            return this.Buf.Copy(index, length);
        }

        public virtual IByteBuffer Slice()
        {
            return this.Buf.Slice();
        }

        public virtual IByteBuffer Slice(int index, int length)
        {
            return this.Buf.Slice(index, length);
        }

        public virtual byte[] ToArray()
        {
            return this.Buf.ToArray();
        }

        public virtual IByteBuffer Duplicate()
        {
            return this.Buf.Duplicate();
        }

        public virtual bool HasArray
        {
            get { return this.Buf.HasArray; }
        }

        public virtual byte[] Array
        {
            get { return this.Buf.Array; }
        }

        public virtual int ArrayOffset
        {
            get { return this.Buf.ArrayOffset; }
        }

        // todo: port: complete
        //    public virtual String toString(Charset charset)
        //    {
        //        return buf.toString(charset);
        //    }

        //public virtual String toString(int index, int length, Charset charset)
        //    {
        //        return buf.ToString(index, length, charset);
        //    }

        public override int GetHashCode()
        {
            return this.Buf.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return this.Buf.Equals(obj);
        }

        // todo: port: complete
        //public virtual int CompareTo(IByteBuffer buffer)
        //{
        //    return this.buf.CompareTo(buffer);
        //}

        public override string ToString()
        {
            return this.GetType().Name + '(' + this.Buf + ')';
        }

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

        public bool IsReadable(int size)
        {
            return this.Buf.IsReadable(size);
        }

        public bool IsWritable(int size)
        {
            return this.Buf.IsWritable(size);
        }

        public int ReferenceCount
        {
            get { return this.Buf.ReferenceCount; }
        }

        public virtual bool Release()
        {
            return this.Buf.Release();
        }

        public virtual bool Release(int decrement)
        {
            return this.Buf.Release(decrement);
        }

        public int ForEachByte(ByteProcessor processor)
        {
            return this.ForEachByte(processor);
        }

        public int ForEachByte(int index, int length, ByteProcessor processor)
        {
            return this.ForEachByte(index, length, processor);
        }

        public int ForEachByteDesc(ByteProcessor processor)
        {
            return this.ForEachByteDesc(processor);
        }

        public int ForEachByteDesc(int index, int length, ByteProcessor processor)
        {
            return this.ForEachByteDesc(processor);
        }

        public virtual string ToString(Encoding encoding)
        {
            return Buf.ToString(encoding);
        }

        public virtual string ToString(int index, int length, Encoding encoding)
        {
            return Buf.ToString(index, length, encoding);
        }
    }
}