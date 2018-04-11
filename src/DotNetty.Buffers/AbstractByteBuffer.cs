// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable InconsistentNaming
namespace DotNetty.Buffers
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    /// <inheritdoc />
    /// <summary>
    ///     Abstract base class implementation of a <see cref="T:DotNetty.Buffers.IByteBuffer" />
    /// </summary>
    public abstract class AbstractByteBuffer : IByteBuffer
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<AbstractByteBuffer>();
        const string PropMode = "io.netty.buffer.bytebuf.checkAccessible";
        static readonly bool CheckAccessible;

        internal static readonly ResourceLeakDetector LeakDetector = ResourceLeakDetector.Create<IByteBuffer>();

        int readerIndex;
        int writerIndex;

        int markedReaderIndex;
        int markedWriterIndex;
        int maxCapacity;

        static AbstractByteBuffer()
        {
            CheckAccessible = SystemPropertyUtil.GetBoolean(PropMode, true);
            if (Logger.DebugEnabled)
            {
                Logger.Debug("-D{}: {}", PropMode, CheckAccessible);
            }
        }

        protected AbstractByteBuffer(int maxCapacity)
        {
            Contract.Requires(maxCapacity >= 0);

            this.maxCapacity = maxCapacity;
        }

        public abstract int Capacity { get; }

        public abstract IByteBuffer AdjustCapacity(int newCapacity);

        public virtual int MaxCapacity => this.maxCapacity;

        protected void SetMaxCapacity(int newMaxCapacity)
        {
            Contract.Requires(newMaxCapacity >= 0);

            this.maxCapacity = newMaxCapacity;
        }

        public abstract IByteBufferAllocator Allocator { get; }

        public virtual int ReaderIndex => this.readerIndex;

        public virtual IByteBuffer SetReaderIndex(int index)
        {
            if (index < 0 || index > this.writerIndex)
            {
                ThrowHelper.ThrowIndexOutOfRangeException_ReaderIndex(index, this.WriterIndex);
            }

            this.readerIndex = index;
            return this;
        }

        public virtual int WriterIndex => this.writerIndex;

        public virtual IByteBuffer SetWriterIndex(int index)
        {
            if (index < this.readerIndex || index > this.Capacity)
            {
                ThrowHelper.ThrowIndexOutOfRangeException_WriterIndex(index, this.readerIndex, this.Capacity);
            }

            this.SetWriterIndex0(index);
            return this;
        }

        protected void SetWriterIndex0(int index)
        {
            this.writerIndex = index;
        }

        public virtual IByteBuffer SetIndex(int readerIdx, int writerIdx)
        {
            if (readerIdx < 0 || readerIdx > writerIdx || writerIdx > this.Capacity)
            {
                ThrowHelper.ThrowIndexOutOfRangeException_ReaderWriterIndex(readerIdx, writerIdx, this.Capacity);
            }

            this.SetIndex0(readerIdx, writerIdx);
            return this;
        }

        public virtual IByteBuffer Clear()
        {
            this.readerIndex = this.writerIndex = 0;
            return this;
        }

        public virtual bool IsReadable() => this.writerIndex > this.readerIndex;

        public virtual bool IsReadable(int size) => this.writerIndex - this.readerIndex >= size;

        public virtual bool IsWritable() => this.Capacity > this.writerIndex;

        public virtual bool IsWritable(int size) => this.Capacity - this.writerIndex >= size;

        public virtual int ReadableBytes => this.writerIndex - this.readerIndex;

        public virtual int WritableBytes => this.Capacity - this.writerIndex;

        public virtual int MaxWritableBytes => this.MaxCapacity - this.writerIndex;

        public virtual IByteBuffer MarkReaderIndex()
        {
            this.markedReaderIndex = this.readerIndex;
            return this;
        }

        public virtual IByteBuffer ResetReaderIndex()
        {
            this.SetReaderIndex(this.markedReaderIndex);
            return this;
        }

        public virtual IByteBuffer MarkWriterIndex()
        {
            this.markedWriterIndex = this.writerIndex;
            return this;
        }

        public virtual IByteBuffer ResetWriterIndex()
        {
            this.SetWriterIndex(this.markedWriterIndex);
            return this;
        }

        protected void MarkIndex()
        {
            this.markedReaderIndex = this.readerIndex;
            this.markedWriterIndex = this.writerIndex;
        }

        public virtual IByteBuffer DiscardReadBytes()
        {
            this.EnsureAccessible();
            if (this.readerIndex == 0)
            {
                return this;
            }

            if (this.readerIndex != this.writerIndex)
            {
                this.SetBytes(0, this, this.readerIndex, this.writerIndex - this.readerIndex);
                this.writerIndex -= this.readerIndex;
                this.AdjustMarkers(this.readerIndex);
                this.readerIndex = 0;
            }
            else
            {
                this.AdjustMarkers(this.readerIndex);
                this.writerIndex = this.readerIndex = 0;
            }

            return this;
        }

        public virtual IByteBuffer DiscardSomeReadBytes()
        {
            this.EnsureAccessible();
            if (this.readerIndex == 0)
            {
                return this;
            }

            if (this.readerIndex == this.writerIndex)
            {
                this.AdjustMarkers(this.readerIndex);
                this.writerIndex = this.readerIndex = 0;
                return this;
            }

            if (this.readerIndex >= this.Capacity.RightUShift(1))
            {
                this.SetBytes(0, this, this.readerIndex, this.writerIndex - this.readerIndex);
                this.writerIndex -= this.readerIndex;
                this.AdjustMarkers(this.readerIndex);
                this.readerIndex = 0;
            }

            return this;
        }

        protected void AdjustMarkers(int decrement)
        {
            int markedReaderIdx = this.markedReaderIndex;
            if (markedReaderIdx <= decrement)
            {
                this.markedReaderIndex = 0;
                int markedWriterIdx = this.markedWriterIndex;
                if (markedWriterIdx <= decrement)
                {
                    this.markedWriterIndex = 0;
                }
                else
                {
                    this.markedWriterIndex = markedWriterIdx - decrement;
                }
            }
            else
            {
                this.markedReaderIndex = markedReaderIdx - decrement;
                this.markedWriterIndex -= decrement;
            }
        }

        public virtual IByteBuffer EnsureWritable(int minWritableBytes)
        {
            if (minWritableBytes < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_MinWritableBytes();
            }

            this.EnsureWritable0(minWritableBytes);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void EnsureWritable0(int minWritableBytes)
        {
            this.EnsureAccessible();
            if (minWritableBytes <= this.WritableBytes)
            {
                return;
            }

            if (minWritableBytes > this.MaxCapacity - this.writerIndex)
            {
                ThrowHelper.ThrowIndexOutOfRangeException_WriterIndex(minWritableBytes, this.writerIndex, this.MaxCapacity, this);
            }

            // Normalize the current capacity to the power of 2.
            int newCapacity = this.Allocator.CalculateNewCapacity(this.writerIndex + minWritableBytes, this.MaxCapacity);

            // Adjust to the new capacity.
            this.AdjustCapacity(newCapacity);
        }

        public virtual int EnsureWritable(int minWritableBytes, bool force)
        {
            Contract.Ensures(minWritableBytes >= 0);

            this.EnsureAccessible();
            if (minWritableBytes <= this.WritableBytes)
            {
                return 0;
            }

            if (minWritableBytes > this.MaxCapacity - this.writerIndex)
            {
                if (!force || this.Capacity == this.MaxCapacity)
                {
                        return 1;
                }

                this.AdjustCapacity(this.MaxCapacity);
                return 3;
            }

            // Normalize the current capacity to the power of 2.
            int newCapacity = this.Allocator.CalculateNewCapacity(this.writerIndex + minWritableBytes, this.MaxCapacity);

            // Adjust to the new capacity.
            this.AdjustCapacity(newCapacity);
            return 2;
        }

        public virtual byte GetByte(int index)
        {
            this.CheckIndex(index);
            return this._GetByte(index);
        }

        protected internal abstract byte _GetByte(int index);

        public bool GetBoolean(int index) => this.GetByte(index) != 0;

        public virtual short GetShort(int index)
        {
            this.CheckIndex(index, 2);
            return this._GetShort(index);
        }

        protected internal abstract short _GetShort(int index);

        public virtual short GetShortLE(int index)
        {
            this.CheckIndex(index, 2);
            return this._GetShortLE(index);
        }

        protected internal abstract short _GetShortLE(int index);

        public ushort GetUnsignedShort(int index)
        {
            unchecked
            {
                return (ushort)this.GetShort(index);
            }
        }

        public ushort GetUnsignedShortLE(int index)
        {
            unchecked
            {
                return (ushort)this.GetShortLE(index);
            }
        }

        public virtual int GetUnsignedMedium(int index)
        {
            this.CheckIndex(index, 3);
            return this._GetUnsignedMedium(index);
        }

        protected internal abstract int _GetUnsignedMedium(int index);

        public virtual int GetUnsignedMediumLE(int index)
        {
            this.CheckIndex(index, 3);
            return this._GetUnsignedMediumLE(index);
        }

        protected internal abstract int _GetUnsignedMediumLE(int index);

        public int GetMedium(int index)
        {
            uint value = (uint)this.GetUnsignedMedium(index);
            if ((value & 0x800000) != 0)
            {
                value |= 0xff000000;
            }

            return (int)value;
        }

        public int GetMediumLE(int index)
        {
            uint value = (uint)this.GetUnsignedMediumLE(index);
            if ((value & 0x800000) != 0)
            {
                value |= 0xff000000;
            }

            return (int)value;
        }

        public virtual int GetInt(int index)
        {
            this.CheckIndex(index, 4);
            return this._GetInt(index);
        }

        protected internal abstract int _GetInt(int index);

        public virtual int GetIntLE(int index)
        {
            this.CheckIndex(index, 4);
            return this._GetIntLE(index);
        }

        protected internal abstract int _GetIntLE(int index);

        public uint GetUnsignedInt(int index)
        {
            unchecked
            {
                return (uint)(this.GetInt(index));
            }
        }

        public uint GetUnsignedIntLE(int index)
        {
            unchecked
            {
                return (uint)this.GetIntLE(index);
            }
        }

        public virtual long GetLong(int index)
        {
            this.CheckIndex(index, 8);
            return this._GetLong(index);
        }

        protected internal abstract long _GetLong(int index);

        public virtual long GetLongLE(int index)
        {
            this.CheckIndex(index, 8);
            return this._GetLongLE(index);
        }

        protected internal abstract long _GetLongLE(int index);

        public virtual char GetChar(int index) => Convert.ToChar(this.GetShort(index));

        public float GetFloat(int index) => ByteBufferUtil.Int32BitsToSingle(this.GetInt(index));

        public float GetFloatLE(int index) => ByteBufferUtil.Int32BitsToSingle(this.GetIntLE(index));

        public double GetDouble(int index) => BitConverter.Int64BitsToDouble(this.GetLong(index));

        public double GetDoubleLE(int index) => BitConverter.Int64BitsToDouble(this.GetLongLE(index));

        public virtual IByteBuffer GetBytes(int index, byte[] destination)
        {
            this.GetBytes(index, destination, 0, destination.Length);
            return this;
        }

        public abstract IByteBuffer GetBytes(int index, byte[] destination, int dstIndex, int length);

        public virtual IByteBuffer GetBytes(int index, IByteBuffer destination)
        {
            this.GetBytes(index, destination, destination.WritableBytes);
            return this;
        }

        public virtual IByteBuffer GetBytes(int index, IByteBuffer destination, int length)
        {
            this.GetBytes(index, destination, destination.WriterIndex, length);
            destination.SetWriterIndex(destination.WriterIndex + length);
            return this;
        }

        public abstract IByteBuffer GetBytes(int index, IByteBuffer destination, int dstIndex, int length);

        public abstract IByteBuffer GetBytes(int index, Stream destination, int length);

        public virtual unsafe string GetString(int index, int length, Encoding encoding)
        {
            this.CheckIndex0(index, length);
            if (length == 0)
            {
                return string.Empty;
            }

            if (this.HasMemoryAddress)
            {
                IntPtr ptr = this.AddressOfPinnedMemory();
                if (ptr != IntPtr.Zero)
                {
                    return UnsafeByteBufferUtil.GetString((byte*)(ptr + index), length, encoding);
                }
                else 
                {
                    fixed (byte* p = &this.GetPinnableMemoryAddress())
                        return UnsafeByteBufferUtil.GetString(p + index, length, encoding);
                }
            }
            if (this.HasArray)
            {
                return encoding.GetString(this.Array, this.ArrayOffset + index, length);
            }

            return this.ToString(index, length, encoding);
        }

        public virtual string ReadString(int length, Encoding encoding)
        {
            string value = this.GetString(this.readerIndex, length, encoding);
            this.readerIndex += length;
            return value;
        }

        public virtual unsafe ICharSequence GetCharSequence(int index, int length, Encoding encoding)
        {
            this.CheckIndex0(index, length);
            if (length == 0)
            {
                return StringCharSequence.Empty;
            }

            if (this.HasMemoryAddress)
            {
                IntPtr ptr = this.AddressOfPinnedMemory();
                if (ptr != IntPtr.Zero)
                {
                    return new StringCharSequence(UnsafeByteBufferUtil.GetString((byte*)(ptr + index), length, encoding));
                }
                else
                {
                    fixed (byte* p = &this.GetPinnableMemoryAddress())
                        return new StringCharSequence(UnsafeByteBufferUtil.GetString(p + index, length, encoding));
                }
            }
            if (this.HasArray)
            {
                return new StringCharSequence(encoding.GetString(this.Array, this.ArrayOffset + index, length));
            }

            return new StringCharSequence(this.ToString(index, length, encoding));
        }

        public virtual ICharSequence ReadCharSequence(int length, Encoding encoding)
        {
            ICharSequence sequence = this.GetCharSequence(this.readerIndex, length, encoding);
            this.readerIndex += length;
            return sequence;
        }

        public virtual IByteBuffer SetByte(int index, int value)
        {
            this.CheckIndex(index);
            this._SetByte(index, value);
            return this;
        }

        protected internal abstract void _SetByte(int index, int value);

        public virtual IByteBuffer SetBoolean(int index, bool value)
        {
            this.SetByte(index, value ? 1 : 0);
            return this;
        }

        public virtual IByteBuffer SetShort(int index, int value)
        {
            this.CheckIndex(index, 2);
            this._SetShort(index, value);
            return this;
        }

        protected internal abstract void _SetShort(int index, int value);

        public virtual IByteBuffer SetShortLE(int index, int value)
        {
            this.CheckIndex(index, 2);
            this._SetShortLE(index, value);
            return this;
        }

        protected internal abstract void _SetShortLE(int index, int value);

        public virtual IByteBuffer SetUnsignedShort(int index, ushort value)
        {
            this.SetShort(index, value);
            return this;
        }

        public virtual IByteBuffer SetUnsignedShortLE(int index, ushort value)
        {
            this.SetShortLE(index, value);
            return this;
        }

        public virtual IByteBuffer SetChar(int index, char value)
        {
            this.SetShort(index, value);
            return this;
        }

        public virtual IByteBuffer SetMedium(int index, int value)
        {
            this.CheckIndex(index, 3);
            this._SetMedium(index, value);
            return this;
        }

        protected internal abstract void _SetMedium(int index, int value);

        public virtual IByteBuffer SetMediumLE(int index, int value)
        {
            this.CheckIndex(index, 3);
            this._SetMediumLE(index, value);
            return this;
        }

        protected internal abstract void _SetMediumLE(int index, int value);

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
                return this.SetInt(index, (int)value);
            }
        }

        public IByteBuffer SetUnsignedIntLE(int index, uint value)
        {
            unchecked
            {
                return this.SetIntLE(index, (int)value);
            }
        }

        protected internal abstract void _SetInt(int index, int value);

        public virtual IByteBuffer SetIntLE(int index, int value)
        {
            this.CheckIndex(index, 4);
            this._SetIntLE(index, value);
            return this;
        }

        protected internal abstract void _SetIntLE(int index, int value);

        public virtual IByteBuffer SetFloat(int index, float value)
        {
            this.SetInt(index, ByteBufferUtil.SingleToInt32Bits(value));
            return this;
        }

        public IByteBuffer SetFloatLE(int index, float value) => this.SetIntLE(index, ByteBufferUtil.SingleToInt32Bits(value));

        public virtual IByteBuffer SetLong(int index, long value)
        {
            this.CheckIndex(index, 8);
            this._SetLong(index, value);
            return this;
        }

        protected internal abstract void _SetLong(int index, long value);

        public virtual IByteBuffer SetLongLE(int index, long value)
        {
            this.CheckIndex(index, 8);
            this._SetLongLE(index, value);
            return this;
        }

        protected internal abstract void _SetLongLE(int index, long value);

        public virtual IByteBuffer SetDouble(int index, double value)
        {
            this.SetLong(index, BitConverter.DoubleToInt64Bits(value));
            return this;
        }

        public IByteBuffer SetDoubleLE(int index, double value) => this.SetLongLE(index, BitConverter.DoubleToInt64Bits(value));

        public virtual IByteBuffer SetBytes(int index, byte[] src)
        {
            this.SetBytes(index, src, 0, src.Length);
            return this;
        }

        public abstract IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length);

        public virtual IByteBuffer SetBytes(int index, IByteBuffer src)
        {
            this.SetBytes(index, src, src.ReadableBytes);
            return this;
        }

        public virtual IByteBuffer SetBytes(int index, IByteBuffer src, int length)
        {
            Contract.Requires(src != null);

            this.CheckIndex(index, length);
            if (length > src.ReadableBytes)
            {
                ThrowHelper.ThrowIndexOutOfRangeException_ReadableBytes(length, src);
            }
            this.SetBytes(index, src, src.ReaderIndex, length);
            src.SetReaderIndex(src.ReaderIndex + length);
            return this;
        }

        public abstract IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length);

        public abstract Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken);

        public virtual IByteBuffer SetZero(int index, int length)
        {
            if (length == 0)
            {
                return this;
            }

            this.CheckIndex(index, length);

            int nLong = length.RightUShift(3);
            int nBytes = length & 7;
            for (int i = nLong; i > 0; i--)
            {
                this._SetLong(index, 0);
                index += 8;
            }
            if (nBytes == 4)
            {
                this._SetInt(index, 0);
                // Not need to update the index as we not will use it after this.
            }
            else if (nBytes < 4)
            {
                for (int i = nBytes; i > 0; i--)
                {
                    this._SetByte(index, 0);
                    index++;
                }
            }
            else
            {
                this._SetInt(index, 0);
                index += 4;
                for (int i = nBytes - 4; i > 0; i--)
                {
                    this._SetByte(index, 0);
                    index++;
                }
            }

            return this;
        }

        public virtual int SetString(int index, string value, Encoding encoding) => this.SetString0(index, value, encoding, false);

        int SetString0(int index, string value, Encoding encoding, bool expand)
        {
            if (ReferenceEquals(encoding, Encoding.UTF8))
            {
                int length = ByteBufferUtil.Utf8MaxBytes(value);
                if (expand)
                {
                    this.EnsureWritable0(length);
                    this.CheckIndex0(index, length);
                }
                else
                {
                    this.CheckIndex(index, length);
                }
                return ByteBufferUtil.WriteUtf8(this, index, value, value.Length);
            }
            if (ReferenceEquals(encoding, Encoding.ASCII))
            {
                int length = value.Length;
                if (expand)
                {
                    this.EnsureWritable0(length);
                    this.CheckIndex0(index, length);
                }
                else
                {
                    this.CheckIndex(index, length);
                }
                return ByteBufferUtil.WriteAscii(this, index, value, length);
            }
            byte[] bytes = encoding.GetBytes(value);
            if (expand)
            {
                this.EnsureWritable0(bytes.Length);
                // setBytes(...) will take care of checking the indices.
            }
            this.SetBytes(index, bytes);
            return bytes.Length;
        }

        public virtual int SetCharSequence(int index, ICharSequence sequence, Encoding encoding) => this.SetCharSequence0(index, sequence, encoding, false);

        int SetCharSequence0(int index, ICharSequence sequence, Encoding encoding, bool expand)
        {
            if (ReferenceEquals(encoding, Encoding.UTF8))
            {
                int length = ByteBufferUtil.Utf8MaxBytes(sequence);
                if (expand)
                {
                    this.EnsureWritable0(length);
                    this.CheckIndex0(index, length);
                }
                else
                {
                    this.CheckIndex(index, length);
                }
                return ByteBufferUtil.WriteUtf8(this, index, sequence, sequence.Count);
            }
            if (ReferenceEquals(encoding, Encoding.ASCII))
            {
                int length = sequence.Count;
                if (expand)
                {
                    this.EnsureWritable0(length);
                    this.CheckIndex0(index, length);
                }
                else
                {
                    this.CheckIndex(index, length);
                }
                return ByteBufferUtil.WriteAscii(this, index, sequence, length);
            }
            byte[] bytes = encoding.GetBytes(sequence.ToString());
            if (expand)
            {
                this.EnsureWritable0(bytes.Length);
                // setBytes(...) will take care of checking the indices.
            }
            this.SetBytes(index, bytes);
            return bytes.Length;
        }

        public virtual byte ReadByte()
        {
            this.CheckReadableBytes0(1);
            int i = this.readerIndex;
            byte b = this._GetByte(i);
            this.readerIndex = i + 1;
            return b;
        }

        public bool ReadBoolean() => this.ReadByte() != 0;

        public virtual short ReadShort()
        {
            this.CheckReadableBytes0(2);
            short v = this._GetShort(this.readerIndex);
            this.readerIndex += 2;
            return v;
        }

        public virtual short ReadShortLE()
        {
            this.CheckReadableBytes0(2);
            short v = this._GetShortLE(this.readerIndex);
            this.readerIndex += 2;
            return v;
        }

        public ushort ReadUnsignedShort()
        {
            unchecked
            {
                return (ushort)(this.ReadShort());
            }
        }

        public ushort ReadUnsignedShortLE()
        {
            unchecked
            {
                return (ushort)this.ReadShortLE();
            }
        }

        public int ReadMedium()
        {
            uint value = (uint)this.ReadUnsignedMedium();
            if ((value & 0x800000) != 0)
            {
                value |= 0xff000000;
            }

            return (int)value;
        }

        public int ReadMediumLE()
        {
            uint value = (uint)this.ReadUnsignedMediumLE();
            if ((value & 0x800000) != 0)
            {
                value |= 0xff000000;
            }

            return (int)value;
        }

        public virtual int ReadUnsignedMedium()
        {
            this.CheckReadableBytes0(3);
            int v = this._GetUnsignedMedium(this.readerIndex);
            this.readerIndex += 3;
            return v;
        }

        public virtual int ReadUnsignedMediumLE()
        {
            this.CheckReadableBytes0(3);
            int v = this._GetUnsignedMediumLE(this.readerIndex);
            this.readerIndex += 3;
            return v;
        }

        public virtual int ReadInt()
        {
            this.CheckReadableBytes0(4);
            int v = this._GetInt(this.readerIndex);
            this.readerIndex += 4;
            return v;
        }

        public virtual int ReadIntLE()
        {
            this.CheckReadableBytes0(4);
            int v = this._GetIntLE(this.readerIndex);
            this.readerIndex += 4;
            return v;
        }

        public uint ReadUnsignedInt()
        {
            unchecked
            {
                return (uint)(this.ReadInt());
            }
        }

        public uint ReadUnsignedIntLE()
        {
            unchecked
            {
                return (uint)this.ReadIntLE();
            }
        }

        public virtual long ReadLong()
        {
            this.CheckReadableBytes0(8);
            long v = this._GetLong(this.readerIndex);
            this.readerIndex += 8;
            return v;
        }

        public virtual long ReadLongLE()
        {
            this.CheckReadableBytes0(8);
            long v = this._GetLongLE(this.readerIndex);
            this.readerIndex += 8;
            return v;
        }

        public char ReadChar() => (char)this.ReadShort();

        public float ReadFloat() => ByteBufferUtil.Int32BitsToSingle(this.ReadInt());

        public float ReadFloatLE() => ByteBufferUtil.Int32BitsToSingle(this.ReadIntLE());

        public double ReadDouble() => BitConverter.Int64BitsToDouble(this.ReadLong());

        public double ReadDoubleLE() => BitConverter.Int64BitsToDouble(this.ReadLongLE());

        public virtual IByteBuffer ReadBytes(int length)
        {
            this.CheckReadableBytes(length);
            if (length == 0)
            {
                return Unpooled.Empty;
            }

            IByteBuffer buf = this.Allocator.Buffer(length, this.MaxCapacity);
            buf.WriteBytes(this, this.readerIndex, length);
            this.readerIndex += length;
            return buf;
        }

        public virtual IByteBuffer ReadSlice(int length)
        {
            this.CheckReadableBytes(length);
            IByteBuffer slice = this.Slice(this.readerIndex, length);
            this.readerIndex += length;
            return slice;
        }

        public virtual IByteBuffer ReadRetainedSlice(int length)
        {
            this.CheckReadableBytes(length);
            IByteBuffer slice = this.RetainedSlice(this.readerIndex, length);
            this.readerIndex += length;
            return slice;
        }

        public virtual IByteBuffer ReadBytes(byte[] destination, int dstIndex, int length)
        {
            this.CheckReadableBytes(length);
            this.GetBytes(this.readerIndex, destination, dstIndex, length);
            this.readerIndex += length;
            return this;
        }

        public virtual IByteBuffer ReadBytes(byte[] dst)
        {
            this.ReadBytes(dst, 0, dst.Length);
            return this;
        }

        public virtual IByteBuffer ReadBytes(IByteBuffer dst)
        {
            this.ReadBytes(dst, dst.WritableBytes);
            return this;
        }

        public virtual IByteBuffer ReadBytes(IByteBuffer dst, int length)
        {
            if (length > dst.WritableBytes)
            {
                ThrowHelper.ThrowIndexOutOfRangeException_WritableBytes(length, dst);
            }
            this.ReadBytes(dst, dst.WriterIndex, length);
            dst.SetWriterIndex(dst.WriterIndex + length);
            return this;
        }

        public virtual IByteBuffer ReadBytes(IByteBuffer dst, int dstIndex, int length)
        {
            this.CheckReadableBytes(length);
            this.GetBytes(this.readerIndex, dst, dstIndex, length);
            this.readerIndex += length;
            return this;
        }

        public virtual IByteBuffer ReadBytes(Stream destination, int length)
        {
            this.CheckReadableBytes(length);
            this.GetBytes(this.readerIndex, destination, length);
            this.readerIndex += length;
            return this;
        }

        public virtual IByteBuffer SkipBytes(int length)
        {
            this.CheckReadableBytes(length);
            this.readerIndex += length;
            return this;
        }

        public virtual IByteBuffer WriteBoolean(bool value)
        {
            this.WriteByte(value ? 1 : 0);
            return this;
        }

        public virtual IByteBuffer WriteByte(int value)
        {
            this.EnsureWritable0(1);
            this._SetByte(this.writerIndex++, value);
            return this;
        }

        public virtual IByteBuffer WriteShort(int value)
        {
            this.EnsureWritable0(2);
            this._SetShort(this.writerIndex, value);
            this.writerIndex += 2;
            return this;
        }

        public virtual IByteBuffer WriteShortLE(int value)
        {
            this.EnsureWritable0(2);
            this._SetShortLE(this.writerIndex, value);
            this.writerIndex += 2;
            return this;
        }

        public IByteBuffer WriteUnsignedShort(ushort value)
        {
            unchecked
            {
                return this.WriteShort((short)value);
            }
        }

        public IByteBuffer WriteUnsignedShortLE(ushort value)
        {
            unchecked
            {
                return this.WriteShortLE((short)value);
            }
        }

        public virtual IByteBuffer WriteMedium(int value)
        {
            this.EnsureWritable0(3);
            this._SetMedium(this.writerIndex, value);
            this.writerIndex += 3;
            return this;
        }

        public virtual IByteBuffer WriteMediumLE(int value)
        {
            this.EnsureWritable0(3);
            this._SetMediumLE(this.writerIndex, value);
            this.writerIndex += 3;
            return this;
        }

        public virtual IByteBuffer WriteInt(int value)
        {
            this.EnsureWritable0(4);
            this._SetInt(this.writerIndex, value);
            this.writerIndex += 4;
            return this;
        }

        public virtual IByteBuffer WriteIntLE(int value)
        {
            this.EnsureWritable0(4);
            this._SetIntLE(this.writerIndex, value);
            this.writerIndex += 4;
            return this;
        }

        public virtual IByteBuffer WriteLong(long value)
        {
            this.EnsureWritable0(8);
            this._SetLong(this.writerIndex, value);
            this.writerIndex += 8;
            return this;
        }

        public virtual IByteBuffer WriteLongLE(long value)
        {
            this.EnsureWritable0(8);
            this._SetLongLE(this.writerIndex, value);
            this.writerIndex += 8;
            return this;
        }

        public virtual IByteBuffer WriteChar(char value)
        {
            this.WriteShort(value);
            return this;
        }

        public virtual IByteBuffer WriteFloat(float value)
        {
            this.WriteInt(ByteBufferUtil.SingleToInt32Bits(value));
            return this;
        }

        public IByteBuffer WriteFloatLE(float value) => this.WriteIntLE(ByteBufferUtil.SingleToInt32Bits(value));

        public virtual IByteBuffer WriteDouble(double value)
        {
            this.WriteLong(BitConverter.DoubleToInt64Bits(value));
            return this;
        }

        public IByteBuffer WriteDoubleLE(double value) => this.WriteLongLE(BitConverter.DoubleToInt64Bits(value));

        public virtual IByteBuffer WriteBytes(byte[] src, int srcIndex, int length)
        {
            this.EnsureWritable(length);
            this.SetBytes(this.writerIndex, src, srcIndex, length);
            this.writerIndex += length;
            return this;
        }

        public virtual IByteBuffer WriteBytes(byte[] src)
        {
            this.WriteBytes(src, 0, src.Length);
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
                ThrowHelper.ThrowIndexOutOfRangeException_ReadableBytes(length, src);
            }
            this.WriteBytes(src, src.ReaderIndex, length);
            src.SetReaderIndex(src.ReaderIndex + length);
            return this;
        }

        public virtual IByteBuffer WriteBytes(IByteBuffer src, int srcIndex, int length)
        {
            this.EnsureWritable(length);
            this.SetBytes(this.writerIndex, src, srcIndex, length);
            this.writerIndex += length;
            return this;
        }

        public virtual async Task WriteBytesAsync(Stream stream, int length, CancellationToken cancellationToken)
        {
            this.EnsureWritable(length);
            if (this.WritableBytes < length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            int writerIdx = this.writerIndex;
            int wrote = await this.SetBytesAsync(writerIdx, stream, length, cancellationToken);

            Contract.Assert(writerIdx == this.writerIndex);
            this.writerIndex = writerIdx + wrote;
        }

        public Task WriteBytesAsync(Stream stream, int length) => this.WriteBytesAsync(stream, length, CancellationToken.None);

        public virtual IByteBuffer WriteZero(int length)
        {
            if (length == 0)
            {
                return this;
            }

            this.EnsureWritable(length);
            int wIndex = this.writerIndex;
            this.CheckIndex0(wIndex, length);

            int nLong = length.RightUShift(3);
            int nBytes = length & 7;
            for (int i = nLong; i > 0; i--)
            {
                this._SetLong(wIndex, 0);
                wIndex += 8;
            }
            if (nBytes == 4)
            {
                this._SetInt(wIndex, 0);
                wIndex += 4;
            }
            else if (nBytes < 4)
            {
                for (int i = nBytes; i > 0; i--)
                {
                    this._SetByte(wIndex, 0);
                    wIndex++;
                }
            }
            else
            {
                this._SetInt(wIndex, 0);
                wIndex += 4;
                for (int i = nBytes - 4; i > 0; i--)
                {
                    this._SetByte(wIndex, 0);
                    wIndex++;
                }
            }

            this.writerIndex = wIndex;
            return this;
        }

        public virtual int WriteCharSequence(ICharSequence sequence, Encoding encoding)
        {
            int written = this.SetCharSequence0(this.writerIndex, sequence, encoding, true);
            this.writerIndex += written;
            return written;
        }

        public virtual int WriteString(string value, Encoding encoding)
        {
            int written = this.SetString0(this.writerIndex, value, encoding, true);
            this.writerIndex += written;
            return written;
        }

        public virtual IByteBuffer Copy() => this.Copy(this.readerIndex, this.ReadableBytes);

        public abstract IByteBuffer Copy(int index, int length);

        public virtual IByteBuffer Duplicate() => new UnpooledDuplicatedByteBuffer(this);

        public virtual IByteBuffer RetainedDuplicate() => (IByteBuffer)this.Duplicate().Retain();

        public virtual IByteBuffer Slice() => this.Slice(this.readerIndex, this.ReadableBytes);

        public virtual IByteBuffer RetainedSlice() => (IByteBuffer)this.Slice().Retain();

        public virtual IByteBuffer Slice(int index, int length) => new UnpooledSlicedByteBuffer(this, index, length);

        public virtual IByteBuffer RetainedSlice(int index, int length) => (IByteBuffer)this.Slice(index, length).Retain();

        public virtual string ToString(Encoding encoding) => this.ToString(this.readerIndex, this.ReadableBytes, encoding);

        public virtual string ToString(int index, int length, Encoding encoding) => ByteBufferUtil.DecodeString(this, index, length, encoding);

        public virtual int IndexOf(int fromIndex, int toIndex, byte value) => ByteBufferUtil.IndexOf(this, fromIndex, toIndex, value);

        public int BytesBefore(byte value) => this.BytesBefore(this.ReaderIndex, this.ReadableBytes, value);

        public int BytesBefore(int length, byte value)
        {
            this.CheckReadableBytes(length);
            return this.BytesBefore(this.ReaderIndex, length, value);
        }

        public virtual int BytesBefore(int index, int length, byte value)
        {
            int endIndex = this.IndexOf(index, index + length, value);
            if (endIndex < 0)
            {
                return -1;
            }

            return endIndex - index;
        }

        public virtual int ForEachByte(IByteProcessor processor)
        {
            this.EnsureAccessible();
            return this.ForEachByteAsc0(this.readerIndex, this.writerIndex, processor);
        }

        public virtual int ForEachByte(int index, int length, IByteProcessor processor)
        {
            this.CheckIndex(index, length);
            return this.ForEachByteAsc0(index, index + length, processor);
        }

        int ForEachByteAsc0(int start, int end, IByteProcessor processor)
        {
            for (; start < end; ++start)
            {
                if (!processor.Process(this._GetByte(start)))
                {
                    return start;
                }
            }

            return -1;
        }

        public virtual int ForEachByteDesc(IByteProcessor processor)
        {
            this.EnsureAccessible();
            return this.ForEachByteDesc0(this.writerIndex - 1, this.readerIndex, processor);
        }

        public virtual int ForEachByteDesc(int index, int length, IByteProcessor processor)
        {
            this.CheckIndex(index, length);
            return this.ForEachByteDesc0(index + length - 1, index, processor);
        }

        int ForEachByteDesc0(int rStart, int rEnd, IByteProcessor processor)
        {
            for (; rStart >= rEnd; --rStart)
            {
                if (!processor.Process(this._GetByte(rStart)))
                {
                    return rStart;
                }
            }

            return -1;
        }

        public override int GetHashCode() => ByteBufferUtil.HashCode(this);

        public sealed override bool Equals(object o) => this.Equals(o as IByteBuffer);

        public virtual bool Equals(IByteBuffer buffer) =>
            ReferenceEquals(this, buffer) || buffer != null && ByteBufferUtil.Equals(this, buffer);

        public virtual int CompareTo(IByteBuffer that) => ByteBufferUtil.Compare(this, that);

        public override string ToString()
        {
            if (this.ReferenceCount == 0)
            {
                return StringUtil.SimpleClassName(this) + "(freed)";
            }

            StringBuilder buf = new StringBuilder()
                .Append(StringUtil.SimpleClassName(this))
                .Append("(ridx: ").Append(this.readerIndex)
                .Append(", widx: ").Append(this.writerIndex)
                .Append(", cap: ").Append(this.Capacity);
            if (this.MaxCapacity != int.MaxValue)
            {
                buf.Append('/').Append(this.MaxCapacity);
            }

            IByteBuffer unwrapped = this.Unwrap();
            if (unwrapped != null)
            {
                buf.Append(", unwrapped: ").Append(unwrapped);
            }
            buf.Append(')');
            return buf.ToString();
        }

        protected void CheckIndex(int index) => this.CheckIndex(index, 1);

        protected internal void CheckIndex(int index, int fieldLength)
        {
            this.EnsureAccessible();
            this.CheckIndex0(index, fieldLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void CheckIndex0(int index, int fieldLength)
        {
            if (MathUtil.IsOutOfBounds(index, fieldLength, this.Capacity))
            {
                ThrowHelper.ThrowIndexOutOfRangeException_Index(index, fieldLength, this.Capacity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void CheckSrcIndex(int index, int length, int srcIndex, int srcCapacity)
        {
            this.CheckIndex(index, length);
            if (MathUtil.IsOutOfBounds(srcIndex, length, srcCapacity))
            {
                ThrowHelper.ThrowIndexOutOfRangeException_SrcIndex(srcIndex, length, srcCapacity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void CheckDstIndex(int index, int length, int dstIndex, int dstCapacity)
        {
            this.CheckIndex(index, length);
            if (MathUtil.IsOutOfBounds(dstIndex, length, dstCapacity))
            {
                ThrowHelper.ThrowIndexOutOfRangeException_DstIndex(dstIndex, length, dstCapacity);
            }
        }

        protected void CheckReadableBytes(int minimumReadableBytes)
        {
            if (minimumReadableBytes < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_MinimumReadableBytes(minimumReadableBytes);
            }

            this.CheckReadableBytes0(minimumReadableBytes);
        }

        protected void CheckNewCapacity(int newCapacity)
        {
            this.EnsureAccessible();
            if (newCapacity < 0 || newCapacity > this.MaxCapacity)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_Capacity(newCapacity, this.MaxCapacity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CheckReadableBytes0(int minimumReadableBytes)
        {
            this.EnsureAccessible();
            if (this.readerIndex > this.writerIndex - minimumReadableBytes)
            {
                ThrowHelper.ThrowIndexOutOfRangeException_ReaderIndex(minimumReadableBytes, this.readerIndex, this.writerIndex, this);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void EnsureAccessible()
        {
            if (CheckAccessible && this.ReferenceCount == 0)
            {
                ThrowHelper.ThrowIllegalReferenceCountException(0);
            }
        }

        protected void SetIndex0(int readerIdx, int writerIdx)
        {
            this.readerIndex = readerIdx;
            this.writerIndex = writerIdx;
        }

        protected void DiscardMarks()
        {
            this.markedReaderIndex = this.markedWriterIndex = 0;
        }

        public abstract int IoBufferCount { get; }

        public ArraySegment<byte> GetIoBuffer() => this.GetIoBuffer(this.readerIndex, this.ReadableBytes);

        public abstract ArraySegment<byte> GetIoBuffer(int index, int length);

        public ArraySegment<byte>[] GetIoBuffers() => this.GetIoBuffers(this.readerIndex, this.ReadableBytes);

        public abstract ArraySegment<byte>[] GetIoBuffers(int index, int length);

        public abstract bool HasArray { get; }

        public abstract byte[] Array { get; }

        public abstract int ArrayOffset { get; }

        public abstract bool HasMemoryAddress { get; }

        public abstract ref byte GetPinnableMemoryAddress();

        public abstract IntPtr AddressOfPinnedMemory();

        public abstract IByteBuffer Unwrap();

        public abstract bool IsDirect { get; }

        public abstract int ReferenceCount { get; }

        public abstract IReferenceCounted Retain();

        public abstract IReferenceCounted Retain(int increment);

        public abstract IReferenceCounted Touch();

        public abstract IReferenceCounted Touch(object hint);

        public abstract bool Release();

        public abstract bool Release(int decrement);
    }
}