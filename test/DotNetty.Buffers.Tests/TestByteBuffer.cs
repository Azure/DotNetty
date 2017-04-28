// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;
    class TestByteBuffer : IByteBuffer
    {
        public int ReadableBytes => 100;

        public IByteBuffer ReadBytes(byte[] destination, int dstIndex, int length)
        {
            destination.Fill(dstIndex, length, (byte)42);
            return this;
        }

        public int Capacity => throw new NotImplementedException();

        public int MaxCapacity => throw new NotImplementedException();

        public IByteBufferAllocator Allocator => throw new NotImplementedException();

        public int ReaderIndex => throw new NotImplementedException();

        public int WriterIndex => throw new NotImplementedException();

        public int WritableBytes => throw new NotImplementedException();

        public int MaxWritableBytes => throw new NotImplementedException();

        public int IoBufferCount => throw new NotImplementedException();

        public bool HasArray => throw new NotImplementedException();

        public byte[] Array => throw new NotImplementedException();

        public ByteOrder Order => throw new NotImplementedException();

        public int ArrayOffset => throw new NotImplementedException();

        public int ReferenceCount => throw new NotImplementedException();

        public IByteBuffer AdjustCapacity(int newCapacity)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer Clear()
        {
            throw new NotImplementedException();
        }

        public int CompareTo(IByteBuffer other)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer Copy()
        {
            throw new NotImplementedException();
        }

        public IByteBuffer Copy(int index, int length)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer DiscardReadBytes()
        {
            throw new NotImplementedException();
        }

        public IByteBuffer DiscardSomeReadBytes()
        {
            throw new NotImplementedException();
        }

        public IByteBuffer Duplicate()
        {
            throw new NotImplementedException();
        }

        public IByteBuffer EnsureWritable(int minWritableBytes)
        {
            throw new NotImplementedException();
        }

        public int EnsureWritable(int minWritableBytes, bool force)
        {
            throw new NotImplementedException();
        }

        public bool Equals(IByteBuffer other)
        {
            throw new NotImplementedException();
        }

        public int ForEachByte(ByteProcessor processor)
        {
            throw new NotImplementedException();
        }

        public int ForEachByte(int index, int length, ByteProcessor processor)
        {
            throw new NotImplementedException();
        }

        public int ForEachByteDesc(ByteProcessor processor)
        {
            throw new NotImplementedException();
        }

        public int ForEachByteDesc(int index, int length, ByteProcessor processor)
        {
            throw new NotImplementedException();
        }

        public bool GetBoolean(int index)
        {
            throw new NotImplementedException();
        }

        public byte GetByte(int index)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer GetBytes(int index, IByteBuffer destination)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer GetBytes(int index, IByteBuffer destination, int length)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer GetBytes(int index, IByteBuffer destination, int dstIndex, int length)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer GetBytes(int index, byte[] destination)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer GetBytes(int index, byte[] destination, int dstIndex, int length)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            throw new NotImplementedException();
        }

        public char GetChar(int index)
        {
            throw new NotImplementedException();
        }

        public double GetDouble(int index)
        {
            throw new NotImplementedException();
        }

        public int GetInt(int index)
        {
            throw new NotImplementedException();
        }

        public ArraySegment<byte> GetIoBuffer()
        {
            throw new NotImplementedException();
        }

        public ArraySegment<byte> GetIoBuffer(int index, int length)
        {
            throw new NotImplementedException();
        }

        public ArraySegment<byte>[] GetIoBuffers()
        {
            throw new NotImplementedException();
        }

        public ArraySegment<byte>[] GetIoBuffers(int index, int length)
        {
            throw new NotImplementedException();
        }

        public long GetLong(int index)
        {
            throw new NotImplementedException();
        }

        public short GetShort(int index)
        {
            throw new NotImplementedException();
        }

        public uint GetUnsignedInt(int index)
        {
            throw new NotImplementedException();
        }

        public ushort GetUnsignedShort(int index)
        {
            throw new NotImplementedException();
        }

        public bool IsReadable()
        {
            throw new NotImplementedException();
        }

        public bool IsReadable(int size)
        {
            throw new NotImplementedException();
        }

        public bool IsWritable()
        {
            throw new NotImplementedException();
        }

        public bool IsWritable(int size)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer MarkReaderIndex()
        {
            throw new NotImplementedException();
        }

        public IByteBuffer MarkWriterIndex()
        {
            throw new NotImplementedException();
        }

        public bool ReadBoolean()
        {
            throw new NotImplementedException();
        }

        public byte ReadByte()
        {
            throw new NotImplementedException();
        }

        public IByteBuffer ReadBytes(int length)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer ReadBytes(IByteBuffer destination)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer ReadBytes(IByteBuffer destination, int length)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer ReadBytes(IByteBuffer destination, int dstIndex, int length)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer ReadBytes(byte[] destination)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer ReadBytes(Stream destination, int length)
        {
            throw new NotImplementedException();
        }

        public char ReadChar()
        {
            throw new NotImplementedException();
        }

        public double ReadDouble()
        {
            throw new NotImplementedException();
        }

        public int ReadInt()
        {
            throw new NotImplementedException();
        }

        public long ReadLong()
        {
            throw new NotImplementedException();
        }

        public short ReadShort()
        {
            throw new NotImplementedException();
        }

        public IByteBuffer ReadSlice(int length)
        {
            throw new NotImplementedException();
        }

        public uint ReadUnsignedInt()
        {
            throw new NotImplementedException();
        }

        public ushort ReadUnsignedShort()
        {
            throw new NotImplementedException();
        }

        public bool Release()
        {
            throw new NotImplementedException();
        }

        public bool Release(int decrement)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer ResetReaderIndex()
        {
            throw new NotImplementedException();
        }

        public IByteBuffer ResetWriterIndex()
        {
            throw new NotImplementedException();
        }

        public IReferenceCounted Retain()
        {
            throw new NotImplementedException();
        }

        public IReferenceCounted Retain(int increment)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer SetBoolean(int index, bool value)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer SetByte(int index, int value)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer SetBytes(int index, IByteBuffer src)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer SetBytes(int index, IByteBuffer src, int length)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer SetBytes(int index, byte[] src)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            throw new NotImplementedException();
        }

        public Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer SetChar(int index, char value)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer SetDouble(int index, double value)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer SetIndex(int readerIndex, int writerIndex)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer SetInt(int index, int value)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer SetLong(int index, long value)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer SetReaderIndex(int readerIndex)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer SetShort(int index, int value)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer SetUnsignedInt(int index, uint value)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer SetUnsignedShort(int index, ushort value)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer SetWriterIndex(int writerIndex)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer SkipBytes(int length)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer Slice()
        {
            throw new NotImplementedException();
        }

        public IByteBuffer Slice(int index, int length)
        {
            throw new NotImplementedException();
        }

        public byte[] ToArray()
        {
            throw new NotImplementedException();
        }

        public string ToString(Encoding encoding)
        {
            throw new NotImplementedException();
        }

        public string ToString(int index, int length, Encoding encoding)
        {
            throw new NotImplementedException();
        }

        public IReferenceCounted Touch()
        {
            throw new NotImplementedException();
        }

        public IReferenceCounted Touch(object hint)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer Unwrap()
        {
            throw new NotImplementedException();
        }

        public IByteBuffer WithOrder(ByteOrder order)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer WriteBoolean(bool value)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer WriteByte(int value)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer WriteBytes(IByteBuffer src)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer WriteBytes(IByteBuffer src, int length)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer WriteBytes(IByteBuffer src, int srcIndex, int length)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer WriteBytes(byte[] src)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer WriteBytes(byte[] src, int srcIndex, int length)
        {
            throw new NotImplementedException();
        }

        public Task WriteBytesAsync(Stream stream, int length)
        {
            throw new NotImplementedException();
        }

        public Task WriteBytesAsync(Stream stream, int length, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer WriteChar(char value)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer WriteDouble(double value)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer WriteInt(int value)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer WriteLong(long value)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer WriteShort(int value)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer WriteUnsignedInt(uint value)
        {
            throw new NotImplementedException();
        }

        public IByteBuffer WriteUnsignedShort(ushort value)
        {
            throw new NotImplementedException();
        }
    }
}