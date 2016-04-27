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

    /// <summary>
    ///     Represents an empty byte buffer
    /// </summary>
    public sealed class EmptyByteBuffer : IByteBuffer
    {
        readonly IByteBufferAllocator allocator;
        readonly ByteOrder order;
        readonly string str;
        EmptyByteBuffer swapped;

        public EmptyByteBuffer(IByteBufferAllocator allocator)
            : this(allocator, ByteOrder.BigEndian)
        {
        }

        EmptyByteBuffer(IByteBufferAllocator allocator, ByteOrder order)
        {
            Contract.Requires(allocator != null);

            this.allocator = allocator;
            this.order = order;
            this.str = this.GetType().Name + (order == ByteOrder.BigEndian ? "BE" : "LE");
        }

        public int Capacity
        {
            get { return 0; }
        }

        public IByteBuffer AdjustCapacity(int newCapacity)
        {
            throw new NotSupportedException();
        }

        public int MaxCapacity
        {
            get { return 0; }
        }

        public IByteBufferAllocator Allocator
        {
            get { return this.allocator; }
        }

        public int ReaderIndex
        {
            get { return 0; }
        }

        public int WriterIndex
        {
            get { return 0; }
        }

        public IByteBuffer SetWriterIndex(int writerIndex)
        {
            return this.CheckIndex(writerIndex);
        }

        public IByteBuffer SetReaderIndex(int readerIndex)
        {
            return this.CheckIndex(readerIndex);
        }

        public IByteBuffer SetIndex(int readerIndex, int writerIndex)
        {
            this.CheckIndex(readerIndex);
            return this.CheckIndex(writerIndex);
        }

        public int ReadableBytes
        {
            get { return 0; }
        }

        public int WritableBytes
        {
            get { return 0; }
        }

        public int MaxWritableBytes
        {
            get { return 0; }
        }

        public bool IsReadable()
        {
            return false;
        }

        public bool IsReadable(int size)
        {
            return false;
        }

        public bool IsWritable()
        {
            return false;
        }

        public bool IsWritable(int size)
        {
            return false;
        }

        public IByteBuffer Clear()
        {
            return this;
        }

        public IByteBuffer MarkReaderIndex()
        {
            return this;
        }

        public IByteBuffer ResetReaderIndex()
        {
            return this;
        }

        public IByteBuffer MarkWriterIndex()
        {
            return this;
        }

        public IByteBuffer ResetWriterIndex()
        {
            return this;
        }

        public IByteBuffer DiscardReadBytes()
        {
            return this;
        }

        public IByteBuffer DiscardSomeReadBytes()
        {
            return this;
        }

        public IByteBuffer EnsureWritable(int minWritableBytes)
        {
            Contract.Requires(minWritableBytes >= 0);

            if (minWritableBytes != 0)
            {
                throw new IndexOutOfRangeException();
            }
            return this;
        }

        public int EnsureWritable(int minWritableBytes, bool force)
        {
            Contract.Requires(minWritableBytes >= 0);

            if (minWritableBytes == 0)
            {
                return 0;
            }

            return 1;
        }

        public bool GetBoolean(int index)
        {
            throw new IndexOutOfRangeException();
        }

        public byte GetByte(int index)
        {
            throw new IndexOutOfRangeException();
        }

        public short GetShort(int index)
        {
            throw new IndexOutOfRangeException();
        }

        public ushort GetUnsignedShort(int index)
        {
            throw new IndexOutOfRangeException();
        }

        public int GetInt(int index)
        {
            throw new IndexOutOfRangeException();
        }

        public uint GetUnsignedInt(int index)
        {
            throw new IndexOutOfRangeException();
        }

        public long GetLong(int index)
        {
            throw new IndexOutOfRangeException();
        }

        public char GetChar(int index)
        {
            throw new IndexOutOfRangeException();
        }

        public double GetDouble(int index)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer GetBytes(int index, IByteBuffer destination)
        {
            return this.CheckIndex(index, destination.WritableBytes);
        }

        public IByteBuffer GetBytes(int index, IByteBuffer destination, int length)
        {
            return this.CheckIndex(index, length);
        }

        public IByteBuffer GetBytes(int index, IByteBuffer destination, int dstIndex, int length)
        {
            return this.CheckIndex(index, length);
        }

        public IByteBuffer GetBytes(int index, byte[] destination)
        {
            return this.CheckIndex(index, destination.Length);
        }

        public IByteBuffer GetBytes(int index, byte[] destination, int dstIndex, int length)
        {
            return this.CheckIndex(index, length);
        }

        public IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            return this.CheckIndex(index, length);
        }

        public IByteBuffer SetBoolean(int index, bool value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer SetByte(int index, int value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer SetShort(int index, int value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer SetUnsignedShort(int index, ushort value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer SetInt(int index, int value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer SetUnsignedInt(int index, uint value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer SetLong(int index, long value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer SetChar(int index, char value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer SetDouble(int index, double value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer SetBytes(int index, IByteBuffer src)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer SetBytes(int index, IByteBuffer src, int length)
        {
            return this.CheckIndex(index, length);
        }

        public IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            return this.CheckIndex(index, length);
        }

        public IByteBuffer SetBytes(int index, byte[] src)
        {
            return this.CheckIndex(index, src.Length);
        }

        public IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            return this.CheckIndex(index, length);
        }

        public Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            this.CheckIndex(index, length);
            return TaskEx.Zero;
        }

        public bool ReadBoolean()
        {
            throw new IndexOutOfRangeException();
        }

        public byte ReadByte()
        {
            throw new IndexOutOfRangeException();
        }

        public short ReadShort()
        {
            throw new IndexOutOfRangeException();
        }

        public ushort ReadUnsignedShort()
        {
            throw new IndexOutOfRangeException();
        }

        public int ReadInt()
        {
            throw new IndexOutOfRangeException();
        }

        public uint ReadUnsignedInt()
        {
            throw new IndexOutOfRangeException();
        }

        public long ReadLong()
        {
            throw new IndexOutOfRangeException();
        }

        public char ReadChar()
        {
            throw new IndexOutOfRangeException();
        }

        public double ReadDouble()
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer ReadBytes(int length)
        {
            return this.CheckLength(length);
        }

        public IByteBuffer ReadBytes(IByteBuffer destination)
        {
            return this.CheckLength(destination.WritableBytes);
        }

        public IByteBuffer ReadBytes(IByteBuffer destination, int length)
        {
            return this.CheckLength(length);
        }

        public IByteBuffer ReadBytes(IByteBuffer destination, int dstIndex, int length)
        {
            return this.CheckLength(length);
        }

        public IByteBuffer ReadBytes(byte[] destination)
        {
            return this.CheckLength(destination.Length);
        }

        public IByteBuffer ReadBytes(byte[] destination, int dstIndex, int length)
        {
            return this.CheckLength(length);
        }

        public IByteBuffer ReadBytes(Stream destination, int length)
        {
            return this.CheckLength(length);
        }

        public IByteBuffer SkipBytes(int length)
        {
            return this.CheckLength(length);
        }

        public IByteBuffer WriteBoolean(bool value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer WriteByte(int value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer WriteShort(int value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer WriteUnsignedShort(ushort value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer WriteInt(int value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer WriteUnsignedInt(uint value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer WriteLong(long value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer WriteChar(char value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer WriteDouble(double value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer WriteBytes(IByteBuffer src)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer WriteBytes(IByteBuffer src, int length)
        {
            return this.CheckLength(length);
        }

        public IByteBuffer WriteBytes(IByteBuffer src, int srcIndex, int length)
        {
            return this.CheckLength(length);
        }

        public IByteBuffer WriteBytes(byte[] src)
        {
            return this.CheckLength(src.Length);
        }

        public IByteBuffer WriteBytes(byte[] src, int srcIndex, int length)
        {
            return this.CheckLength(length);
        }

        public bool HasArray
        {
            get { return true; }
        }

        public byte[] Array
        {
            get { return ByteArrayExtensions.Empty; }
        }

        public byte[] ToArray()
        {
            return ByteArrayExtensions.Empty;
        }

        public IByteBuffer Duplicate()
        {
            return this;
        }

        public IByteBuffer WithOrder(ByteOrder endianness)
        {
            if (endianness == this.Order)
            {
                return this;
            }

            EmptyByteBuffer s = this.swapped;
            if (s != null)
            {
                return s;
            }

            this.swapped = s = new EmptyByteBuffer(this.Allocator, endianness);
            return s;
        }

        public IByteBuffer Copy()
        {
            return this;
        }

        public IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            return this;
        }

        public IByteBuffer Slice()
        {
            return this;
        }

        public IByteBuffer Slice(int index, int length)
        {
            return this.CheckIndex(index, length);
        }

        public int ArrayOffset
        {
            get { return 0; }
        }

        public IByteBuffer ReadSlice(int length)
        {
            return this.CheckLength(length);
        }

        public Task WriteBytesAsync(Stream stream, int length)
        {
            this.CheckLength(length);
            return TaskEx.Completed;
        }

        public Task WriteBytesAsync(Stream stream, int length, CancellationToken cancellationToken)
        {
            this.CheckLength(length);
            return TaskEx.Completed;
        }

        public IByteBuffer Unwrap()
        {
            return null;
        }

        public ByteOrder Order
        {
            get { return this.order; }
        }

        public int ReferenceCount
        {
            get { return 1; }
        }

        public IReferenceCounted Retain()
        {
            return this;
        }

        public IReferenceCounted Retain(int increment)
        {
            return this;
        }

        public IReferenceCounted Touch()
        {
            return this;
        }

        public IReferenceCounted Touch(object hint)
        {
            return this;
        }

        public bool Release()
        {
            return false;
        }

        public bool Release(int decrement)
        {
            return false;
        }

        public override string ToString()
        {
            return this.str;
        }

        IByteBuffer CheckIndex(int index)
        {
            if (index != 0)
            {
                throw new IndexOutOfRangeException();
            }
            return this;
        }

        IByteBuffer CheckIndex(int index, int length)
        {
            if (length < 0)
            {
                throw new ArgumentException("length: " + length);
            }
            if (index != 0 || length != 0)
            {
                throw new IndexOutOfRangeException();
            }
            return this;
        }

        IByteBuffer CheckLength(int length)
        {
            if (length < 0)
            {
                throw new ArgumentException("length: " + length + " (expected: >= 0)");
            }
            if (length != 0)
            {
                throw new IndexOutOfRangeException();
            }
            return this;
        }

        public int ForEachByte(ByteProcessor processor)
        {
            return -1;
        }

        public int ForEachByte(int index, int length, ByteProcessor processor)
        {
            CheckIndex(index, length);
            return -1;
        }

        public int ForEachByteDesc(ByteProcessor processor)
        {
            return -1;
        }

        public int ForEachByteDesc(int index, int length, ByteProcessor processor)
        {
            CheckIndex(index, length);
            return -1;
        }

        public string ToString(Encoding encoding)
        {
            return string.Empty;
        }

        public string ToString(int index, int length, Encoding encoding)
        {
            CheckIndex(index, length);
            return this.ToString(encoding);
        }
    }
}