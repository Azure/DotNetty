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

    /// <inheritdoc />
    /// <summary>
    ///     Represents an empty byte buffer
    /// </summary>
    public sealed class EmptyByteBuffer : IByteBuffer
    {
        static readonly ArraySegment<byte> EmptyBuffer = new ArraySegment<byte>(ArrayExtensions.ZeroBytes);
        static readonly ArraySegment<byte>[] EmptyBuffers = { EmptyBuffer };

        public EmptyByteBuffer(IByteBufferAllocator allocator)
        {
            Contract.Requires(allocator != null);

            this.Allocator = allocator;
        }

        public int Capacity => 0;

        public IByteBuffer AdjustCapacity(int newCapacity) =>throw new NotSupportedException();

        public int MaxCapacity => 0;

        public IByteBufferAllocator Allocator { get; }

        public IByteBuffer Unwrap() => null;

        public bool IsDirect => true;

        public int ReaderIndex => 0;

        public IByteBuffer SetReaderIndex(int readerIndex) => this.CheckIndex(readerIndex);

        public int WriterIndex => 0;

        public IByteBuffer SetWriterIndex(int writerIndex) => this.CheckIndex(writerIndex);

        public IByteBuffer SetIndex(int readerIndex, int writerIndex)
        {
            this.CheckIndex(readerIndex);
            this.CheckIndex(writerIndex);
            return this;
        }

        public int ReadableBytes => 0;

        public int WritableBytes => 0;

        public int MaxWritableBytes => 0;

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

        public bool GetBoolean(int index) => throw new IndexOutOfRangeException();

        public byte GetByte(int index) => throw new IndexOutOfRangeException();

        public short GetShort(int index) => throw new IndexOutOfRangeException();

        public short GetShortLE(int index) => throw new IndexOutOfRangeException();

        public ushort GetUnsignedShort(int index) => throw new IndexOutOfRangeException();

        public ushort GetUnsignedShortLE(int index) => throw new IndexOutOfRangeException();

        public int GetMedium(int index) => throw new IndexOutOfRangeException();

        public int GetMediumLE(int index) => throw new IndexOutOfRangeException();

        public int GetUnsignedMedium(int index) => throw new IndexOutOfRangeException();

        public int GetUnsignedMediumLE(int index) => throw new IndexOutOfRangeException();

        public int GetInt(int index) => throw new IndexOutOfRangeException();

        public int GetIntLE(int index) => throw new IndexOutOfRangeException();

        public uint GetUnsignedInt(int index) => throw new IndexOutOfRangeException();

        public uint GetUnsignedIntLE(int index) => throw new IndexOutOfRangeException();

        public long GetLong(int index) => throw new IndexOutOfRangeException();

        public long GetLongLE(int index) => throw new IndexOutOfRangeException();

        public char GetChar(int index) => throw new IndexOutOfRangeException();

        public float GetFloat(int index) => throw new IndexOutOfRangeException();

        public float GetFloatLE(int index) => throw new IndexOutOfRangeException();

        public double GetDouble(int index) => throw new IndexOutOfRangeException();

        public double GetDoubleLE(int index) => throw new IndexOutOfRangeException();

        public IByteBuffer GetBytes(int index, IByteBuffer destination) => this.CheckIndex(index, destination.WritableBytes);

        public IByteBuffer GetBytes(int index, IByteBuffer destination, int length) => this.CheckIndex(index, length);

        public IByteBuffer GetBytes(int index, IByteBuffer destination, int dstIndex, int length) => this.CheckIndex(index, length);

        public IByteBuffer GetBytes(int index, byte[] destination) => this.CheckIndex(index, destination.Length);

        public IByteBuffer GetBytes(int index, byte[] destination, int dstIndex, int length) => this.CheckIndex(index, length);

        public IByteBuffer GetBytes(int index, Stream destination, int length) => this.CheckIndex(index, length);

        public ICharSequence GetCharSequence(int index, int length, Encoding encoding)
        {
            this.CheckIndex(index, length);
            return null;
        }

        public string GetString(int index, int length, Encoding encoding)
        {
            this.CheckIndex(index, length);
            return null;
        }

        public IByteBuffer SetBoolean(int index, bool value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetByte(int index, int value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetShort(int index, int value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetShortLE(int index, int value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetUnsignedShort(int index, ushort value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetUnsignedShortLE(int index, ushort value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetMedium(int index, int value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetMediumLE(int index, int value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetInt(int index, int value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetIntLE(int index, int value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetUnsignedInt(int index, uint value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetUnsignedIntLE(int index, uint value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetLong(int index, long value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetLongLE(int index, long value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetChar(int index, char value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetFloat(int index, float value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetFloatLE(int index, float value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetDouble(int index, double value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetDoubleLE(int index, double value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetBytes(int index, IByteBuffer src) => throw new IndexOutOfRangeException();

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

        public int SetCharSequence(int index, ICharSequence sequence, Encoding encoding) => throw new IndexOutOfRangeException();

        public int SetString(int index, string value, Encoding encoding) => throw new IndexOutOfRangeException();

        public bool ReadBoolean() => throw new IndexOutOfRangeException();

        public byte ReadByte() => throw new IndexOutOfRangeException();

        public short ReadShort() => throw new IndexOutOfRangeException();

        public short ReadShortLE() => throw new IndexOutOfRangeException();

        public ushort ReadUnsignedShort() => throw new IndexOutOfRangeException();

        public ushort ReadUnsignedShortLE() => throw new IndexOutOfRangeException();

        public int ReadMedium() => throw new IndexOutOfRangeException();

        public int ReadMediumLE() => throw new IndexOutOfRangeException();

        public int ReadUnsignedMedium() => throw new IndexOutOfRangeException();

        public int ReadUnsignedMediumLE() => throw new IndexOutOfRangeException();

        public int ReadInt() => throw new IndexOutOfRangeException();

        public int ReadIntLE() => throw new IndexOutOfRangeException();

        public uint ReadUnsignedInt() => throw new IndexOutOfRangeException();

        public uint ReadUnsignedIntLE() => throw new IndexOutOfRangeException();

        public long ReadLong() => throw new IndexOutOfRangeException();

        public long ReadLongLE() => throw new IndexOutOfRangeException();

        public char ReadChar() => throw new IndexOutOfRangeException();

        public float ReadFloat() => throw new IndexOutOfRangeException();

        public float ReadFloatLE() => throw new IndexOutOfRangeException();

        public double ReadDouble() => throw new IndexOutOfRangeException();

        public double ReadDoubleLE() => throw new IndexOutOfRangeException();

        public IByteBuffer ReadBytes(int length) => this.CheckLength(length);

        public IByteBuffer ReadBytes(IByteBuffer destination) => this.CheckLength(destination.WritableBytes);

        public IByteBuffer ReadBytes(IByteBuffer destination, int length) => this.CheckLength(length);

        public IByteBuffer ReadBytes(IByteBuffer destination, int dstIndex, int length) => this.CheckLength(length);

        public IByteBuffer ReadBytes(byte[] destination) => this.CheckLength(destination.Length);

        public IByteBuffer ReadBytes(byte[] destination, int dstIndex, int length) => this.CheckLength(length);

        public IByteBuffer ReadBytes(Stream destination, int length) => this.CheckLength(length);

        public ICharSequence ReadCharSequence(int length, Encoding encoding)
        {
            this.CheckLength(length);
            return null;
        }

        public string ReadString(int length, Encoding encoding)
        {
            this.CheckLength(length);
            return null;
        }

        public IByteBuffer SkipBytes(int length) => this.CheckLength(length);

        public IByteBuffer WriteBoolean(bool value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteByte(int value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteShort(int value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteShortLE(int value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteUnsignedShort(ushort value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteUnsignedShortLE(ushort value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteMedium(int value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteMediumLE(int value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteUnsignedMedium(int value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteUnsignedMediumLE(int value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteInt(int value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteIntLE(int value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteUnsignedInt(uint value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteUnsignedIntLE(uint value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteLong(long value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteLongLE(long value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteChar(char value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteFloat(float value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteFloatLE(float value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteDouble(double value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteDoubleLE(double value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteBytes(IByteBuffer src) => this.CheckLength(src.ReadableBytes);

        public IByteBuffer WriteBytes(IByteBuffer src, int length) => this.CheckLength(length);

        public IByteBuffer WriteBytes(IByteBuffer src, int srcIndex, int length) => this.CheckLength(length);

        public IByteBuffer WriteBytes(byte[] src) => this.CheckLength(src.Length);

        public IByteBuffer WriteBytes(byte[] src, int srcIndex, int length) => this.CheckLength(length);

        public IByteBuffer WriteZero(int length) => this.CheckLength(length);

        public int WriteCharSequence(ICharSequence sequence, Encoding encoding) => throw new IndexOutOfRangeException();

        public int WriteString(string value, Encoding encoding) => throw new IndexOutOfRangeException();

        public int IndexOf(int fromIndex, int toIndex, byte value)
        {
            this.CheckIndex(fromIndex);
            this.CheckIndex(toIndex);
            return -1;
        }

        public int BytesBefore(byte value) => -1;

        public int BytesBefore(int length, byte value)
        {
            this.CheckLength(length);
            return -1;
        }

        public int BytesBefore(int index, int length, byte value)
        {
            this.CheckIndex(index, length);
            return -1;
        }

        public int ForEachByte(IByteProcessor processor) => -1;

        public int ForEachByte(int index, int length, IByteProcessor processor)
        {
            this.CheckIndex(index, length);
            return -1;
        }

        public int ForEachByteDesc(IByteProcessor processor) => -1;

        public int ForEachByteDesc(int index, int length, IByteProcessor processor)
        {
            this.CheckIndex(index, length);
            return -1;
        }

        public IByteBuffer Copy() => this;

        public IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            return this;
        }

        public IByteBuffer Slice() => this;

        public IByteBuffer RetainedSlice() => this;

        public IByteBuffer Slice(int index, int length) => this.CheckIndex(index, length);

        public IByteBuffer RetainedSlice(int index, int length) => this.CheckIndex(index, length);

        public IByteBuffer Duplicate() => this;

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

        public int ArrayOffset => 0;

        public bool HasMemoryAddress => false;

        public ref byte GetPinnableMemoryAddress() => throw new NotSupportedException();

        public IntPtr AddressOfPinnedMemory() => IntPtr.Zero;

        public string ToString(Encoding encoding) => string.Empty;

        public string ToString(int index, int length, Encoding encoding)
        {
            this.CheckIndex(index, length);
            return this.ToString(encoding);
        }

        public override int GetHashCode() => 0;

        public bool Equals(IByteBuffer buffer) => buffer != null && !buffer.IsReadable();

        public override bool Equals(object obj)
        {
            var buffer = obj as IByteBuffer;
            return this.Equals(buffer);
        }

        public int CompareTo(IByteBuffer buffer) => buffer.IsReadable() ? -1 : 0;

        public override string ToString() => string.Empty;

        public bool IsReadable() => false;

        public bool IsReadable(int size) => false;

        public int ReferenceCount => 1;

        public IReferenceCounted Retain() => this;

        public IByteBuffer RetainedDuplicate() => this;

        public IReferenceCounted Retain(int increment) => this;

        public IReferenceCounted Touch() => this;

        public IReferenceCounted Touch(object hint) => this;

        public bool Release() => false;

        public bool Release(int decrement) => false;

        public IByteBuffer ReadSlice(int length) => this.CheckLength(length);

        public IByteBuffer ReadRetainedSlice(int length) => this.CheckLength(length);

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

        // ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local
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
        // ReSharper restore ParameterOnlyUsedForPreconditionCheck.Local

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
    }
}