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
        static readonly ArraySegment<byte> EmptyBuffer = new ArraySegment<byte>(ArrayExtensions.ZeroBytes);
        static readonly ArraySegment<byte>[] EmptyBuffers = { EmptyBuffer };

        readonly string str;
        EmptyByteBuffer swapped;

        public EmptyByteBuffer(IByteBufferAllocator allocator)
            : this(allocator, ByteOrder.BigEndian)
        {
        }

        EmptyByteBuffer(IByteBufferAllocator allocator, ByteOrder order)
        {
            Contract.Requires(allocator != null);

            this.Allocator = allocator;
            this.Order = order;
            this.str = this.GetType().Name + (order == ByteOrder.BigEndian ? "BE" : "LE");
        }

        public int Capacity => 0;

        public IByteBuffer AdjustCapacity(int newCapacity)
        {
            throw new NotSupportedException();
        }

        public int MaxCapacity => 0;

        public IByteBufferAllocator Allocator { get; }

        public int ReaderIndex => 0;

        public int WriterIndex => 0;

        public IByteBuffer SetWriterIndex(int writerIndex) => this.CheckIndex(writerIndex);

        public IByteBuffer SetReaderIndex(int readerIndex) => this.CheckIndex(readerIndex);

        public IByteBuffer SetIndex(int readerIndex, int writerIndex)
        {
            this.CheckIndex(readerIndex);
            return this.CheckIndex(writerIndex);
        }

        public int ReadableBytes => 0;

        public int WritableBytes => 0;

        public int MaxWritableBytes => 0;

        public bool IsReadable() => false;

        public bool IsReadable(int size) => false;

        public bool IsWritable() => false;

        public bool IsWritable(int size) => false;

        public IByteBuffer Clear() => this;

        public IByteBuffer MarkReaderIndex() => this;

        public IByteBuffer ResetReaderIndex() => this;

        public IByteBuffer MarkWriterIndex() => this;

        public IByteBuffer ResetWriterIndex() => this;

        public IByteBuffer DiscardReadBytes() => this;

        public IByteBuffer DiscardSomeReadBytes() => this;

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

        public float GetFloat(int index)
        {
            throw new IndexOutOfRangeException();
        }

        public double GetDouble(int index)
        {
            throw new IndexOutOfRangeException();
        }

        public int GetMedium(int index)
        {
            throw new IndexOutOfRangeException();
        }

        public int GetUnsignedMedium(int index)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer GetBytes(int index, IByteBuffer destination) => this.CheckIndex(index, destination.WritableBytes);

        public IByteBuffer GetBytes(int index, IByteBuffer destination, int length) => this.CheckIndex(index, length);

        public IByteBuffer GetBytes(int index, IByteBuffer destination, int dstIndex, int length) => this.CheckIndex(index, length);

        public IByteBuffer GetBytes(int index, byte[] destination) => this.CheckIndex(index, destination.Length);

        public IByteBuffer GetBytes(int index, byte[] destination, int dstIndex, int length) => this.CheckIndex(index, length);

        public IByteBuffer GetBytes(int index, Stream destination, int length) => this.CheckIndex(index, length);

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

        public IByteBuffer SetMedium(int index, int value)
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

        public IByteBuffer SetFloat(int index, float value)
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

        public IByteBuffer SetBytes(int index, IByteBuffer src, int length) => this.CheckIndex(index, length);

        public IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length) => this.CheckIndex(index, length);

        public IByteBuffer SetBytes(int index, byte[] src) => this.CheckIndex(index, src.Length);

        public IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length) => this.CheckIndex(index, length);

        public Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            this.CheckIndex(index, length);
            return TaskEx.Zero;
        }

        public IByteBuffer SetZero(int index, int length) => this.CheckIndex(index, length);

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

        public int ReadMedium()
        {
            throw new IndexOutOfRangeException();
        }

        public int ReadUnsignedMedium()
        {
            throw new IndexOutOfRangeException();
        }

        public char ReadChar()
        {
            throw new IndexOutOfRangeException();
        }

        public float ReadFloat()
        {
            throw new IndexOutOfRangeException();
        }

        public double ReadDouble()
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer ReadBytes(int length) => this.CheckLength(length);

        public IByteBuffer ReadBytes(IByteBuffer destination) => this.CheckLength(destination.WritableBytes);

        public IByteBuffer ReadBytes(IByteBuffer destination, int length) => this.CheckLength(length);

        public IByteBuffer ReadBytes(IByteBuffer destination, int dstIndex, int length) => this.CheckLength(length);

        public IByteBuffer ReadBytes(byte[] destination) => this.CheckLength(destination.Length);

        public IByteBuffer ReadBytes(byte[] destination, int dstIndex, int length) => this.CheckLength(length);

        public IByteBuffer ReadBytes(Stream destination, int length) => this.CheckLength(length);

        public IByteBuffer SkipBytes(int length) => this.CheckLength(length);

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

        public IByteBuffer WriteUnsignedMedium(int value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer WriteMedium(int value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer WriteChar(char value)
        {
            throw new IndexOutOfRangeException();
        }

        public IByteBuffer WriteFloat(float value)
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

        public IByteBuffer WriteBytes(IByteBuffer src, int length) => this.CheckLength(length);

        public IByteBuffer WriteBytes(IByteBuffer src, int srcIndex, int length) => this.CheckLength(length);

        public IByteBuffer WriteBytes(byte[] src) => this.CheckLength(src.Length);

        public IByteBuffer WriteBytes(byte[] src, int srcIndex, int length) => this.CheckLength(length);

        public int IoBufferCount => 1;

        public ArraySegment<byte> GetIoBuffer() => EmptyBuffer;

        public ArraySegment<byte> GetIoBuffer(int index, int length)
        {
            this.CheckIndex(index, length);
            return this.GetIoBuffer();
        }

        public ArraySegment<byte>[] GetIoBuffers() => EmptyBuffers;

        public ArraySegment<byte>[] GetIoBuffers(int index, int length)
        {
            this.CheckIndex(index, length);
            return this.GetIoBuffers();
        }

        public bool HasArray => true;

        public byte[] Array => ArrayExtensions.ZeroBytes;

        public byte[] ToArray() => ArrayExtensions.ZeroBytes;

        public IByteBuffer Duplicate() => this;

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

        public IByteBuffer Copy() => this;

        public IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            return this;
        }

        public IByteBuffer Slice() => this;

        public IByteBuffer Slice(int index, int length) => this.CheckIndex(index, length);

        public int ArrayOffset => 0;

        public IByteBuffer ReadSlice(int length) => this.CheckLength(length);

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

        public IByteBuffer WriteZero(int length) => this.CheckLength(length);

        public IByteBuffer Unwrap() => null;

        public ByteOrder Order { get; }

        public int ReferenceCount => 1;

        public IReferenceCounted Retain() => this;

        public IReferenceCounted Retain(int increment) => this;

        public IReferenceCounted Touch() => this;

        public IReferenceCounted Touch(object hint) => this;

        public bool Release() => false;

        public bool Release(int decrement) => false;

        public override int GetHashCode() => 0;

        public override bool Equals(object obj)
        {
            var buffer = obj as IByteBuffer;
            return this.Equals(buffer);
        }

        public bool Equals(IByteBuffer buffer) => buffer != null && !buffer.IsReadable();

        public int CompareTo(IByteBuffer buffer) => buffer.IsReadable() ? -1 : 0;

        public override string ToString() => this.str;

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

        public int ForEachByte(ByteProcessor processor) => -1;

        public int ForEachByte(int index, int length, ByteProcessor processor)
        {
            this.CheckIndex(index, length);
            return -1;
        }

        public int ForEachByteDesc(ByteProcessor processor) => -1;

        public int ForEachByteDesc(int index, int length, ByteProcessor processor)
        {
            this.CheckIndex(index, length);
            return -1;
        }

        public string ToString(Encoding encoding) => string.Empty;

        public string ToString(int index, int length, Encoding encoding)
        {
            this.CheckIndex(index, length);
            return this.ToString(encoding);
        }
    }
}