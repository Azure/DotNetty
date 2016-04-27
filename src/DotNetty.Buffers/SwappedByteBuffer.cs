// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// Wrapper which swaps the <see cref="ByteOrder"/> of a <see cref="IByteBuffer"/>.
    /// </summary>
	public class SwappedByteBuffer : IByteBuffer
    {
        readonly IByteBuffer buf;
        readonly ByteOrder order;

        public SwappedByteBuffer(IByteBuffer buf)
        {
            Contract.Requires(buf != null);

            this.buf = buf;
            if (buf.Order == ByteOrder.BigEndian)
            {
                this.order = ByteOrder.LittleEndian;
            }
            else
            {
                this.order = ByteOrder.BigEndian;
            }
        }

        public int ReferenceCount
        {
            get { return this.buf.ReferenceCount; }
        }

        public IReferenceCounted Retain()
        {
            this.buf.Retain();
            return this;
        }

        public IReferenceCounted Retain(int increment)
        {
            this.buf.Retain(increment);
            return this;
        }

        public IReferenceCounted Touch()
        {
            this.buf.Touch();
            return this;
        }

        public IReferenceCounted Touch(object hint)
        {
            this.buf.Touch(hint);
            return this;
        }

        public bool Release()
        {
            return this.buf.Release();
        }

        public bool Release(int decrement)
        {
            return this.buf.Release(decrement);
        }

        public int Capacity
        {
            get { return this.buf.Capacity; }
        }

        public IByteBuffer AdjustCapacity(int newCapacity)
        {
            return this.buf.AdjustCapacity(newCapacity);
        }

        public int MaxCapacity
        {
            get { return this.buf.MaxCapacity; }
        }

        public IByteBufferAllocator Allocator
        {
            get { return this.buf.Allocator; }
        }

        public int ReaderIndex
        {
            get { return this.buf.ReaderIndex; }
        }

        public int WriterIndex
        {
            get { return this.buf.WriterIndex; }
        }

        public IByteBuffer SetWriterIndex(int writerIndex)
        {
            this.buf.SetWriterIndex(writerIndex);
            return this;
        }

        public IByteBuffer SetReaderIndex(int readerIndex)
        {
            this.buf.SetReaderIndex(readerIndex);
            return this;
        }

        public IByteBuffer SetIndex(int readerIndex, int writerIndex)
        {
            this.buf.SetIndex(readerIndex, writerIndex);
            return this;
        }

        public int ReadableBytes
        {
            get { return this.buf.ReadableBytes; }
        }

        public int WritableBytes
        {
            get { return this.buf.WritableBytes; }
        }

        public int MaxWritableBytes
        {
            get { return this.buf.MaxWritableBytes; }
        }

        public bool IsReadable()
        {
            return this.buf.IsReadable();
        }

        public bool IsReadable(int size)
        {
            return this.buf.IsReadable(size);
        }

        public bool IsWritable()
        {
            return this.buf.IsWritable();
        }

        public bool IsWritable(int size)
        {
            return this.buf.IsWritable(size);
        }

        public IByteBuffer Clear()
        {
            this.buf.Clear();
            return this;
        }

        public IByteBuffer MarkReaderIndex()
        {
            this.buf.MarkReaderIndex();
            return this;
        }

        public IByteBuffer ResetReaderIndex()
        {
            this.buf.ResetReaderIndex();
            return this;
        }

        public IByteBuffer MarkWriterIndex()
        {
            this.buf.MarkWriterIndex();
            return this;
        }

        public IByteBuffer ResetWriterIndex()
        {
            this.buf.ResetWriterIndex();
            return this;
        }

        public IByteBuffer DiscardReadBytes()
        {
            this.buf.DiscardReadBytes();
            return this;
        }

        public IByteBuffer DiscardSomeReadBytes()
        {
            throw new NotImplementedException();
        }

        public IByteBuffer EnsureWritable(int minWritableBytes)
        {
            this.buf.EnsureWritable(minWritableBytes);
            return this;
        }

        public int EnsureWritable(int minWritableBytes, bool force)
        {
            throw new NotImplementedException();
        }

        public bool GetBoolean(int index)
        {
            return this.buf.GetBoolean(index);
        }

        public byte GetByte(int index)
        {
            return this.buf.GetByte(index);
        }

        public short GetShort(int index)
        {
            return ByteBufferUtil.SwapShort(this.buf.GetShort(index));
        }

        public ushort GetUnsignedShort(int index)
        {
            unchecked
            {
                return (ushort)(this.GetShort(index));
            }
        }

        public int GetInt(int index)
        {
            return ByteBufferUtil.SwapInt(this.buf.GetInt(index));
        }

        public uint GetUnsignedInt(int index)
        {
            unchecked
            {
                return (uint)this.GetInt(index);
            }
        }

        public long GetLong(int index)
        {
            return ByteBufferUtil.SwapLong(this.buf.GetLong(index));
        }

        public char GetChar(int index)
        {
            return (char)this.GetShort(index);
        }

        public double GetDouble(int index)
        {
            return BitConverter.Int64BitsToDouble(this.GetLong(index));
        }

        public IByteBuffer GetBytes(int index, IByteBuffer destination)
        {
            this.buf.GetBytes(index, destination);
            return this;
        }

        public IByteBuffer GetBytes(int index, IByteBuffer destination, int length)
        {
            this.buf.GetBytes(index, destination, length);
            return this;
        }

        public IByteBuffer GetBytes(int index, IByteBuffer destination, int dstIndex, int length)
        {
            this.buf.GetBytes(index, destination, dstIndex, length);
            return this;
        }

        public IByteBuffer GetBytes(int index, byte[] destination)
        {
            this.buf.GetBytes(index, destination);
            return this;
        }

        public IByteBuffer GetBytes(int index, byte[] destination, int dstIndex, int length)
        {
            this.buf.GetBytes(index, destination, dstIndex, length);
            return this;
        }

        public IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            this.buf.GetBytes(index, destination, length);
            return this;
        }

        public IByteBuffer SetBoolean(int index, bool value)
        {
            this.buf.SetBoolean(index, value);
            return this;
        }

        public IByteBuffer SetByte(int index, int value)
        {
            this.buf.SetByte(index, value);
            return this;
        }

        public IByteBuffer SetShort(int index, int value)
        {
            this.buf.SetShort(index, ByteBufferUtil.SwapShort((short)value));
            return this;
        }

        public IByteBuffer SetUnsignedShort(int index, ushort value)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer SetUnsignedShort(int index, int value)
        {
            unchecked
            {
                this.buf.SetUnsignedShort(index, (ushort)ByteBufferUtil.SwapShort((short)value));
            }
            return this;
        }

        public IByteBuffer SetInt(int index, int value)
        {
            this.buf.SetInt(index, ByteBufferUtil.SwapInt(value));
            return this;
        }

        public IByteBuffer SetUnsignedInt(int index, uint value)
        {
            unchecked
            {
                this.buf.SetUnsignedInt(index, (uint)ByteBufferUtil.SwapInt((int)value));
            }
            return this;
        }

        public IByteBuffer SetLong(int index, long value)
        {
            this.buf.SetLong(index, ByteBufferUtil.SwapLong(value));
            return this;
        }

        public IByteBuffer SetChar(int index, char value)
        {
            this.SetShort(index, (short)value);
            return this;
        }

        public IByteBuffer SetDouble(int index, double value)
        {
            this.SetLong(index, BitConverter.DoubleToInt64Bits(value));
            return this;
        }

        public IByteBuffer SetBytes(int index, IByteBuffer src)
        {
            this.buf.SetBytes(index, src);
            return this;
        }

        public IByteBuffer SetBytes(int index, IByteBuffer src, int length)
        {
            this.buf.SetBytes(index, src, length);
            return this;
        }

        public IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            this.buf.SetBytes(index, src, srcIndex, length);
            return this;
        }

        public IByteBuffer SetBytes(int index, byte[] src)
        {
            this.buf.SetBytes(index, src);
            return this;
        }

        public IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            this.buf.SetBytes(index, src, srcIndex, length);
            return this;
        }

        public Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            return this.buf.SetBytesAsync(index, src, length, cancellationToken);
        }

        public bool ReadBoolean()
        {
            return this.buf.ReadBoolean();
        }

        public byte ReadByte()
        {
            return this.buf.ReadByte();
        }

        public short ReadShort()
        {
            return ByteBufferUtil.SwapShort(this.buf.ReadShort());
        }

        public ushort ReadUnsignedShort()
        {
            unchecked
            {
                return (ushort)this.ReadShort();
            }
        }

        public int ReadInt()
        {
            return ByteBufferUtil.SwapInt(this.buf.ReadInt());
        }

        public uint ReadUnsignedInt()
        {
            unchecked
            {
                return (uint)this.ReadInt();
            }
        }

        public long ReadLong()
        {
            return ByteBufferUtil.SwapLong(this.buf.ReadLong());
        }

        public char ReadChar()
        {
            return (char)this.ReadShort();
        }

        public double ReadDouble()
        {
            return BitConverter.Int64BitsToDouble(this.ReadLong());
        }

        public IByteBuffer ReadBytes(int length)
        {
            return this.buf.ReadBytes(length).WithOrder(this.Order);
        }

        public IByteBuffer ReadBytes(IByteBuffer destination)
        {
            this.buf.ReadBytes(destination);
            return this;
        }

        public IByteBuffer ReadBytes(IByteBuffer destination, int length)
        {
            this.buf.ReadBytes(destination, length);
            return this;
        }

        public IByteBuffer ReadBytes(IByteBuffer destination, int dstIndex, int length)
        {
            this.buf.ReadBytes(destination, dstIndex, length);
            return this;
        }

        public IByteBuffer ReadBytes(byte[] destination)
        {
            this.buf.ReadBytes(destination);
            return this;
        }

        public IByteBuffer ReadBytes(byte[] destination, int dstIndex, int length)
        {
            this.buf.ReadBytes(destination, dstIndex, length);
            return this;
        }

        public IByteBuffer ReadBytes(Stream destination, int length)
        {
            this.buf.ReadBytes(destination, length);
            return this;
        }

        public IByteBuffer SkipBytes(int length)
        {
            this.buf.SkipBytes(length);
            return this;
        }

        public IByteBuffer WriteBoolean(bool value)
        {
            this.buf.WriteBoolean(value);
            return this;
        }

        public IByteBuffer WriteByte(int value)
        {
            this.buf.WriteByte(value);
            return this;
        }

        public IByteBuffer WriteShort(int value)
        {
            this.buf.WriteShort(ByteBufferUtil.SwapShort((short)value));
            return this;
        }

        public IByteBuffer WriteUnsignedShort(ushort value)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer WriteUnsignedShort(int value)
        {
            this.buf.WriteUnsignedShort(unchecked((ushort)ByteBufferUtil.SwapShort((short)value)));
            return this;
        }

        public IByteBuffer WriteInt(int value)
        {
            this.buf.WriteInt(ByteBufferUtil.SwapInt(value));
            return this;
        }

        public IByteBuffer WriteUnsignedInt(uint value)
        {
            unchecked
            {
                this.buf.WriteUnsignedInt((uint)ByteBufferUtil.SwapInt((int)value));
            }

            return this;
        }

        public IByteBuffer WriteLong(long value)
        {
            this.buf.WriteLong(ByteBufferUtil.SwapLong(value));
            return this;
        }

        public IByteBuffer WriteChar(char value)
        {
            this.WriteShort(value);
            return this;
        }

        public IByteBuffer WriteDouble(double value)
        {
            this.WriteLong(BitConverter.DoubleToInt64Bits(value));
            return this;
        }

        public IByteBuffer WriteBytes(IByteBuffer src)
        {
            this.buf.WriteBytes(src);
            return this;
        }

        public IByteBuffer WriteBytes(IByteBuffer src, int length)
        {
            this.buf.WriteBytes(src, length);
            return this;
        }

        public IByteBuffer WriteBytes(IByteBuffer src, int srcIndex, int length)
        {
            this.buf.WriteBytes(src, srcIndex, length);
            return this;
        }

        public IByteBuffer WriteBytes(byte[] src)
        {
            this.buf.WriteBytes(src);
            return this;
        }

        public IByteBuffer WriteBytes(byte[] src, int srcIndex, int length)
        {
            this.buf.WriteBytes(src, srcIndex, length);
            return this;
        }

        public bool HasArray
        {
            get { return this.buf.HasArray; }
        }

        public byte[] Array
        {
            get { return this.buf.Array; }
        }

        public byte[] ToArray()
        {
            return this.buf.ToArray().Reverse().ToArray();
        }

        public IByteBuffer Duplicate()
        {
            return this.buf.Duplicate().WithOrder(this.Order);
        }

        public IByteBuffer Unwrap()
        {
            return this.buf.Unwrap();
        }

        public ByteOrder Order
        {
            get { return this.order; }
        }

        public IByteBuffer WithOrder(ByteOrder endianness)
        {
            if (endianness == this.Order)
            {
                return this;
            }
            return this.buf;
        }

        public IByteBuffer Copy()
        {
            return this.buf.Copy().WithOrder(this.Order);
        }

        public IByteBuffer Copy(int index, int length)
        {
            return this.buf.Copy(index, length).WithOrder(this.Order);
        }

        public IByteBuffer Slice()
        {
            return this.buf.Slice().WithOrder(this.Order);
        }

        public IByteBuffer Slice(int index, int length)
        {
            return this.buf.Slice(index, length).WithOrder(this.Order);
        }

        public int ArrayOffset
        {
            get { return this.buf.ArrayOffset; }
        }

        public IByteBuffer ReadSlice(int length)
        {
            return this.buf.ReadSlice(length).WithOrder(this.Order);
        }

        public Task WriteBytesAsync(Stream stream, int length)
        {
            return this.buf.WriteBytesAsync(stream, length);
        }

        public Task WriteBytesAsync(Stream stream, int length, CancellationToken cancellationToken)
        {
            return this.buf.WriteBytesAsync(stream, length, cancellationToken);
        }

        public int ForEachByte(ByteProcessor processor)
        {
            return this.buf.ForEachByte(processor);
        }

        public int ForEachByte(int index, int length, ByteProcessor processor)
        {
            return this.buf.ForEachByte(index, length, processor);
        }

        public int ForEachByteDesc(ByteProcessor processor)
        {
            return this.buf.ForEachByteDesc(processor);
        }

        public int ForEachByteDesc(int index, int length, ByteProcessor processor)
        {
            return this.buf.ForEachByteDesc(index, length, processor);
        }

        public override string ToString()
        {
            return "Swapped(" + this.buf + ")";
        }

        public string ToString(Encoding encoding)
        {
            return buf.ToString(encoding);
        }

        public string ToString(int index, int length, Encoding encoding)
        {
            return buf.ToString(index, length, encoding);
        }
    }
}