// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// Inspired by the Netty ByteBuffer implementation (https://github.com/netty/netty/blob/master/buffer/src/main/java/io/netty/buffer/ByteBuf.java)
    /// 
    /// Provides circular-buffer-esque security around a byte array, allowing reads and writes to occur independently.
    /// 
    /// In general, the <see cref="IByteBuffer"/> guarantees:
    /// 
    /// /// <see cref="ReaderIndex"/> LESS THAN OR EQUAL TO <see cref="WriterIndex"/> LESS THAN OR EQUAL TO <see cref="Capacity"/>.
    /// </summary>
    public interface IByteBuffer : IReferenceCounted
    {
        int Capacity { get; }

        /// <summary>
        /// Expands the capacity of this buffer so long as it is less than <see cref="MaxCapacity"/>.
        /// </summary>
        IByteBuffer AdjustCapacity(int newCapacity);

        int MaxCapacity { get; }

        /// <summary>
        /// The allocator who created this buffer
        /// </summary>
        IByteBufferAllocator Allocator { get; }

        int ReaderIndex { get; }

        int WriterIndex { get; }

        /// <summary>
        /// Sets the <see cref="WriterIndex"/> of this buffer
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">thrown if <see cref="WriterIndex"/> exceeds the length of the buffer</exception>
        IByteBuffer SetWriterIndex(int writerIndex);

        /// <summary>
        /// Sets the <see cref="ReaderIndex"/> of this buffer
        /// </summary>
        /// <exception cref="IndexOutOfRangeException"> thrown if <see cref="ReaderIndex"/> is greater than <see cref="WriterIndex"/> or less than <c>0</c>.</exception>
        IByteBuffer SetReaderIndex(int readerIndex);

        /// <summary>
        /// Sets both indexes
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">thrown if <see cref="WriterIndex"/> or <see cref="ReaderIndex"/> exceeds the length of the buffer</exception>
        IByteBuffer SetIndex(int readerIndex, int writerIndex);

        int ReadableBytes { get; }

        int WritableBytes { get; }

        int MaxWritableBytes { get; }

        /// <summary>
        /// Returns true if <see cref="WriterIndex"/> - <see cref="ReaderIndex"/> is greater than <c>0</c>.
        /// </summary>
        bool IsReadable();

        /// <summary>
        /// Is the buffer readable if and only if the buffer contains equal or more than the specified number of elements
        /// </summary>
        /// <param name="size">The number of elements we would like to read</param>
        bool IsReadable(int size);

        /// <summary>
        /// Returns true if and only if <see cref="Capacity"/> - <see cref="WriterIndex"/> is greater than zero.
        /// </summary>
        bool IsWritable();

        /// <summary>
        /// Returns true if and only if the buffer has enough <see cref="Capacity"/> to accomodate <see cref="size"/> additional bytes.
        /// </summary>
        /// <param name="size">The number of additional elements we would like to write.</param>
        bool IsWritable(int size);

        /// <summary>
        /// Sets the <see cref="WriterIndex"/> and <see cref="ReaderIndex"/> to <c>0</c>. Does not erase any of the data written into the buffer already,
        /// but it will overwrite that data.
        /// </summary>
        IByteBuffer Clear();

        /// <summary>
        /// Marks the current <see cref="ReaderIndex"/> in this buffer. You can reposition the current <see cref="ReaderIndex"/>
        /// to the marked <see cref="ReaderIndex"/> by calling <see cref="ResetReaderIndex"/>.
        /// 
        /// The initial value of the marked <see cref="ReaderIndex"/> is <c>0</c>.
        /// </summary>
        IByteBuffer MarkReaderIndex();

        /// <summary>
        /// Repositions the current <see cref="ReaderIndex"/> to the marked <see cref="ReaderIndex"/> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">is thrown if the current <see cref="WriterIndex"/> is less than the 
        /// marked <see cref="ReaderIndex"/></exception>
        IByteBuffer ResetReaderIndex();

        /// <summary>
        /// Marks the current <see cref="WriterIndex"/> in this buffer. You can reposition the current <see cref="WriterIndex"/>
        /// to the marked <see cref="WriterIndex"/> by calling <see cref="ResetWriterIndex"/>.
        /// 
        /// The initial value of the marked <see cref="WriterIndex"/> is <c>0</c>.
        /// </summary>
        IByteBuffer MarkWriterIndex();

        /// <summary>
        /// Repositions the current <see cref="WriterIndex"/> to the marked <see cref="WriterIndex"/> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">is thrown if the current <see cref="ReaderIndex"/> is greater than the 
        /// marked <see cref="WriterIndex"/></exception>
        IByteBuffer ResetWriterIndex();

        /// <summary>
        /// Discards the bytes between the 0th index and <see cref="ReaderIndex"/>.
        /// 
        /// It moves the bytes between <see cref="ReaderIndex"/> and <see cref="WriterIndex"/> to the 0th index,
        /// and sets <see cref="ReaderIndex"/> and <see cref="WriterIndex"/> to <c>0</c> and <c>oldWriterIndex - oldReaderIndex</c> respectively.
        /// </summary>
        IByteBuffer DiscardReadBytes();

        /// <summary>
        /// Similar to <see cref="DiscardReadBytes"/> except that this method might discard
        /// some, all, or none of read bytes depending on its internal implementation to reduce
        /// overall memory bandwidth consumption at the cost of potentially additional memory
        /// consumption.
        /// </summary>
        IByteBuffer DiscardSomeReadBytes();

        /// <summary>
        /// Makes sure the number of <see cref="WritableBytes"/> is equal to or greater than
        /// the specified value (<see cref="minWritableBytes"/>.) If there is enough writable bytes in this buffer,
        /// the method returns with no side effect. Otherwise, it raises an <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        /// <param name="minWritableBytes">The expected number of minimum writable bytes</param>
        /// <exception cref="IndexOutOfRangeException"> if <see cref="WriterIndex"/> + <see cref="minWritableBytes"/> > <see cref="MaxCapacity"/>.</exception>
        IByteBuffer EnsureWritable(int minWritableBytes);

        /// <summary>
        /// Tries to make sure the number of <see cref="WritableBytes"/>
        /// is equal to or greater than the specified value. Unlike <see cref="EnsureWritable(int)"/>,
        /// this method does not raise an exception but returns a code.
        /// </summary>
        /// <param name="minWritableBytes">the expected minimum number of writable bytes</param>
        /// <param name="force">
        /// When <see cref="WriterIndex"/> + <c>minWritableBytes</c> > <see cref="MaxCapacity"/>:
        /// <ul>
        /// <li><c>true</c> - the capacity of the buffer is expanded to <see cref="MaxCapacity"/></li>
        /// <li><c>false</c> - the capacity of the buffer is unchanged</li>
        /// </ul>
        /// </param>
        /// <returns>
        /// <c>0</c> if the buffer has enough writable bytes, and its capacity is unchanged.
        /// <c>1</c> if the buffer does not have enough bytes, and its capacity is unchanged.
        /// <c>2</c> if the buffer has enough writable bytes, and its capacity has been increased.
        /// <c>3</c> if the buffer does not have enough bytes, but its capacity has been increased to its maximum.
        /// </returns>
        int EnsureWritable(int minWritableBytes, bool force);

        /// <summary>
        /// Gets a boolean at the specified absolute <see cref="index"/> in this buffer.
        /// This method does not modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/>
        /// of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        bool GetBoolean(int index);

        /// <summary>
        /// Gets a byte at the specified absolute <see cref="index"/> in this buffer.
        /// This method does not modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/>
        /// of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        byte GetByte(int index);

        /// <summary>
        /// Gets a short at the specified absolute <see cref="index"/> in this buffer.
        /// This method does not modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/>
        /// of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        short GetShort(int index);

        /// <summary>
        /// Gets an ushort at the specified absolute <see cref="index"/> in this buffer.
        /// This method does not modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/>
        /// of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        ushort GetUnsignedShort(int index);

        /// <summary>
        /// Gets an integer at the specified absolute <see cref="index"/> in this buffer.
        /// This method does not modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/>
        /// of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        int GetInt(int index);

        /// <summary>
        /// Gets an unsigned integer at the specified absolute <see cref="index"/> in this buffer.
        /// This method does not modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/>
        /// of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        uint GetUnsignedInt(int index);

        /// <summary>
        /// Gets a long integer at the specified absolute <see cref="index"/> in this buffer.
        /// This method does not modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/>
        /// of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        long GetLong(int index);

        /// <summary>
        /// Gets a char at the specified absolute <see cref="index"/> in this buffer.
        /// This method does not modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/>
        /// of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        char GetChar(int index);

        /// <summary>
        /// Gets a double at the specified absolute <see cref="index"/> in this buffer.
        /// This method does not modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/>
        /// of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        double GetDouble(int index);

        /// <summary>
        /// Transfers this buffers data to the specified <see cref="destination"/> buffer starting at the specified
        /// absolute <see cref="index"/> until the destination becomes non-writable.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        IByteBuffer GetBytes(int index, IByteBuffer destination);

        /// <summary>
        /// Transfers this buffers data to the specified <see cref="destination"/> buffer starting at the specified
        /// absolute <see cref="index"/> until the destination becomes non-writable.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        IByteBuffer GetBytes(int index, IByteBuffer destination, int length);

        /// <summary>
        /// Transfers this buffers data to the specified <see cref="destination"/> buffer starting at the specified
        /// absolute <see cref="index"/> until the destination becomes non-writable.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        IByteBuffer GetBytes(int index, IByteBuffer destination, int dstIndex, int length);

        /// <summary>
        /// Transfers this buffers data to the specified <see cref="destination"/> buffer starting at the specified
        /// absolute <see cref="index"/> until the destination becomes non-writable.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        IByteBuffer GetBytes(int index, byte[] destination);

        /// <summary>
        /// Transfers this buffers data to the specified <see cref="destination"/> buffer starting at the specified
        /// absolute <see cref="index"/> until the destination becomes non-writable.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        IByteBuffer GetBytes(int index, byte[] destination, int dstIndex, int length);

        ///  <summary>
        ///  Transfers this buffer's data to the specified stream starting at the
        ///  specified absolute <c>index</c>.
        ///  </summary>
        ///  <remarks>
        ///  This method does not modify <c>readerIndex</c> or <c>writerIndex</c> of
        ///  this buffer.
        ///  </remarks>
        /// 
        /// <param name="index">absolute index in this buffer to start getting bytes from</param>
        /// <param name="destination">destination stream</param>
        /// <param name="length">the number of bytes to transfer</param>

        /// <exception cref="IndexOutOfRangeException">
        ///          if the specified <c>index</c> is less than <c>0</c> or
        ///          if <c>index + length</c> is greater than
        ///             <c>this.capacity</c>
        /// </exception> 
        IByteBuffer GetBytes(int index, Stream destination, int length);

        /// <summary>
        /// Sets the specified boolean at the specified absolute <see cref="index"/> in this buffer.
        /// 
        /// This method does not directly modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        IByteBuffer SetBoolean(int index, bool value);

        /// <summary>
        /// Sets the specified byte at the specified absolute <see cref="index"/> in this buffer.
        /// 
        /// This method does not directly modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        IByteBuffer SetByte(int index, int value);

        /// <summary>
        /// Sets the specified short at the specified absolute <see cref="index"/> in this buffer.
        /// 
        /// This method does not directly modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        IByteBuffer SetShort(int index, int value);

        /// <summary>
        /// Sets the specified unsigned short at the specified absolute <see cref="index"/> in this buffer.
        /// 
        /// This method does not directly modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        IByteBuffer SetUnsignedShort(int index, ushort value);

        /// <summary>
        /// Sets the specified integer at the specified absolute <see cref="index"/> in this buffer.
        /// 
        /// This method does not directly modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        IByteBuffer SetInt(int index, int value);

        /// <summary>
        /// Sets the specified unsigned integer at the specified absolute <see cref="index"/> in this buffer.
        /// 
        /// This method does not directly modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        IByteBuffer SetUnsignedInt(int index, uint value);

        /// <summary>
        /// Sets the specified long integer at the specified absolute <see cref="index"/> in this buffer.
        /// 
        /// This method does not directly modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        IByteBuffer SetLong(int index, long value);

        /// <summary>
        /// Sets the specified UTF-16 char at the specified absolute <see cref="index"/> in this buffer.
        /// 
        /// This method does not directly modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        IByteBuffer SetChar(int index, char value);

        /// <summary>
        /// Sets the specified double at the specified absolute <see cref="index"/> in this buffer.
        /// 
        /// This method does not directly modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        IByteBuffer SetDouble(int index, double value);

        /// <summary>
        /// Transfers the <see cref="src"/> byte buffer's contents starting at the specified absolute <see cref="index"/>.
        /// 
        /// This method does not directly modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        IByteBuffer SetBytes(int index, IByteBuffer src);

        /// <summary>
        /// Transfers the <see cref="src"/> byte buffer's contents starting at the specified absolute <see cref="index"/>.
        /// 
        /// This method does not directly modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        IByteBuffer SetBytes(int index, IByteBuffer src, int length);

        /// <summary>
        /// Transfers the <see cref="src"/> byte buffer's contents starting at the specified absolute <see cref="index"/>.
        /// 
        /// This method does not directly modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length);

        /// <summary>
        /// Transfers the <see cref="src"/> byte buffer's contents starting at the specified absolute <see cref="index"/>.
        /// 
        /// This method does not directly modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        IByteBuffer SetBytes(int index, byte[] src);

        /// <summary>
        /// Transfers the <see cref="src"/> byte buffer's contents starting at the specified absolute <see cref="index"/>.
        /// 
        /// This method does not directly modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if the specified <see cref="index"/> is less than <c>0</c> or <c>index + 1</c> greater than <see cref="Capacity"/></exception>
        IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length);

        /// <summary>
        ///     Transfers the content of the specified source stream to this buffer
        ///     starting at the specified absolute {@code index}.
        ///     This method does not modify {@code readerIndex} or {@code writerIndex} of
        ///     this buffer.
        /// </summary>
        /// <param name="index">absolute index in this byte buffer to start writing to</param>
        /// <param name="src"></param>
        /// <param name="length">number of bytes to transfer</param>
        /// <param name="cancellationToken">cancellation token</param>
        /// <returns>the actual number of bytes read in from the specified channel.</returns>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <c>index</c> is less than {@code 0} or
        ///     if <c>index + length</c> is greater than <c>this.capacity</c>
        /// </exception>
        Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken);

        /// <summary>
        /// Gets a boolean at the current <see cref="ReaderIndex"/> and increases the <see cref="ReaderIndex"/>
        /// by <c>1</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes"/> is less than <c>1</c></exception>
        bool ReadBoolean();

        /// <summary>
        /// Gets a byte at the current <see cref="ReaderIndex"/> and increases the <see cref="ReaderIndex"/>
        /// by <c>1</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes"/> is less than <c>1</c></exception>
        byte ReadByte();

        /// <summary>
        /// Gets a short at the current <see cref="ReaderIndex"/> and increases the <see cref="ReaderIndex"/>
        /// by <c>2</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes"/> is less than <c>2</c></exception>
        short ReadShort();

        /// <summary>
        /// Gets an unsigned short at the current <see cref="ReaderIndex"/> and increases the <see cref="ReaderIndex"/>
        /// by <c>2</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes"/> is less than <c>2</c></exception>
        ushort ReadUnsignedShort();

        /// <summary>
        /// Gets an integer at the current <see cref="ReaderIndex"/> and increases the <see cref="ReaderIndex"/>
        /// by <c>4</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes"/> is less than <c>4</c></exception>
        int ReadInt();

        /// <summary>
        /// Gets an unsigned integer at the current <see cref="ReaderIndex"/> and increases the <see cref="ReaderIndex"/>
        /// by <c>4</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes"/> is less than <c>4</c></exception>
        uint ReadUnsignedInt();

        long ReadLong();

        /// <summary>
        /// Gets a 2-byte UTF-16 character at the current <see cref="ReaderIndex"/> and increases the <see cref="ReaderIndex"/>
        /// by <c>2</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes"/> is less than <c>2</c></exception>
        char ReadChar();

        /// <summary>
        /// Gets an 8-byte Decimaling integer at the current <see cref="ReaderIndex"/> and increases the <see cref="ReaderIndex"/>
        /// by <c>8</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes"/> is less than <c>8</c></exception>
        double ReadDouble();

        /// <summary>
        /// Reads <see cref="length"/> bytes from this buffer into a new destination buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes"/> is less than <see cref="length"/></exception>
        IByteBuffer ReadBytes(int length);

        /// <summary>
        /// Transfers bytes from this buffer's data into the specified destination buffer
        /// starting at the curent <see cref="ReaderIndex"/> until the destination becomes
        /// non-writable and increases the <see cref="ReaderIndex"/> by the number of transferred bytes.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="destination.WritableBytes"/> is greater than <see cref="ReadableBytes"/>.</exception>
        IByteBuffer ReadBytes(IByteBuffer destination);

        IByteBuffer ReadBytes(IByteBuffer destination, int length);

        IByteBuffer ReadBytes(IByteBuffer destination, int dstIndex, int length);

        IByteBuffer ReadBytes(byte[] destination);

        IByteBuffer ReadBytes(byte[] destination, int dstIndex, int length);

        IByteBuffer ReadBytes(Stream destination, int length);

        /// <summary>
        /// Increases the current <see cref="ReaderIndex"/> by the specified <see cref="length"/> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException"> if <see cref="length"/> is greater than <see cref="ReadableBytes"/>.</exception>
        IByteBuffer SkipBytes(int length);

        IByteBuffer WriteBoolean(bool value);

        IByteBuffer WriteByte(int value);

        IByteBuffer WriteShort(int value);

        IByteBuffer WriteUnsignedShort(ushort value);

        IByteBuffer WriteInt(int value);

        IByteBuffer WriteUnsignedInt(uint value);

        IByteBuffer WriteLong(long value);

        IByteBuffer WriteChar(char value);

        IByteBuffer WriteDouble(double value);

        IByteBuffer WriteBytes(IByteBuffer src);

        IByteBuffer WriteBytes(IByteBuffer src, int length);

        IByteBuffer WriteBytes(IByteBuffer src, int srcIndex, int length);

        IByteBuffer WriteBytes(byte[] src);

        IByteBuffer WriteBytes(byte[] src, int srcIndex, int length);

        /// <summary>
        /// Flag that indicates if this <see cref="IByteBuffer"/> is backed by a byte array or not
        /// </summary>
        bool HasArray { get; }

        /// <summary>
        /// Grabs the underlying byte array for this buffer
        /// </summary>
        /// <value></value>
        byte[] Array { get; }

        /// <summary>
        /// Converts the readable contents of the buffer into an array.
        /// 
        /// Does not affect the <see cref="ReaderIndex"/> or <see cref="WriterIndex"/> of the <see cref="IByteBuffer"/>
        /// </summary>
        byte[] ToArray();

        /// <summary>
        /// Creates a deep clone of the existing byte array and returns it
        /// </summary>
        IByteBuffer Duplicate();

        /// <summary>
        /// Unwraps a nested buffer
        /// </summary>
        IByteBuffer Unwrap();

        ByteOrder Order { get; }

        IByteBuffer WithOrder(ByteOrder order);

        IByteBuffer Copy();

        IByteBuffer Copy(int index, int length);

        IByteBuffer Slice();

        IByteBuffer Slice(int index, int length);

        int ArrayOffset { get; }

        IByteBuffer ReadSlice(int length);

        Task WriteBytesAsync(Stream stream, int length);

        Task WriteBytesAsync(Stream stream, int length, CancellationToken cancellationToken);

        string ToString(Encoding encoding);

        string ToString(int index, int length, Encoding encoding);

        /// <summary>
        /// Iterates over the readable bytes of this buffer with the specified <c>processor</c> in ascending order.
        /// </summary>
        /// <returns><c>1</c> if the processor iterated to or beyond the end of the readable bytes.
        /// The last-visited index If the <see cref="ByteProcessor.Process(byte)"/> returned <c>false</c>.
        /// </returns>
        /// <param name="processor">Processor.</param>
        int ForEachByte(ByteProcessor processor);

        /// <summary>
        /// Iterates over the specified area of this buffer with the specified {@code processor} in ascending order.
        /// (i.e. {@code index}, {@code (index + 1)},  .. {@code (index + length - 1)})
        /// </summary>
        /// <returns>{@code -1} if the processor iterated to or beyond the end of the specified area.
        /// The last-visited index If the {@link ByteProcessor#process(byte)} returned {@code false}.
        /// </returns>
        /// <param name="index">Index.</param>
        /// <param name="length">Length.</param>
        /// <param name="processor">Processor.</param>
        int ForEachByte(int index, int length, ByteProcessor processor);

        /// <summary>
        /// Iterates over the readable bytes of this buffer with the specified {@code processor} in descending order.
        /// </summary>
        /// <returns>{@code -1} if the processor iterated to or beyond the beginning of the readable bytes.
        /// The last-visited index If the {@link ByteProcessor#process(byte)} returned {@code false}.
        /// </returns>
        /// <param name="processor">Processor.</param>
        int ForEachByteDesc(ByteProcessor processor);

        /// <summary>
        /// Iterates over the specified area of this buffer with the specified {@code processor} in descending order.
        /// (i.e. {@code (index + length - 1)}, {@code (index + length - 2)}, ... {@code index})
        /// </summary>
        /// <returns>{@code -1} if the processor iterated to or beyond the beginning of the specified area.
        /// The last-visited index If the {@link ByteProcessor#process(byte)} returned {@code false}.
        /// </returns>
        /// <param name="index">Index.</param>
        /// <param name="length">Length.</param>
        /// <param name="processor">Processor.</param>
        int ForEachByteDesc(int index, int length, ByteProcessor processor);
    }
}