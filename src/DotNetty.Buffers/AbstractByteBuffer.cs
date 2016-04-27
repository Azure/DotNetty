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
    /// Abstract base class implementation of a <see cref="IByteBuffer"/>
    /// </summary>
    public abstract class AbstractByteBuffer : IByteBuffer
    {
        internal static readonly ResourceLeakDetector LeakDetector = ResourceLeakDetector.Create<IByteBuffer>();

        int markedReaderIndex;
        int markedWriterIndex;
        SwappedByteBuffer swappedByteBuffer;

        protected AbstractByteBuffer(int maxCapacity)
        {
            this.MaxCapacity = maxCapacity;
        }

        public abstract int Capacity { get; }

        public abstract IByteBuffer AdjustCapacity(int newCapacity);

        public int MaxCapacity { get; protected set; }

        public abstract IByteBufferAllocator Allocator { get; }

        public virtual int ReaderIndex { get; protected set; }

        public virtual int WriterIndex { get; protected set; }

        public virtual IByteBuffer SetWriterIndex(int writerIndex)
        {
            if (writerIndex < this.ReaderIndex || writerIndex > this.Capacity)
            {
                throw new IndexOutOfRangeException(string.Format("WriterIndex: {0} (expected: 0 <= readerIndex({1}) <= writerIndex <= capacity ({2})", writerIndex, this.ReaderIndex, this.Capacity));
            }

            this.WriterIndex = writerIndex;
            return this;
        }

        public virtual IByteBuffer SetReaderIndex(int readerIndex)
        {
            if (readerIndex < 0 || readerIndex > this.WriterIndex)
            {
                throw new IndexOutOfRangeException(string.Format("ReaderIndex: {0} (expected: 0 <= readerIndex <= writerIndex({1})", readerIndex, this.WriterIndex));
            }
            this.ReaderIndex = readerIndex;
            return this;
        }

        public virtual IByteBuffer SetIndex(int readerIndex, int writerIndex)
        {
            if (readerIndex < 0 || readerIndex > writerIndex || writerIndex > this.Capacity)
            {
                throw new IndexOutOfRangeException(string.Format("ReaderIndex: {0}, WriterIndex: {1} (expected: 0 <= readerIndex <= writerIndex <= capacity ({2})", readerIndex, writerIndex, this.Capacity));
            }

            this.ReaderIndex = readerIndex;
            this.WriterIndex = writerIndex;
            return this;
        }

        public virtual int ReadableBytes
        {
            get { return this.WriterIndex - this.ReaderIndex; }
        }

        public virtual int WritableBytes
        {
            get { return this.Capacity - this.WriterIndex; }
        }

        public virtual int MaxWritableBytes
        {
            get { return this.MaxCapacity - this.WriterIndex; }
        }

        public bool IsReadable()
        {
            return this.IsReadable(1);
        }

        public bool IsReadable(int size)
        {
            return this.ReadableBytes >= size;
        }

        public bool IsWritable()
        {
            return this.IsWritable(1);
        }

        public bool IsWritable(int size)
        {
            return this.WritableBytes >= size;
        }

        public virtual IByteBuffer Clear()
        {
            this.ReaderIndex = this.WriterIndex = 0;
            return this;
        }

        public virtual IByteBuffer MarkReaderIndex()
        {
            this.markedReaderIndex = this.ReaderIndex;
            return this;
        }

        public virtual IByteBuffer ResetReaderIndex()
        {
            this.SetReaderIndex(this.markedReaderIndex);
            return this;
        }

        public virtual IByteBuffer MarkWriterIndex()
        {
            this.markedWriterIndex = this.WriterIndex;
            return this;
        }

        public virtual IByteBuffer ResetWriterIndex()
        {
            this.SetWriterIndex(this.markedWriterIndex);
            return this;
        }

        public virtual IByteBuffer DiscardReadBytes()
        {
            this.EnsureAccessible();
            if (this.ReaderIndex == 0)
            {
                return this;
            }

            if (this.ReaderIndex != this.WriterIndex)
            {
                this.SetBytes(0, this, this.ReaderIndex, this.WriterIndex - this.ReaderIndex);
                this.WriterIndex -= this.ReaderIndex;
                this.AdjustMarkers(this.ReaderIndex);
                this.ReaderIndex = 0;
            }
            else
            {
                this.AdjustMarkers(this.ReaderIndex);
                this.WriterIndex = this.ReaderIndex = 0;
            }

            return this;
        }

        public virtual IByteBuffer DiscardSomeReadBytes()
        {
            this.EnsureAccessible();
            if (this.ReaderIndex == 0)
            {
                return this;
            }

            if (this.ReaderIndex == this.WriterIndex)
            {
                this.AdjustMarkers(this.ReaderIndex);
                this.WriterIndex = this.ReaderIndex = 0;
                return this;
            }

            if (this.ReaderIndex >= this.Capacity.RightUShift(1))
            {
                this.SetBytes(0, this, this.ReaderIndex, this.WriterIndex - this.ReaderIndex);
                this.WriterIndex -= this.ReaderIndex;
                this.AdjustMarkers(this.ReaderIndex);
                this.ReaderIndex = 0;
            }
            return this;
        }

        public virtual IByteBuffer EnsureWritable(int minWritableBytes)
        {
            if (minWritableBytes < 0)
            {
                throw new ArgumentOutOfRangeException("minWritableBytes",
                    "expected minWritableBytes to be greater than zero");
            }

            if (minWritableBytes <= this.WritableBytes)
            {
                return this;
            }

            if (minWritableBytes > this.MaxCapacity - this.WriterIndex)
            {
                throw new IndexOutOfRangeException(string.Format(
                    "writerIndex({0}) + minWritableBytes({1}) exceeds maxCapacity({2}): {3}",
                    this.WriterIndex, minWritableBytes, this.MaxCapacity, this));
            }

            //Normalize the current capacity to the power of 2
            int newCapacity = this.CalculateNewCapacity(this.WriterIndex + minWritableBytes);

            //Adjust to the new capacity
            this.AdjustCapacity(newCapacity);
            return this;
        }

        public int EnsureWritable(int minWritableBytes, bool force)
        {
            Contract.Ensures(minWritableBytes >= 0);

            if (minWritableBytes <= this.WritableBytes)
            {
                return 0;
            }

            if (minWritableBytes > this.MaxCapacity - this.WriterIndex)
            {
                if (force)
                {
                    if (this.Capacity == this.MaxCapacity)
                    {
                        return 1;
                    }

                    this.AdjustCapacity(this.MaxCapacity);
                    return 3;
                }
            }

            // Normalize the current capacity to the power of 2.
            int newCapacity = this.CalculateNewCapacity(this.WriterIndex + minWritableBytes);

            // Adjust to the new capacity.
            this.AdjustCapacity(newCapacity);
            return 2;
        }

        int CalculateNewCapacity(int minNewCapacity)
        {
            int maxCapacity = this.MaxCapacity;
            const int Threshold = 4 * 1024 * 1024; // 4 MiB page
            int newCapacity;
            if (minNewCapacity == Threshold)
            {
                return Threshold;
            }

            // If over threshold, do not double but just increase by threshold.
            if (minNewCapacity > Threshold)
            {
                newCapacity = minNewCapacity - (minNewCapacity % Threshold);
                return Math.Min(maxCapacity, newCapacity + Threshold);
            }

            // Not over threshold. Double up to 4 MiB, starting from 64.
            newCapacity = 64;
            while (newCapacity < minNewCapacity)
            {
                newCapacity <<= 1;
            }

            return Math.Min(newCapacity, maxCapacity);
        }

        public virtual bool GetBoolean(int index)
        {
            return this.GetByte(index) != 0;
        }

        public virtual byte GetByte(int index)
        {
            this.CheckIndex(index);
            return this._GetByte(index);
        }

        protected abstract byte _GetByte(int index);

        public virtual short GetShort(int index)
        {
            this.CheckIndex(index, 2);
            return this._GetShort(index);
        }

        protected abstract short _GetShort(int index);

        public virtual ushort GetUnsignedShort(int index)
        {
            unchecked
            {
                return (ushort)(this.GetShort(index));
            }
        }

        public virtual int GetInt(int index)
        {
            this.CheckIndex(index, 4);
            return this._GetInt(index);
        }

        protected abstract int _GetInt(int index);

        public virtual uint GetUnsignedInt(int index)
        {
            unchecked
            {
                return (uint)(this.GetInt(index));
            }
        }

        public virtual long GetLong(int index)
        {
            this.CheckIndex(index, 8);
            return this._GetLong(index);
        }

        protected abstract long _GetLong(int index);

        public virtual char GetChar(int index)
        {
            return Convert.ToChar(this.GetShort(index));
        }

        public virtual double GetDouble(int index)
        {
            return BitConverter.Int64BitsToDouble(this.GetLong(index));
        }

        public virtual IByteBuffer GetBytes(int index, IByteBuffer destination)
        {
            this.GetBytes(index, destination, destination.WritableBytes);
            return this;
        }

        public virtual IByteBuffer GetBytes(int index, IByteBuffer destination, int length)
        {
            this.GetBytes(index, destination, destination.WriterIndex, length);
            return this;
        }

        public abstract IByteBuffer GetBytes(int index, IByteBuffer destination, int dstIndex, int length);

        public virtual IByteBuffer GetBytes(int index, byte[] destination)
        {
            this.GetBytes(index, destination, 0, destination.Length);
            return this;
        }

        public abstract IByteBuffer GetBytes(int index, byte[] destination, int dstIndex, int length);

        public abstract IByteBuffer GetBytes(int index, Stream destination, int length);

        public virtual IByteBuffer SetBoolean(int index, bool value)
        {
            this.SetByte(index, value ? 1 : 0);
            return this;
        }

        public virtual IByteBuffer SetByte(int index, int value)
        {
            this.CheckIndex(index);
            this._SetByte(index, value);
            return this;
        }

        protected abstract void _SetByte(int index, int value);

        public virtual IByteBuffer SetShort(int index, int value)
        {
            this.CheckIndex(index, 2);
            this._SetShort(index, value);
            return this;
        }

        public IByteBuffer SetUnsignedShort(int index, ushort value)
        {
            this.SetShort(index, value);
            return this;
        }

        protected abstract void _SetShort(int index, int value);

        public virtual IByteBuffer SetInt(int index, int value)
        {
            this.CheckIndex(index, 4);
            this._SetInt(index, value);
            return this;
        }

        public IByteBuffer SetUnsignedInt(int index, uint value)
        {
            unchecked
            {
                this.SetInt(index, (int)value);
            }
            return this;
        }

        protected abstract void _SetInt(int index, int value);

        public virtual IByteBuffer SetLong(int index, long value)
        {
            this.CheckIndex(index, 8);
            this._SetLong(index, value);
            return this;
        }

        protected abstract void _SetLong(int index, long value);

        public virtual IByteBuffer SetChar(int index, char value)
        {
            this.SetShort(index, value);
            return this;
        }

        public virtual IByteBuffer SetDouble(int index, double value)
        {
            this.SetLong(index, BitConverter.DoubleToInt64Bits(value));
            return this;
        }

        public virtual IByteBuffer SetBytes(int index, IByteBuffer src)
        {
            this.SetBytes(index, src, src.ReadableBytes);
            return this;
        }

        public virtual IByteBuffer SetBytes(int index, IByteBuffer src, int length)
        {
            this.CheckIndex(index, length);
            if (src == null)
            {
                throw new NullReferenceException("src cannot be null");
            }
            if (length > src.ReadableBytes)
            {
                throw new IndexOutOfRangeException(string.Format(
                    "length({0}) exceeds src.readableBytes({1}) where src is: {2}", length, src.ReadableBytes, src));
            }
            this.SetBytes(index, src, src.ReaderIndex, length);
            src.SetReaderIndex(src.ReaderIndex + length);
            return this;
        }

        public abstract IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length);

        public virtual IByteBuffer SetBytes(int index, byte[] src)
        {
            this.SetBytes(index, src, 0, src.Length);
            return this;
        }

        public abstract IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length);

        public abstract Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken);

        public virtual bool ReadBoolean()
        {
            return this.ReadByte() != 0;
        }

        public virtual byte ReadByte()
        {
            this.CheckReadableBytes(1);
            int i = this.ReaderIndex;
            byte b = this.GetByte(i);
            this.ReaderIndex = i + 1;
            return b;
        }

        public virtual short ReadShort()
        {
            this.CheckReadableBytes(2);
            short v = this._GetShort(this.ReaderIndex);
            this.ReaderIndex += 2;
            return v;
        }

        public virtual ushort ReadUnsignedShort()
        {
            unchecked
            {
                return (ushort)(this.ReadShort());
            }
        }

        public virtual int ReadInt()
        {
            this.CheckReadableBytes(4);
            int v = this._GetInt(this.ReaderIndex);
            this.ReaderIndex += 4;
            return v;
        }

        public virtual uint ReadUnsignedInt()
        {
            unchecked
            {
                return (uint)(this.ReadInt());
            }
        }

        public virtual long ReadLong()
        {
            this.CheckReadableBytes(8);
            long v = this._GetLong(this.ReaderIndex);
            this.ReaderIndex += 8;
            return v;
        }

        public virtual char ReadChar()
        {
            return (char)this.ReadShort();
        }

        public virtual double ReadDouble()
        {
            return BitConverter.Int64BitsToDouble(this.ReadLong());
        }

        public IByteBuffer ReadBytes(int length)
        {
            this.CheckReadableBytes(length);
            if (length == 0)
            {
                return Unpooled.Empty;
            }

            IByteBuffer buf = Unpooled.Buffer(length, this.MaxCapacity);
            buf.WriteBytes(this, this.ReaderIndex, length);
            this.ReaderIndex += length;
            return buf;
        }

        public virtual IByteBuffer ReadBytes(IByteBuffer destination)
        {
            this.ReadBytes(destination, destination.WritableBytes);
            return this;
        }

        public virtual IByteBuffer ReadBytes(IByteBuffer destination, int length)
        {
            if (length > destination.WritableBytes)
            {
                throw new IndexOutOfRangeException(string.Format("length({0}) exceeds destination.WritableBytes({1}) where destination is: {2}",
                    length, destination.WritableBytes, destination));
            }
            this.ReadBytes(destination, destination.WriterIndex, length);
            destination.SetWriterIndex(destination.WriterIndex + length);
            return this;
        }

        public virtual IByteBuffer ReadBytes(IByteBuffer destination, int dstIndex, int length)
        {
            this.CheckReadableBytes(length);
            this.GetBytes(this.ReaderIndex, destination, dstIndex, length);
            this.ReaderIndex += length;
            return this;
        }

        public virtual IByteBuffer ReadBytes(byte[] destination)
        {
            this.ReadBytes(destination, 0, destination.Length);
            return this;
        }

        public virtual IByteBuffer ReadBytes(byte[] destination, int dstIndex, int length)
        {
            this.CheckReadableBytes(length);
            this.GetBytes(this.ReaderIndex, destination, dstIndex, length);
            this.ReaderIndex += length;
            return this;
        }

        public virtual IByteBuffer ReadBytes(Stream destination, int length)
        {
            this.CheckReadableBytes(length);
            this.GetBytes(this.ReaderIndex, destination, length);
            this.ReaderIndex += length;
            return this;
        }

        public virtual IByteBuffer SkipBytes(int length)
        {
            this.CheckReadableBytes(length);
            this.ReaderIndex += length;
            return this;
        }

        public virtual IByteBuffer WriteBoolean(bool value)
        {
            this.WriteByte(value ? 1 : 0);
            return this;
        }

        public virtual IByteBuffer WriteByte(int value)
        {
            this.EnsureWritable(1);
            this.SetByte(this.WriterIndex, value);
            this.WriterIndex += 1;
            return this;
        }

        public virtual IByteBuffer WriteShort(int value)
        {
            this.EnsureWritable(2);
            this._SetShort(this.WriterIndex, value);
            this.WriterIndex += 2;
            return this;
        }

        public IByteBuffer WriteUnsignedShort(ushort value)
        {
            unchecked
            {
                this.WriteShort((short)value);
            }
            return this;
        }

        public virtual IByteBuffer WriteInt(int value)
        {
            this.EnsureWritable(4);
            this._SetInt(this.WriterIndex, value);
            this.WriterIndex += 4;
            return this;
        }

        public IByteBuffer WriteUnsignedInt(uint value)
        {
            unchecked
            {
                this.WriteInt((int)value);
            }
            return this;
        }

        public virtual IByteBuffer WriteLong(long value)
        {
            this.EnsureWritable(8);
            this._SetLong(this.WriterIndex, value);
            this.WriterIndex += 8;
            return this;
        }

        public virtual IByteBuffer WriteChar(char value)
        {
            this.WriteShort(value);
            return this;
        }

        public virtual IByteBuffer WriteDouble(double value)
        {
            this.WriteLong(BitConverter.DoubleToInt64Bits(value));
            return this;
        }

        public virtual IByteBuffer WriteBytes(IByteBuffer src)
        {
            this.WriteBytes(src, src.ReadableBytes);
            return this;
        }

        public virtual IByteBuffer WriteBytes(IByteBuffer src, int length)
        {
            if (length > src.ReadableBytes)
            {
                throw new IndexOutOfRangeException(string.Format("length({0}) exceeds src.readableBytes({1}) where src is: {2}", length, src.ReadableBytes, src));
            }
            this.WriteBytes(src, src.ReaderIndex, length);
            src.SetReaderIndex(src.ReaderIndex + length);
            return this;
        }

        public virtual IByteBuffer WriteBytes(IByteBuffer src, int srcIndex, int length)
        {
            this.EnsureAccessible();
            this.EnsureWritable(length);
            this.SetBytes(this.WriterIndex, src, srcIndex, length);
            this.WriterIndex += length;
            return this;
        }

        public virtual IByteBuffer WriteBytes(byte[] src)
        {
            this.WriteBytes(src, 0, src.Length);
            return this;
        }

        public virtual IByteBuffer WriteBytes(byte[] src, int srcIndex, int length)
        {
            this.EnsureAccessible();
            this.EnsureWritable(length);
            this.SetBytes(this.WriterIndex, src, srcIndex, length);
            this.WriterIndex += length;
            return this;
        }

        public async Task WriteBytesAsync(Stream stream, int length, CancellationToken cancellationToken)
        {
            this.EnsureAccessible();
            this.EnsureWritable(length);
            if (this.WritableBytes < length)
            {
                throw new ArgumentOutOfRangeException("length");
            }

            int writerIndex = this.WriterIndex;
            int wrote = await this.SetBytesAsync(writerIndex, stream, length, cancellationToken);

            Contract.Assert(writerIndex == this.WriterIndex);

            this.SetWriterIndex(writerIndex + wrote);
        }

        public Task WriteBytesAsync(Stream stream, int length)
        {
            return this.WriteBytesAsync(stream, length, CancellationToken.None);
        }

        public abstract bool HasArray { get; }

        public abstract byte[] Array { get; }

        public abstract int ArrayOffset { get; }

        public virtual byte[] ToArray()
        {
            int readableBytes = this.ReadableBytes;
            if (readableBytes == 0)
            {
                return ByteArrayExtensions.Empty;
            }

            if (this.HasArray)
            {
                return this.Array.Slice(this.ArrayOffset + this.ReaderIndex, readableBytes);
            }

            var bytes = new byte[readableBytes];
            this.GetBytes(this.ReaderIndex, bytes);
            return bytes;
        }

        public virtual IByteBuffer Duplicate()
        {
            return new DuplicatedByteBuffer(this);
        }

        public abstract IByteBuffer Unwrap();

        public virtual ByteOrder Order // todo: push to actual implementations for them to decide
        {
            get { return ByteOrder.BigEndian; }
        }

        public IByteBuffer WithOrder(ByteOrder order)
        {
            if (order == this.Order)
            {
                return this;
            }
            SwappedByteBuffer swappedBuf = this.swappedByteBuffer;
            if (swappedBuf == null)
            {
                this.swappedByteBuffer = swappedBuf = this.NewSwappedByteBuffer();
            }
            return swappedBuf;
        }

        /// <summary>
        /// Creates a new <see cref="SwappedByteBuffer"/> for this <see cref="IByteBuffer"/> instance.
        /// </summary>
        /// <returns>A <see cref="SwappedByteBuffer"/> for this buffer.</returns>
        protected SwappedByteBuffer NewSwappedByteBuffer()
        {
            return new SwappedByteBuffer(this);
        }

        protected void AdjustMarkers(int decrement)
        {
            int markedReaderIndex = this.markedReaderIndex;
            if (markedReaderIndex <= decrement)
            {
                this.markedReaderIndex = 0;
                int markedWriterIndex = this.markedWriterIndex;
                if (markedWriterIndex <= decrement)
                {
                    this.markedWriterIndex = 0;
                }
                else
                {
                    this.markedWriterIndex = markedWriterIndex - decrement;
                }
            }
            else
            {
                this.markedReaderIndex = markedReaderIndex - decrement;
                this.markedWriterIndex -= decrement;
            }
        }

        protected void CheckIndex(int index)
        {
            this.EnsureAccessible();
            if (index < 0 || index >= this.Capacity)
            {
                throw new IndexOutOfRangeException(string.Format("index: {0} (expected: range(0, {1})", index, this.Capacity));
            }
        }

        protected void CheckIndex(int index, int fieldLength)
        {
            this.EnsureAccessible();
            if (fieldLength < 0)
            {
                throw new IndexOutOfRangeException(string.Format("length: {0} (expected: >= 0)", fieldLength));
            }

            if (index < 0 || index > this.Capacity - fieldLength)
            {
                throw new IndexOutOfRangeException(string.Format("index: {0}, length: {1} (expected: range(0, {2})", index, fieldLength, this.Capacity));
            }
        }

        protected void CheckSrcIndex(int index, int length, int srcIndex, int srcCapacity)
        {
            this.CheckIndex(index, length);
            if (srcIndex < 0 || srcIndex > srcCapacity - length)
            {
                throw new IndexOutOfRangeException(string.Format(
                    "srcIndex: {0}, length: {1} (expected: range(0, {2}))", srcIndex, length, srcCapacity));
            }
        }

        protected void CheckDstIndex(int index, int length, int dstIndex, int dstCapacity)
        {
            this.CheckIndex(index, length);
            if (dstIndex < 0 || dstIndex > dstCapacity - length)
            {
                throw new IndexOutOfRangeException(string.Format(
                    "dstIndex: {0}, length: {1} (expected: range(0, {2}))", dstIndex, length, dstCapacity));
            }
        }

        /// <summary>
        /// Throws a <see cref="IndexOutOfRangeException"/> if the current <see cref="ReadableBytes"/> of this buffer
        /// is less than <see cref="minimumReadableBytes"/>.
        /// </summary>
        protected void CheckReadableBytes(int minimumReadableBytes)
        {
            this.EnsureAccessible();
            if (minimumReadableBytes < 0)
            {
                throw new ArgumentOutOfRangeException("minimumReadableBytes", string.Format("minimumReadableBytes: {0} (expected: >= 0)", minimumReadableBytes));
            }

            if (this.ReaderIndex > this.WriterIndex - minimumReadableBytes)
            {
                throw new IndexOutOfRangeException(string.Format(
                    "readerIndex({0}) + length({1}) exceeds writerIndex({2}): {3}",
                    this.ReaderIndex, minimumReadableBytes, this.WriterIndex, this));
            }
        }

        protected void EnsureAccessible()
        {
            if (this.ReferenceCount == 0)
            {
                throw new IllegalReferenceCountException(0);
            }
        }

        public IByteBuffer Copy()
        {
            return this.Copy(this.ReaderIndex, this.ReadableBytes);
        }

        public abstract IByteBuffer Copy(int index, int length);

        public IByteBuffer Slice()
        {
            return this.Slice(this.ReaderIndex, this.ReadableBytes);
        }

        public virtual IByteBuffer Slice(int index, int length)
        {
            return new SlicedByteBuffer(this, index, length);
        }

        public IByteBuffer ReadSlice(int length)
        {
            IByteBuffer slice = this.Slice(this.ReaderIndex, length);
            this.ReaderIndex += length;
            return slice;
        }

        public abstract int ReferenceCount { get; }

        public abstract IReferenceCounted Retain();

        public abstract IReferenceCounted Retain(int increment);

        public abstract IReferenceCounted Touch();

        public abstract IReferenceCounted Touch(object hint);

        public abstract bool Release();

        public abstract bool Release(int decrement);

        protected void DiscardMarkers()
        {
            this.markedReaderIndex = this.markedWriterIndex = 0;
        }

        public string ToString(Encoding encoding)
        {
            return this.ToString(ReaderIndex, ReadableBytes, encoding);
        }

        public string ToString(int index, int length, Encoding encoding)
        {
            return ByteBufferUtil.DecodeString(this, index, length, encoding);
        }

        public int ForEachByte(ByteProcessor processor)
        {
            int index = this.ReaderIndex;
            int length = this.WriterIndex - index;
            this.EnsureAccessible();

            return this.ForEachByteAsc0(index, length, processor);
        }

        public int ForEachByte(int index, int length, ByteProcessor processor)
        {
            this.CheckIndex(index, length);

            return this.ForEachByteAsc0(index, length, processor);
        }

        int ForEachByteAsc0(int index, int length, ByteProcessor processor)
        {
            if (processor == null)
            {
                throw new NullReferenceException("processor");
            }

            if (length == 0)
            {
                return -1;
            }

            int endIndex = index + length;
            int i = index;
            try
            {
                do
                {
                    if (processor.Process(this._GetByte(i)))
                    {
                        i++;
                    }
                    else
                    {
                        return i;
                    }
                } while (i < endIndex);
            }
            catch
            {
                throw;
            }

            return -1;
        }

        public int ForEachByteDesc(ByteProcessor processor)
        {
            int index = this.ReaderIndex;
            int length = this.WriterIndex - index;
            this.EnsureAccessible();

            return this.ForEachByteDesc0(index, length, processor);
        }

        public int ForEachByteDesc(int index, int length, ByteProcessor processor)
        {
            this.CheckIndex(index, length);

            return this.ForEachByteDesc0(index, length, processor);
        }

        int ForEachByteDesc0(int index, int length, ByteProcessor processor)
        {

            if (processor == null)
            {
                throw new NullReferenceException("processor");
            }

            if (length == 0)
            {
                return -1;
            }

            int i = index + length - 1;
            try
            {
                do
                {
                    if (processor.Process(this._GetByte(i)))
                    {
                        i--;
                    }
                    else
                    {
                        return i;
                    }
                } while (i >= index);
            }
            catch
            {
                throw;
            }

            return -1;
        }
    }
}