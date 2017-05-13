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
    using System.Runtime.CompilerServices;

    /// <summary>
    ///     Wrapper which swaps the <see cref="ByteOrder" /> of a <see cref="IByteBuffer" />.
    /// </summary>
    public class SwappedByteBuffer : IByteBuffer
    {
        readonly IByteBuffer buf;

        public SwappedByteBuffer(IByteBuffer buf)
        {
            Contract.Requires(buf != null);

            this.buf = buf;
            if (buf.Order == ByteOrder.BigEndian)
            {
                this.Order = ByteOrder.LittleEndian;
            }
            else
            {
                this.Order = ByteOrder.BigEndian;
            }
        }

        public int ReferenceCount => this.buf.ReferenceCount;

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

        public bool Release() => this.buf.Release();

        public bool Release(int decrement) => this.buf.Release(decrement);

        public int Capacity => this.buf.Capacity;

        public IByteBuffer AdjustCapacity(int newCapacity) => this.buf.AdjustCapacity(newCapacity);

        public int MaxCapacity => this.buf.MaxCapacity;

        public IByteBufferAllocator Allocator => this.buf.Allocator;

        public int ReaderIndex => this.buf.ReaderIndex;

        public int WriterIndex => this.buf.WriterIndex;

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

        public int ReadableBytes => this.buf.ReadableBytes;

        public int WritableBytes => this.buf.WritableBytes;

        public int MaxWritableBytes => this.buf.MaxWritableBytes;

        public bool IsReadable() => this.buf.IsReadable();

        public bool IsReadable(int size) => this.buf.IsReadable(size);

        public bool IsWritable() => this.buf.IsWritable();

        public bool IsWritable(int size) => this.buf.IsWritable(size);

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
            this.buf.DiscardSomeReadBytes();
            return this;
        }

        public IByteBuffer EnsureWritable(int minWritableBytes)
        {
            this.buf.EnsureWritable(minWritableBytes);
            return this;
        }

        public int EnsureWritable(int minWritableBytes, bool force) => this.buf.EnsureWritable(minWritableBytes, force);

        public bool GetBoolean(int index) => this.buf.GetBoolean(index);

        public byte GetByte(int index) => this.buf.GetByte(index);

        public short GetShort(int index) => ByteBufferUtil.SwapShort(this.buf.GetShort(index));

        public ushort GetUnsignedShort(int index)
        {
            unchecked
            {
                return (ushort)(this.GetShort(index));
            }
        }

        public int GetInt(int index) => ByteBufferUtil.SwapInt(this.buf.GetInt(index));

        public uint GetUnsignedInt(int index)
        {
            unchecked
            {
                return (uint)this.GetInt(index);
            }
        }

        public long GetLong(int index) => ByteBufferUtil.SwapLong(this.buf.GetLong(index));

        public int GetMedium(int index) => ByteBufferUtil.SwapMedium(this.buf.GetMedium(index));

        public int GetUnsignedMedium(int index) => this.GetMedium(index).ToUnsignedMediumInt();

        public char GetChar(int index) => (char)this.GetShort(index);

        public float GetFloat(int index) => ByteBufferUtil.Int32BitsToSingle(this.GetInt(index));

        public double GetDouble(int index) => BitConverter.Int64BitsToDouble(this.GetLong(index));

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

        public IByteBuffer SetMedium(int index, int value)
        {
            this.buf.SetMedium(index, ByteBufferUtil.SwapMedium(value));
            return this;
        }

        public IByteBuffer SetChar(int index, char value)
        {
            this.SetShort(index, (short)value);
            return this;
        }

        public IByteBuffer SetFloat(int index, float value)
        {
            this.SetInt(index, ByteBufferUtil.SingleToInt32Bits(value));
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

        public IByteBuffer SetZero(int index, int length)
        {
            this.buf.SetZero(index, length);
            return this;
        }

        public Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken) => this.buf.SetBytesAsync(index, src, length, cancellationToken);

        public bool ReadBoolean() => this.buf.ReadBoolean();

        public byte ReadByte() => this.buf.ReadByte();

        public short ReadShort() => ByteBufferUtil.SwapShort(this.buf.ReadShort());

        public ushort ReadUnsignedShort()
        {
            unchecked
            {
                return (ushort)this.ReadShort();
            }
        }

        public int ReadInt() => ByteBufferUtil.SwapInt(this.buf.ReadInt());

        public uint ReadUnsignedInt()
        {
            unchecked
            {
                return (uint)this.ReadInt();
            }
        }

        public long ReadLong() => ByteBufferUtil.SwapLong(this.buf.ReadLong());

        public int ReadMedium() => ByteBufferUtil.SwapMedium(this.buf.ReadMedium());

        public int ReadUnsignedMedium() => this.ReadMedium().ToUnsignedMediumInt();

        public char ReadChar() => (char)this.ReadShort();

        public float ReadFloat() => ByteBufferUtil.Int32BitsToSingle(this.ReadInt());

        public double ReadDouble() => BitConverter.Int64BitsToDouble(this.ReadLong());

        public IByteBuffer ReadBytes(int length) => this.buf.ReadBytes(length).WithOrder(this.Order);

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

        public IByteBuffer WriteUnsignedMedium(int value)
        {
            this.buf.WriteMedium(ByteBufferUtil.SwapMedium(value.ToUnsignedMediumInt()));
            return this;
        }

        public IByteBuffer WriteMedium(int value)
        {
            this.buf.WriteMedium(ByteBufferUtil.SwapMedium(value));
            return this;
        }

        public IByteBuffer WriteChar(char value)
        {
            this.WriteShort(value);
            return this;
        }

        public IByteBuffer WriteFloat(float value)
        {
            this.WriteInt(ByteBufferUtil.SingleToInt32Bits(value));
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

        public int IoBufferCount => this.buf.IoBufferCount;

        public ArraySegment<byte> GetIoBuffer() => this.buf.GetIoBuffer();

        public ArraySegment<byte> GetIoBuffer(int index, int length) => this.buf.GetIoBuffer(index, length);

        public ArraySegment<byte>[] GetIoBuffers() => this.buf.GetIoBuffers();

        public ArraySegment<byte>[] GetIoBuffers(int index, int length) => this.buf.GetIoBuffers(index, length);

        public bool HasArray => this.buf.HasArray;

        public byte[] Array => this.buf.Array;

        public byte[] ToArray() => this.buf.ToArray();

        public IByteBuffer Duplicate() => this.buf.Duplicate().WithOrder(this.Order);

        public IByteBuffer Unwrap() => this.buf.Unwrap();

        public ByteOrder Order { get; }

        public IByteBuffer WithOrder(ByteOrder endianness)
        {
            if (endianness == this.Order)
            {
                return this;
            }
            return this.buf;
        }

        public IByteBuffer Copy() => this.buf.Copy().WithOrder(this.Order);

        public IByteBuffer Copy(int index, int length) => this.buf.Copy(index, length).WithOrder(this.Order);

        public IByteBuffer Slice() => this.buf.Slice().WithOrder(this.Order);

        public IByteBuffer Slice(int index, int length) => this.buf.Slice(index, length).WithOrder(this.Order);

        public int ArrayOffset => this.buf.ArrayOffset;

        public IByteBuffer ReadSlice(int length) => this.buf.ReadSlice(length).WithOrder(this.Order);

        public Task WriteBytesAsync(Stream stream, int length) => this.buf.WriteBytesAsync(stream, length);

        public Task WriteBytesAsync(Stream stream, int length, CancellationToken cancellationToken) => this.buf.WriteBytesAsync(stream, length, cancellationToken);

        public IByteBuffer WriteZero(int length)
        {
            this.buf.WriteZero(length);
            return this;
        }

        public int ForEachByte(ByteProcessor processor) => this.buf.ForEachByte(processor);

        public int ForEachByte(int index, int length, ByteProcessor processor) => this.buf.ForEachByte(index, length, processor);

        public int ForEachByteDesc(ByteProcessor processor) => this.buf.ForEachByteDesc(processor);

        public int ForEachByteDesc(int index, int length, ByteProcessor processor) => this.buf.ForEachByteDesc(index, length, processor);

        public override int GetHashCode() => this.buf.GetHashCode();

        public override bool Equals(object obj) => this.Equals(obj as IByteBuffer);

        public bool Equals(IByteBuffer buffer)
        {
            if (ReferenceEquals(this, buffer))
            {
                return true;
            }
            if (buffer != null)
            {
                return ByteBufferUtil.Equals(this, buffer);
            }
            return false;
        }

        public int CompareTo(IByteBuffer buffer) => ByteBufferUtil.Compare(this, buffer);

        public override string ToString() => "Swapped(" + this.buf + ")";

        public string ToString(Encoding encoding) => this.buf.ToString(encoding);

        public string ToString(int index, int length, Encoding encoding) => this.buf.ToString(index, length, encoding);
    }
}