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
    ///     Inspired by the Netty ByteBuffer implementation
    ///     (https://github.com/netty/netty/blob/master/buffer/src/main/java/io/netty/buffer/ByteBuf.java)
    ///     Provides circular-buffer-esque security around a byte array, allowing reads and writes to occur independently.
    ///     In general, the <see cref="T:DotNetty.Buffers.IByteBuffer" /> guarantees:
    ///     /// <see cref="P:DotNetty.Buffers.IByteBuffer.ReaderIndex" /> LESS THAN OR EQUAL TO <see cref="P:DotNetty.Buffers.IByteBuffer.WriterIndex" /> LESS THAN OR EQUAL TO
    ///     <see cref="P:DotNetty.Buffers.IByteBuffer.Capacity" />.
    /// </summary>
    public interface IByteBuffer : IReferenceCounted, IComparable<IByteBuffer>, IEquatable<IByteBuffer>
    {
        int Capacity { get; }

        /// <summary>
        ///     Expands the capacity of this buffer so long as it is less than <see cref="MaxCapacity" />.
        /// </summary>
        IByteBuffer AdjustCapacity(int newCapacity);

        int MaxCapacity { get; }

        /// <summary>
        ///     The allocator who created this buffer
        /// </summary>
        IByteBufferAllocator Allocator { get; }

        bool IsDirect { get; }

        int ReaderIndex { get; }

        int WriterIndex { get; }

        /// <summary>
        ///     Sets the <see cref="WriterIndex" /> of this buffer
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">thrown if <see cref="WriterIndex" /> exceeds the length of the buffer</exception>
        IByteBuffer SetWriterIndex(int writerIndex);

        /// <summary>
        ///     Sets the <see cref="ReaderIndex" /> of this buffer
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     thrown if <see cref="ReaderIndex" /> is greater than
        ///     <see cref="WriterIndex" /> or less than <c>0</c>.
        /// </exception>
        IByteBuffer SetReaderIndex(int readerIndex);

        /// <summary>
        ///     Sets both indexes
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     thrown if <see cref="WriterIndex" /> or <see cref="ReaderIndex" /> exceeds
        ///     the length of the buffer
        /// </exception>
        IByteBuffer SetIndex(int readerIndex, int writerIndex);

        int ReadableBytes { get; }

        int WritableBytes { get; }

        int MaxWritableBytes { get; }

        /// <summary>
        ///     Returns true if <see cref="WriterIndex" /> - <see cref="ReaderIndex" /> is greater than <c>0</c>.
        /// </summary>
        bool IsReadable();

        /// <summary>
        ///     Is the buffer readable if and only if the buffer contains equal or more than the specified number of elements
        /// </summary>
        /// <param name="size">The number of elements we would like to read</param>
        bool IsReadable(int size);

        /// <summary>
        ///     Returns true if and only if <see cref="Capacity" /> - <see cref="WriterIndex" /> is greater than zero.
        /// </summary>
        bool IsWritable();

        /// <summary>
        ///     Returns true if and only if the buffer has enough <see cref="Capacity" /> to accomodate <paramref name="size" />
        ///     additional bytes.
        /// </summary>
        /// <param name="size">The number of additional elements we would like to write.</param>
        bool IsWritable(int size);

        /// <summary>
        ///     Sets the <see cref="WriterIndex" /> and <see cref="ReaderIndex" /> to <c>0</c>. Does not erase any of the data
        ///     written into the buffer already,
        ///     but it will overwrite that data.
        /// </summary>
        IByteBuffer Clear();

        /// <summary>
        ///     Marks the current <see cref="ReaderIndex" /> in this buffer. You can reposition the current
        ///     <see cref="ReaderIndex" />
        ///     to the marked <see cref="ReaderIndex" /> by calling <see cref="ResetReaderIndex" />.
        ///     The initial value of the marked <see cref="ReaderIndex" /> is <c>0</c>.
        /// </summary>
        IByteBuffer MarkReaderIndex();

        /// <summary>
        ///     Repositions the current <see cref="ReaderIndex" /> to the marked <see cref="ReaderIndex" /> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     is thrown if the current <see cref="WriterIndex" /> is less than the
        ///     marked <see cref="ReaderIndex" />
        /// </exception>
        IByteBuffer ResetReaderIndex();

        /// <summary>
        ///     Marks the current <see cref="WriterIndex" /> in this buffer. You can reposition the current
        ///     <see cref="WriterIndex" />
        ///     to the marked <see cref="WriterIndex" /> by calling <see cref="ResetWriterIndex" />.
        ///     The initial value of the marked <see cref="WriterIndex" /> is <c>0</c>.
        /// </summary>
        IByteBuffer MarkWriterIndex();

        /// <summary>
        ///     Repositions the current <see cref="WriterIndex" /> to the marked <see cref="WriterIndex" /> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     is thrown if the current <see cref="ReaderIndex" /> is greater than the
        ///     marked <see cref="WriterIndex" />
        /// </exception>
        IByteBuffer ResetWriterIndex();

        /// <summary>
        ///     Discards the bytes between the 0th index and <see cref="ReaderIndex" />.
        ///     It moves the bytes between <see cref="ReaderIndex" /> and <see cref="WriterIndex" /> to the 0th index,
        ///     and sets <see cref="ReaderIndex" /> and <see cref="WriterIndex" /> to <c>0</c> and
        ///     <c>oldWriterIndex - oldReaderIndex</c> respectively.
        /// </summary>
        IByteBuffer DiscardReadBytes();

        /// <summary>
        ///     Similar to <see cref="DiscardReadBytes" /> except that this method might discard
        ///     some, all, or none of read bytes depending on its internal implementation to reduce
        ///     overall memory bandwidth consumption at the cost of potentially additional memory
        ///     consumption.
        /// </summary>
        IByteBuffer DiscardSomeReadBytes();

        /// <summary>
        ///     Makes sure the number of <see cref="WritableBytes" /> is equal to or greater than
        ///     the specified value (<paramref name="minWritableBytes" />.) If there is enough writable bytes in this buffer,
        ///     the method returns with no side effect. Otherwise, it raises an <see cref="ArgumentOutOfRangeException" />.
        /// </summary>
        /// <param name="minWritableBytes">The expected number of minimum writable bytes</param>
        /// <exception cref="IndexOutOfRangeException">
        ///     if <see cref="WriterIndex" /> + <paramref name="minWritableBytes" /> >
        ///     <see cref="MaxCapacity" />.
        /// </exception>
        IByteBuffer EnsureWritable(int minWritableBytes);

        /// <summary>
        ///     Tries to make sure the number of <see cref="WritableBytes" />
        ///     is equal to or greater than the specified value. Unlike <see cref="EnsureWritable(int)" />,
        ///     this method does not raise an exception but returns a code.
        /// </summary>
        /// <param name="minWritableBytes">the expected minimum number of writable bytes</param>
        /// <param name="force">
        ///     When <see cref="WriterIndex" /> + <c>minWritableBytes</c> > <see cref="MaxCapacity" />:
        ///     <ul>
        ///         <li><c>true</c> - the capacity of the buffer is expanded to <see cref="MaxCapacity" /></li>
        ///         <li><c>false</c> - the capacity of the buffer is unchanged</li>
        ///     </ul>
        /// </param>
        /// <returns>
        ///     <c>0</c> if the buffer has enough writable bytes, and its capacity is unchanged.
        ///     <c>1</c> if the buffer does not have enough bytes, and its capacity is unchanged.
        ///     <c>2</c> if the buffer has enough writable bytes, and its capacity has been increased.
        ///     <c>3</c> if the buffer does not have enough bytes, but its capacity has been increased to its maximum.
        /// </returns>
        int EnsureWritable(int minWritableBytes, bool force);

        /// <summary>
        ///     Gets a boolean at the specified absolute <paramref name="index" /> in this buffer.
        ///     This method does not modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" />
        ///     of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 1</c> greater than <see cref="Capacity" />
        /// </exception>
        bool GetBoolean(int index);

        /// <summary>
        ///     Gets a byte at the specified absolute <paramref name="index" /> in this buffer.
        ///     This method does not modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" />
        ///     of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 1</c> greater than <see cref="Capacity" />
        /// </exception>
        byte GetByte(int index);

        /// <summary>
        ///     Gets a short at the specified absolute <paramref name="index" /> in this buffer.
        ///     This method does not modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" />
        ///     of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 2</c> greater than <see cref="Capacity" />
        /// </exception>
        short GetShort(int index);

        /// <summary>
        ///     Gets a short at the specified absolute <paramref name="index" /> in this buffer 
        ///     in Little Endian Byte Order. This method does not modify <see cref="ReaderIndex" /> 
        ///     or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 2</c> greater than <see cref="Capacity" />
        /// </exception>
        short GetShortLE(int index);

        /// <summary>
        ///     Gets an ushort at the specified absolute <paramref name="index" /> in this buffer.
        ///     This method does not modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" />
        ///     of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 2</c> greater than <see cref="Capacity" />
        /// </exception>
        ushort GetUnsignedShort(int index);

        /// <summary>
        ///     Gets an ushort at the specified absolute <paramref name="index" /> in this buffer 
        ///     in Little Endian Byte Order. This method does not modify <see cref="ReaderIndex" /> 
        ///     or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 2</c> greater than <see cref="Capacity" />
        /// </exception>
        ushort GetUnsignedShortLE(int index);

        /// <summary>
        ///     Gets an integer at the specified absolute <paramref name="index" /> in this buffer.
        ///     This method does not modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" />
        ///     of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 4</c> greater than <see cref="Capacity" />
        /// </exception>
        int GetInt(int index);

        /// <summary>
        ///     Gets an integer at the specified absolute <paramref name="index" /> in this buffer
        ///     in Little Endian Byte Order. This method does not modify <see cref="ReaderIndex" /> 
        ///     or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 4</c> greater than <see cref="Capacity" />
        /// </exception>
        int GetIntLE(int index);

        /// <summary>
        ///     Gets an unsigned integer at the specified absolute <paramref name="index" /> in this buffer.
        ///     This method does not modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" />
        ///     of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 4</c> greater than <see cref="Capacity" />
        /// </exception>
        uint GetUnsignedInt(int index);

        /// <summary>
        ///     Gets an unsigned integer at the specified absolute <paramref name="index" /> in this buffer
        ///     in Little Endian Byte Order. This method does not modify <see cref="ReaderIndex" /> 
        ///     or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 4</c> greater than <see cref="Capacity" />
        /// </exception>
        uint GetUnsignedIntLE(int index);

        /// <summary>
        ///     Gets a long integer at the specified absolute <paramref name="index" /> in this buffer.
        ///     This method does not modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" />
        ///     of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 8</c> greater than <see cref="Capacity" />
        /// </exception>
        long GetLong(int index);

        /// <summary>
        ///     Gets a long integer at the specified absolute <paramref name="index" /> in this buffer
        ///     in Little Endian Byte Order. This method does not modify <see cref="ReaderIndex" /> or 
        ///     <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 8</c> greater than <see cref="Capacity" />
        /// </exception>
        long GetLongLE(int index);

        /// <summary>
        ///     Gets a 24-bit medium integer at the specified absolute index in this buffer.
        ///     This method does not modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" />
        ///     of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <param name="index"/>  is less than <c>0</c> or
        ///     <c>index + 3</c> greater than <see cref="Capacity" />
        /// </exception>
        int GetMedium(int index);

        /// <summary>
        ///     Gets a 24-bit medium integer at the specified absolute index in this buffer
        ///     in Little Endian Byte Order. This method does not modify <see cref="ReaderIndex" /> 
        ///     or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <param name="index"/> is less than <c>0</c> or
        ///     <c>index + 3</c> greater than <see cref="Capacity" />
        /// </exception>
        int GetMediumLE(int index);

        /// <summary>
        ///     Gets an unsigned 24-bit medium integer at the specified absolute index in this buffer.
        ///     This method does not modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" />
        ///     of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <param name="index"/> is less than <c>0</c> or
        ///     <c>index + 3</c> greater than <see cref="Capacity" />
        /// </exception>
        int GetUnsignedMedium(int index);

        /// <summary>
        ///     Gets an unsigned 24-bit medium integer at the specified absolute index in this buffer
        ///     in Little Endian Byte Order. This method does not modify <see cref="ReaderIndex" /> 
        ///     or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <param name="index"/> is less than <c>0</c> or
        ///     <c>index + 3</c> greater than <see cref="Capacity" />
        /// </exception>
        int GetUnsignedMediumLE(int index);

        /// <summary>
        ///     Gets a char at the specified absolute <paramref name="index" /> in this buffer.
        ///     This method does not modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" />
        ///     of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 2</c> greater than <see cref="Capacity" />
        /// </exception>
        char GetChar(int index);

        /// <summary>
        ///     Gets a float at the specified absolute <paramref name="index"/> in this buffer.
        ///     This method does not modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" />
        ///     of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index"/> is less than <c>0</c> or
        ///     <c>index + 4</c> greater than <see cref="Capacity" />
        /// </exception>
        float GetFloat(int index);

        /// <summary>
        ///     Gets a float at the specified absolute <paramref name="index"/> in this buffer
        ///     in Little Endian Byte Order. This method does not modify <see cref="ReaderIndex" /> 
        ///     or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index"/> is less than <c>0</c> or
        ///     <c>index + 4</c> greater than <see cref="Capacity" />
        /// </exception>
        float GetFloatLE(int index);

        /// <summary>
        ///     Gets a double at the specified absolute <paramref name="index" /> in this buffer.
        ///     This method does not modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" />
        ///     of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 8</c> greater than <see cref="Capacity" />
        /// </exception>
        double GetDouble(int index);

        /// <summary>
        ///     Gets a double at the specified absolute <paramref name="index" /> in this buffer
        ///     in Little Endian Byte Order. This method does not modify <see cref="ReaderIndex" /> 
        ///     or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 8</c> greater than <see cref="Capacity" />
        /// </exception>
        double GetDoubleLE(int index);

        /// <summary>
        ///     Transfers this buffers data to the specified <paramref name="destination" /> buffer starting at the specified
        ///     absolute <paramref name="index" /> until the destination becomes non-writable.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 1</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer GetBytes(int index, IByteBuffer destination);

        /// <summary>
        ///     Transfers this buffers data to the specified <paramref name="destination" /> buffer starting at the specified
        ///     absolute <paramref name="index" /> until the destination becomes non-writable.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 1</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer GetBytes(int index, IByteBuffer destination, int length);

        /// <summary>
        ///     Transfers this buffers data to the specified <paramref name="destination" /> buffer starting at the specified
        ///     absolute <paramref name="index" /> until the destination becomes non-writable.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 1</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer GetBytes(int index, IByteBuffer destination, int dstIndex, int length);

        /// <summary>
        ///     Transfers this buffers data to the specified <paramref name="destination" /> buffer starting at the specified
        ///     absolute <paramref name="index" /> until the destination becomes non-writable.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 1</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer GetBytes(int index, byte[] destination);

        /// <summary>
        ///     Transfers this buffers data to the specified <paramref name="destination" /> buffer starting at the specified
        ///     absolute <paramref name="index" /> until the destination becomes non-writable.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 1</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer GetBytes(int index, byte[] destination, int dstIndex, int length);

        /// <summary>
        ///     Transfers this buffer's data to the specified stream starting at the
        ///     specified absolute <c>index</c>.
        /// </summary>
        /// <remarks>
        ///     This method does not modify <c>readerIndex</c> or <c>writerIndex</c> of
        ///     this buffer.
        /// </remarks>
        /// <param name="index">absolute index in this buffer to start getting bytes from</param>
        /// <param name="destination">destination stream</param>
        /// <param name="length">the number of bytes to transfer</param>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <c>index</c> is less than <c>0</c> or
        ///     if <c>index + length</c> is greater than
        ///     <c>this.capacity</c>
        /// </exception>
        IByteBuffer GetBytes(int index, Stream destination, int length);


        ICharSequence GetCharSequence(int index, int length, Encoding encoding);

        /// <summary>
        ///     Gets a string with the given length at the given index.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="length">length the length to read</param>
        /// <param name="encoding">charset that should be use</param>
        /// <returns>the string value.</returns>
        /// <exception cref="IndexOutOfRangeException">
        ///     if length is greater than readable bytes.
        /// </exception>
        string GetString(int index, int length, Encoding encoding);

        /// <summary>
        ///     Sets the specified boolean at the specified absolute <paramref name="index" /> in this buffer.
        ///     This method does not directly modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 1</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetBoolean(int index, bool value);

        /// <summary>
        ///     Sets the specified byte at the specified absolute <paramref name="index" /> in this buffer.
        ///     This method does not directly modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 1</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetByte(int index, int value);

        /// <summary>
        ///     Sets the specified short at the specified absolute <paramref name="index" /> in this buffer.
        ///     This method does not directly modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 2</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetShort(int index, int value);

        /// <summary>
        ///     Sets the specified short at the specified absolute <paramref name="index" /> in this buffer
        ///     in the Little Endian Byte Order. This method does not directly modify <see cref="ReaderIndex" /> 
        ///     or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 2</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetShortLE(int index, int value);

        /// <summary>
        ///     Sets the specified unsigned short at the specified absolute <paramref name="index" /> in this buffer.
        ///     This method does not directly modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 2</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetUnsignedShort(int index, ushort value);

        /// <summary>
        ///     Sets the specified unsigned short at the specified absolute <paramref name="index" /> in this buffer
        ///     in the Little Endian Byte Order. This method does not directly modify <see cref="ReaderIndex" /> 
        ///     or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 2</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetUnsignedShortLE(int index, ushort value);

        /// <summary>
        ///     Sets the specified integer at the specified absolute <paramref name="index" /> in this buffer.
        ///     This method does not directly modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 4</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetInt(int index, int value);

        /// <summary>
        ///     Sets the specified integer at the specified absolute <paramref name="index" /> in this buffer
        ///     in the Little Endian Byte Order. This method does not directly modify <see cref="ReaderIndex" /> 
        ///     or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 4</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetIntLE(int index, int value);

        /// <summary>
        ///     Sets the specified unsigned integer at the specified absolute <paramref name="index" /> in this buffer.
        ///     This method does not directly modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 4</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetUnsignedInt(int index, uint value);

        /// <summary>
        ///     Sets the specified unsigned integer at the specified absolute <paramref name="index" /> in this buffer
        ///     in the Little Endian Byte Order. This method does not directly modify <see cref="ReaderIndex" /> or 
        ///     <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 4</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetUnsignedIntLE(int index, uint value);

        /// <summary>
        ///     Sets the specified 24-bit medium integer at the specified absolute <paramref name="index" /> in this buffer.
        ///     Note that the most significant byte is ignored in the specified value.
        ///     This method does not directly modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 3</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetMedium(int index, int value);

        /// <summary>
        ///     Sets the specified 24-bit medium integer at the specified absolute <paramref name="index" /> in this buffer.
        ///     Note that the most significant byte is ignored in the specified value.
        ///     This method does not directly modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 3</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetMediumLE(int index, int value);

        /// <summary>
        ///     Sets the specified long integer at the specified absolute <paramref name="index" /> in this buffer.
        ///     This method does not directly modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 8</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetLong(int index, long value);

        /// <summary>
        ///     Sets the specified long integer at the specified absolute <paramref name="index" /> in this buffer
        ///     in the Little Endian Byte Order. This method does not directly modify <see cref="ReaderIndex" /> or 
        ///     <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 8</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetLongLE(int index, long value);

        /// <summary>
        ///     Sets the specified UTF-16 char at the specified absolute <paramref name="index" /> in this buffer.
        ///     This method does not directly modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 2</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetChar(int index, char value);

        /// <summary>
        ///     Sets the specified double at the specified absolute <paramref name="index" /> in this buffer.
        ///     This method does not directly modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 8</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetDouble(int index, double value);

        /// <summary>
        ///     Sets the specified float at the specified absolute <paramref name="index" /> in this buffer.
        ///     This method does not directly modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 4</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetFloat(int index, float value);

        /// <summary>
        ///     Sets the specified float at the specified absolute <paramref name="index" /> in this buffer
        ///     in Little Endian Byte Order. This method does not directly modify <see cref="ReaderIndex" /> 
        ///     or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 4</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetDoubleLE(int index, double value);

        /// <summary>
        ///     Sets the specified float at the specified absolute <paramref name="index" /> in this buffer
        ///     in Little Endian Byte Order. This method does not directly modify <see cref="ReaderIndex" /> 
        ///     or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c>index + 4</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetFloatLE(int index, float value);

        /// <summary>
        ///     Transfers the <paramref name="src" /> byte buffer's contents starting at the specified absolute <paramref name="index" />.
        ///     This method does not directly modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c><paramref name="index"/> + <paramref name="src"/>.ReadableBytes</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetBytes(int index, IByteBuffer src);

        /// <summary>
        ///     Transfers the <paramref name="src" /> byte buffer's contents starting at the specified absolute <paramref name="index" />.
        ///     This method does not directly modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index"/> is less than <c>0</c> or
        ///     <paramref name="length"/> is less than <c>0</c> or
        ///     <c><paramref name="index"/> + <paramref name="length"/></c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetBytes(int index, IByteBuffer src, int length);

        /// <summary>
        ///     Transfers the <paramref name="src" /> byte buffer's contents starting at the specified absolute <paramref name="index" />.
        ///     This method does not directly modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index"/> is less than <c>0</c> or
        ///     <paramref name="srcIndex"/> is less than <c>0</c> or
        ///     <paramref name="length"/> is less than <c>0</c> or
        ///     <c><paramref name="index"/> + <paramref name="length"/></c> greater than <see cref="Capacity" /> or
        ///     <c><paramref name="srcIndex"/> + <paramref name="length"/></c> greater than <c><paramref name="src" />.Capacity</c>
        /// </exception>
        IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length);

        /// <summary>
        ///     Transfers the <paramref name="src" /> byte buffer's contents starting at the specified absolute <paramref name="index" />.
        ///     This method does not directly modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index" /> is less than <c>0</c> or
        ///     <c><paramref name="index"/> + <paramref name="src"/>.Length</c> greater than <see cref="Capacity" />
        /// </exception>
        IByteBuffer SetBytes(int index, byte[] src);

        /// <summary>
        ///     Transfers the <paramref name="src" /> byte buffer's contents starting at the specified absolute <paramref name="index" />.
        ///     This method does not directly modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <paramref name="index"/> is less than <c>0</c> or
        ///     <paramref name="srcIndex"/> is less than <c>0</c> or
        ///     <paramref name="length"/> is less than <c>0</c> or
        ///     <c><paramref name="index"/> + <paramref name="length"/></c> greater than <see cref="Capacity" /> or
        ///     <c><paramref name="srcIndex"/> + <paramref name="length"/></c> greater than <c><paramref name="src" />.Length</c>
        /// </exception>
        IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length);

        /// <summary>
        ///     Transfers the content of the specified source stream to this buffer
        ///     starting at the specified absolute <paramref name="index"/>.
        ///     This method does not modify <see cref="ReaderIndex"/> or <see cref="WriterIndex"/> of
        ///     this buffer.
        /// </summary>
        /// <param name="index">absolute index in this byte buffer to start writing to</param>
        /// <param name="src"></param>
        /// <param name="length">number of bytes to transfer</param>
        /// <param name="cancellationToken">cancellation token</param>
        /// <returns>the actual number of bytes read in from the specified channel.</returns>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified <c>index</c> is less than <c>0</c> or
        ///     if <c>index + length</c> is greater than <c>this.capacity</c>
        /// </exception>
        Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken);

        /// <summary>
        ///     Fills this buffer with NULL (0x00) starting at the specified
        ///     absolute index. This method does not modify reader index
        ///     or writer index of this buffer
        /// </summary>
        /// <param name="index">absolute index in this byte buffer to start writing to</param>
        /// <param name="length">length the number of <tt>NUL</tt>s to write to the buffer</param>
        /// <exception cref="IndexOutOfRangeException">
        ///     if the specified index is less than 0 or if index + length
        ///     is greater than capacity.
        /// </exception>
        IByteBuffer SetZero(int index, int length);

        int SetCharSequence(int index, ICharSequence sequence, Encoding encoding);

        /// <summary>
        ///     Writes the specified string at the current writer index and increases
        ///     the  writer index by the written bytes.
        /// </summary>
        /// <param name="index">Index on which the string should be written</param>
        /// <param name="value">The string value.</param>
        /// <param name="encoding">Encoding that should be used.</param>
        /// <returns>The written number of bytes.</returns>
        /// <exception cref="IndexOutOfRangeException">
        ///    if writable bytes is not large enough to write the whole string.
        /// </exception>
        int SetString(int index, string value, Encoding encoding);

        /// <summary>
        ///     Gets a boolean at the current <see cref="ReaderIndex" /> and increases the <see cref="ReaderIndex" />
        ///     by <c>1</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>1</c></exception>
        bool ReadBoolean();

        /// <summary>
        ///     Gets a byte at the current <see cref="ReaderIndex" /> and increases the <see cref="ReaderIndex" />
        ///     by <c>1</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>1</c></exception>
        byte ReadByte();

        /// <summary>
        ///     Gets a short at the current <see cref="ReaderIndex" /> and increases the <see cref="ReaderIndex" />
        ///     by <c>2</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>2</c></exception>
        short ReadShort();

        /// <summary>
        ///     Gets a short at the current <see cref="ReaderIndex" /> in the Little Endian Byte Order and increases 
        ///     the <see cref="ReaderIndex" /> by <c>2</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>2</c></exception>
        short ReadShortLE();

        /// <summary>
        ///     Gets a 24-bit medium integer at the current <see cref="ReaderIndex" /> and increases the <see cref="ReaderIndex" />
        ///     by <c>3</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>3</c></exception>
        int ReadMedium();

        /// <summary>
        ///     Gets a 24-bit medium integer at the current <see cref="ReaderIndex" /> in the Little Endian Byte Order and 
        ///     increases the <see cref="ReaderIndex" /> by <c>3</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>3</c></exception>
        int ReadMediumLE();

        /// <summary>
        ///     Gets an unsigned 24-bit medium integer at the current <see cref="ReaderIndex" /> and increases the <see cref="ReaderIndex" />
        ///     by <c>3</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>3</c></exception>
        int ReadUnsignedMedium();

        /// <summary>
        ///     Gets an unsigned 24-bit medium integer at the current <see cref="ReaderIndex" /> in the Little Endian Byte Order 
        ///     and increases the <see cref="ReaderIndex" /> by <c>3</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>3</c></exception>
        int ReadUnsignedMediumLE();

        /// <summary>
        ///     Gets an unsigned short at the current <see cref="ReaderIndex" /> and increases the <see cref="ReaderIndex" />
        ///     by <c>2</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>2</c></exception>
        ushort ReadUnsignedShort();

        /// <summary>
        ///     Gets an unsigned short at the current <see cref="ReaderIndex" /> in the Little Endian Byte Order and 
        ///     increases the <see cref="ReaderIndex" /> by <c>2</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>2</c></exception>
        ushort ReadUnsignedShortLE();

        /// <summary>
        ///     Gets an integer at the current <see cref="ReaderIndex" /> and increases the <see cref="ReaderIndex" />
        ///     by <c>4</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>4</c></exception>
        int ReadInt();

        /// <summary>
        ///     Gets an integer at the current <see cref="ReaderIndex" /> in the Little Endian Byte Order and increases 
        ///     the <see cref="ReaderIndex" />  by <c>4</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>4</c></exception>
        int ReadIntLE();

        /// <summary>
        ///     Gets an unsigned integer at the current <see cref="ReaderIndex" /> and increases the <see cref="ReaderIndex" />
        ///     by <c>4</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>4</c></exception>
        uint ReadUnsignedInt();

        /// <summary>
        ///     Gets an unsigned integer at the current <see cref="ReaderIndex" /> in the Little Endian Byte Order and
        ///     increases the <see cref="ReaderIndex" /> by <c>4</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>4</c></exception>
        uint ReadUnsignedIntLE();

        /// <summary>
        ///     Gets an long at the current <see cref="ReaderIndex" /> and increases the <see cref="ReaderIndex" /> 
        ///     by <c>8</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>4</c></exception>
        long ReadLong();

        /// <summary>
        ///     Gets an long at the current <see cref="ReaderIndex" /> in the Little Endian Byte Order and
        ///     increases the <see cref="ReaderIndex" /> by <c>8</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>4</c></exception>
        long ReadLongLE();

        /// <summary>
        ///     Gets a 2-byte UTF-16 character at the current <see cref="ReaderIndex" /> and increases the
        ///     <see cref="ReaderIndex" />
        ///     by <c>2</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>2</c></exception>
        char ReadChar();

        /// <summary>
        ///     Gets an 8-byte Decimaling integer at the current <see cref="ReaderIndex" /> and increases the
        ///     <see cref="ReaderIndex" />
        ///     by <c>8</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>8</c></exception>
        double ReadDouble();

        /// <summary>
        ///     Gets an 8-byte Decimaling integer at the current <see cref="ReaderIndex" /> and increases the
        ///     <see cref="ReaderIndex" /> by <c>8</c> in this buffer in Little Endian Byte Order.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>8</c></exception>
        double ReadDoubleLE();

        /// <summary>
        ///     Gets an 4-byte Decimaling integer at the current <see cref="ReaderIndex" /> and increases the
        ///     <see cref="ReaderIndex" />
        ///     by <c>4</c> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>4</c></exception>
        float ReadFloat();

        /// <summary>
        ///     Gets an 4-byte Decimaling integer at the current <see cref="ReaderIndex" /> and increases the
        ///     <see cref="ReaderIndex" /> by <c>4</c> in this buffer in Little Endian Byte Order.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">if <see cref="ReadableBytes" /> is less than <c>4</c></exception>
        float ReadFloatLE();

        /// <summary>
        ///     Reads <paramref name="length" /> bytes from this buffer into a new destination buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if <see cref="ReadableBytes" /> is less than <paramref name="length" />
        /// </exception>
        IByteBuffer ReadBytes(int length);

        /// <summary>
        ///     Transfers bytes from this buffer's data into the specified destination buffer
        ///     starting at the curent <see cref="ReaderIndex" /> until the destination becomes
        ///     non-writable and increases the <see cref="ReaderIndex" /> by the number of transferred bytes.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        ///     if <c>destination.<see cref="WritableBytes" /></c> is greater than
        ///     <see cref="ReadableBytes" />.
        /// </exception>
        IByteBuffer ReadBytes(IByteBuffer destination);

        IByteBuffer ReadBytes(IByteBuffer destination, int length);

        IByteBuffer ReadBytes(IByteBuffer destination, int dstIndex, int length);

        IByteBuffer ReadBytes(byte[] destination);

        IByteBuffer ReadBytes(byte[] destination, int dstIndex, int length);

        IByteBuffer ReadBytes(Stream destination, int length);

        ICharSequence ReadCharSequence(int length, Encoding encoding);

        /// <summary>
        ///     Gets a string with the given length at the current reader index
        ///     and increases the reader index by the given length.
        /// </summary>
        /// <param name="length">The length to read</param>
        /// <param name="encoding">Encoding that should be used</param>
        /// <returns>The string value</returns>
        string ReadString(int length, Encoding encoding);

        /// <summary>
        ///     Increases the current <see cref="ReaderIndex" /> by the specified <paramref name="length" /> in this buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException"> if <paramref name="length" /> is greater than <see cref="ReadableBytes" />.</exception>
        IByteBuffer SkipBytes(int length);

        IByteBuffer WriteBoolean(bool value);

        IByteBuffer WriteByte(int value);

        IByteBuffer WriteShort(int value);

        IByteBuffer WriteShortLE(int value);

        IByteBuffer WriteUnsignedShort(ushort value);

        IByteBuffer WriteUnsignedShortLE(ushort value);

        IByteBuffer WriteMedium(int value);

        IByteBuffer WriteMediumLE(int value);

        IByteBuffer WriteInt(int value);

        IByteBuffer WriteIntLE(int value);

        IByteBuffer WriteLong(long value);

        IByteBuffer WriteLongLE(long value);

        IByteBuffer WriteChar(char value);

        IByteBuffer WriteDouble(double value);

        IByteBuffer WriteDoubleLE(double value);

        IByteBuffer WriteFloat(float value);

        IByteBuffer WriteFloatLE(float value);

        IByteBuffer WriteBytes(IByteBuffer src);

        IByteBuffer WriteBytes(IByteBuffer src, int length);

        IByteBuffer WriteBytes(IByteBuffer src, int srcIndex, int length);

        IByteBuffer WriteBytes(byte[] src);

        IByteBuffer WriteBytes(byte[] src, int srcIndex, int length);

        /// <summary>
        ///     Returns the maximum <see cref="ArraySegment{T}" /> of <see cref="Byte" /> that this buffer holds. Note that
        ///     <see cref="GetIoBuffers()" />
        ///     or <see cref="GetIoBuffers(int,int)" /> might return a less number of <see cref="ArraySegment{T}" />s of
        ///     <see cref="Byte" />.
        /// </summary>
        /// <returns>
        ///     <c>-1</c> if this buffer cannot represent its content as <see cref="ArraySegment{T}" /> of <see cref="Byte" />.
        ///     the number of the underlying <see cref="IByteBuffer"/>s if this buffer has at least one underlying segment.
        ///     Note that this method does not return <c>0</c> to avoid confusion.
        /// </returns>
        /// <seealso cref="GetIoBuffer()" />
        /// <seealso cref="GetIoBuffer(int,int)" />
        /// <seealso cref="GetIoBuffers()" />
        /// <seealso cref="GetIoBuffers(int,int)" />
        int IoBufferCount { get; }

        /// <summary>
        ///     Exposes this buffer's readable bytes as an <see cref="ArraySegment{T}" /> of <see cref="Byte" />. Returned segment
        ///     shares the content with this buffer. This method is identical
        ///     to <c>buf.GetIoBuffer(buf.ReaderIndex, buf.ReadableBytes)</c>. This method does not
        ///     modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer.  Please note that the
        ///     returned segment will not see the changes of this buffer if this buffer is a dynamic
        ///     buffer and it adjusted its capacity.
        /// </summary>
        /// <exception cref="NotSupportedException">
        ///     if this buffer cannot represent its content as <see cref="ArraySegment{T}" />
        ///     of <see cref="Byte" />
        /// </exception>
        /// <seealso cref="IoBufferCount" />
        /// <seealso cref="GetIoBuffers()" />
        /// <seealso cref="GetIoBuffers(int,int)" />
        ArraySegment<byte> GetIoBuffer();

        /// <summary>
        ///     Exposes this buffer's sub-region as an <see cref="ArraySegment{T}" /> of <see cref="Byte" />. Returned segment
        ///     shares the content with this buffer. This method does not
        ///     modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer. Please note that the
        ///     returned segment will not see the changes of this buffer if this buffer is a dynamic
        ///     buffer and it adjusted its capacity.
        /// </summary>
        /// <exception cref="NotSupportedException">
        ///     if this buffer cannot represent its content as <see cref="ArraySegment{T}" />
        ///     of <see cref="Byte" />
        /// </exception>
        /// <seealso cref="IoBufferCount" />
        /// <seealso cref="GetIoBuffers()" />
        /// <seealso cref="GetIoBuffers(int,int)" />
        ArraySegment<byte> GetIoBuffer(int index, int length);

        /// <summary>
        ///     Exposes this buffer's readable bytes as an array of <see cref="ArraySegment{T}" /> of <see cref="Byte" />. Returned
        ///     segments
        ///     share the content with this buffer. This method does not
        ///     modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer.  Please note that
        ///     returned segments will not see the changes of this buffer if this buffer is a dynamic
        ///     buffer and it adjusted its capacity.
        /// </summary>
        /// <exception cref="NotSupportedException">
        ///     if this buffer cannot represent its content with <see cref="ArraySegment{T}" />
        ///     of <see cref="Byte" />
        /// </exception>
        /// <seealso cref="IoBufferCount" />
        /// <seealso cref="GetIoBuffer()" />
        /// <seealso cref="GetIoBuffer(int,int)" />
        ArraySegment<byte>[] GetIoBuffers();

        /// <summary>
        ///     Exposes this buffer's bytes as an array of <see cref="ArraySegment{T}" /> of <see cref="Byte" /> for the specified
        ///     index and length.
        ///     Returned segments share the content with this buffer. This method does
        ///     not modify <see cref="ReaderIndex" /> or <see cref="WriterIndex" /> of this buffer. Please note that
        ///     returned segments will not see the changes of this buffer if this buffer is a dynamic
        ///     buffer and it adjusted its capacity.
        /// </summary>
        /// <exception cref="NotSupportedException">
        ///     if this buffer cannot represent its content with <see cref="ArraySegment{T}" />
        ///     of <see cref="Byte" />
        /// </exception>
        /// <seealso cref="IoBufferCount" />
        /// <seealso cref="GetIoBuffer()" />
        /// <seealso cref="GetIoBuffer(int,int)" />
        ArraySegment<byte>[] GetIoBuffers(int index, int length);

        /// <summary>
        ///     Flag that indicates if this <see cref="IByteBuffer" /> is backed by a byte array or not
        /// </summary>
        bool HasArray { get; }

        /// <summary>
        ///     Grabs the underlying byte array for this buffer
        /// </summary>
        byte[] Array { get; }

        /// <summary>
        /// Returns {@code true} if and only if this buffer has a reference to the low-level memory address that points
        /// to the backing data.
        /// </summary>
        bool HasMemoryAddress { get; }

        /// <summary>
        ///  Returns the low-level memory address that point to the first byte of ths backing data.
        /// </summary>
        /// <returns>The low-level memory address</returns>
        ref byte GetPinnableMemoryAddress();

        /// <summary>
        /// Returns the pointer address of the buffer if the memory is pinned.
        /// </summary>
        /// <returns>IntPtr.Zero if not pinned.</returns>
        IntPtr AddressOfPinnedMemory();

        /// <summary>
        ///     Creates a deep clone of the existing byte array and returns it
        /// </summary>
        IByteBuffer Duplicate();

        IByteBuffer RetainedDuplicate();

        /// <summary>
        ///     Unwraps a nested buffer
        /// </summary>
        IByteBuffer Unwrap();

        /// <summary>
        ///     Returns a copy of this buffer's readable bytes. Modifying the content of the 
        ///     returned buffer or this buffer does not affect each other at all.This method is 
        ///     identical to {@code buf.copy(buf.readerIndex(), buf.readableBytes())}.
        ///     This method does not modify {@code readerIndex} or {@code writerIndex} of this buffer.
        ///</summary>
        IByteBuffer Copy();

        IByteBuffer Copy(int index, int length);

        IByteBuffer Slice();

        IByteBuffer RetainedSlice();

        IByteBuffer Slice(int index, int length);

        IByteBuffer RetainedSlice(int index, int length);

        int ArrayOffset { get; }

        IByteBuffer ReadSlice(int length);

        IByteBuffer ReadRetainedSlice(int length);

        Task WriteBytesAsync(Stream stream, int length);

        Task WriteBytesAsync(Stream stream, int length, CancellationToken cancellationToken);

        IByteBuffer WriteZero(int length);

        int WriteCharSequence(ICharSequence sequence, Encoding encoding);

        int WriteString(string value, Encoding encoding);

        int IndexOf(int fromIndex, int toIndex, byte value);

        int BytesBefore(byte value);

        int BytesBefore(int length, byte value);

        int BytesBefore(int index, int length, byte value);

        string ToString();

        string ToString(Encoding encoding);

        string ToString(int index, int length, Encoding encoding);

        /// <summary>
        ///     Iterates over the readable bytes of this buffer with the specified <c>processor</c> in ascending order.
        /// </summary>
        /// <returns>
        ///     <c>-1</c> if the processor iterated to or beyond the end of the readable bytes.
        ///     The last-visited index If the <see cref="IByteProcessor.Process(byte)" /> returned <c>false</c>.
        /// </returns>
        /// <param name="processor">Processor.</param>
        int ForEachByte(IByteProcessor processor);

        /// <summary>
        ///     Iterates over the specified area of this buffer with the specified <paramref name="processor"/> in ascending order.
        ///     (i.e. <paramref name="index"/>, <c>(index + 1)</c>,  .. <c>(index + length - 1)</c>)
        /// </summary>
        /// <returns>
        ///     <c>-1</c> if the processor iterated to or beyond the end of the specified area.
        ///     The last-visited index If the <see cref="IByteProcessor.Process(byte)"/> returned <c>false</c>.
        /// </returns>
        /// <param name="index">Index.</param>
        /// <param name="length">Length.</param>
        /// <param name="processor">Processor.</param>
        int ForEachByte(int index, int length, IByteProcessor processor);

        /// <summary>
        ///     Iterates over the readable bytes of this buffer with the specified <paramref name="processor"/> in descending order.
        /// </summary>
        /// <returns>
        ///     <c>-1</c> if the processor iterated to or beyond the beginning of the readable bytes.
        ///     The last-visited index If the <see cref="IByteProcessor.Process(byte)"/> returned <c>false</c>.
        /// </returns>
        /// <param name="processor">Processor.</param>
        int ForEachByteDesc(IByteProcessor processor);

        /// <summary>
        ///     Iterates over the specified area of this buffer with the specified <paramref name="processor"/> in descending order.
        ///     (i.e. <c>(index + length - 1)</c>, <c>(index + length - 2)</c>, ... <paramref name="index"/>)
        /// </summary>
        /// <returns>
        ///     <c>-1</c> if the processor iterated to or beyond the beginning of the specified area.
        ///     The last-visited index If the <see cref="IByteProcessor.Process(byte)"/> returned <c>false</c>.
        /// </returns>
        /// <param name="index">Index.</param>
        /// <param name="length">Length.</param>
        /// <param name="processor">Processor.</param>
        int ForEachByteDesc(int index, int length, IByteProcessor processor);
    }
}