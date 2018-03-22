// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Text;
    using DotNetty.Common.Internal;

    /// <summary>
    ///     Utility class for managing and creating unpooled buffers
    /// </summary>
    public static class Unpooled
    {
        static readonly UnpooledByteBufferAllocator Allocator = UnpooledByteBufferAllocator.Default;

        public static readonly IByteBuffer Empty = Allocator.Buffer(0, 0);

        public static IByteBuffer Buffer() => Allocator.HeapBuffer();

        public static IByteBuffer DirectBuffer() => Allocator.DirectBuffer();

        public static IByteBuffer Buffer(int initialCapacity) => Allocator.HeapBuffer(initialCapacity);

        public static IByteBuffer DirectBuffer(int initialCapacity) => Allocator.DirectBuffer(initialCapacity);

        public static IByteBuffer Buffer(int initialCapacity, int maxCapacity) =>
            Allocator.HeapBuffer(initialCapacity, maxCapacity);

        public static IByteBuffer DirectBuffer(int initialCapacity, int maxCapacity) =>
            Allocator.DirectBuffer(initialCapacity, maxCapacity);

        /// <summary>
        ///     Creates a new big-endian buffer which wraps the specified array.
        ///     A modification on the specified array's content will be visible to the returned buffer.
        /// </summary>
        public static IByteBuffer WrappedBuffer(byte[] array) =>
            array.Length == 0 ? Empty  : new UnpooledHeapByteBuffer(Allocator, array, array.Length);

        /// <summary>
        ///     Creates a new big-endian buffer which wraps the sub-region of the
        ///     specified array. A modification on the specified array's content 
        ///     will be visible to the returned buffer.
        /// </summary>
        public static IByteBuffer WrappedBuffer(byte[] array, int offset, int length)
        {
            if (length == 0)
            {
                return Empty;
            }

            if (offset == 0 && length == array.Length)
            {
                return WrappedBuffer(array);
            }

            return WrappedBuffer(array).Slice(offset, length);
        }

        /// <summary>
        ///     Creates a new buffer which wraps the specified buffer's readable bytes.
        ///     A modification on the specified buffer's content will be visible to the returned buffer.
        /// </summary>
        /// <param name="buffer">The buffer to wrap. Reference count ownership of this variable is transfered to this method.</param>
        /// <returns>The readable portion of the buffer, or an empty buffer if there is no readable portion.</returns>
        public static IByteBuffer WrappedBuffer(IByteBuffer buffer)
        {
            if (buffer.IsReadable())
            {
                return buffer.Slice();
            }
            else
            {
                buffer.Release();
                return Empty;
            }
        }

        /// <summary>
        ///     Creates a new big-endian composite buffer which wraps the specified arrays without copying them.
        ///     A modification on the specified arrays' content will be visible to the returned buffer.
        /// </summary>
        public static IByteBuffer WrappedBuffer(params byte[][] arrays) => WrappedBuffer(AbstractByteBufferAllocator.DefaultMaxComponents, arrays);

        /// <summary>
        ///     Creates a new big-endian composite buffer which wraps the readable bytes of the specified buffers without copying them. 
        ///     A modification on the content of the specified buffers will be visible to the returned buffer.
        /// </summary>
        /// <param name="buffers">The buffers to wrap. Reference count ownership of all variables is transfered to this method.</param>
        /// <returns>The readable portion of the buffers. The caller is responsible for releasing this buffer.</returns>
        public static IByteBuffer WrappedBuffer(params IByteBuffer[] buffers) => WrappedBuffer(AbstractByteBufferAllocator.DefaultMaxComponents, buffers);

        /// <summary>
        ///     Creates a new big-endian composite buffer which wraps the specified arrays without copying them.
        ///     A modification on the specified arrays' content will be visible to the returned buffer.
        /// </summary>
        public static IByteBuffer WrappedBuffer(int maxNumComponents, params byte[][] arrays)
        {
            switch (arrays.Length)
            {
                case 0:
                    break;
                case 1:
                    if (arrays[0].Length != 0)
                    {
                        return WrappedBuffer(arrays[0]);
                    }
                    break;
                default:
                    // Get the list of the component, while guessing the byte order.
                    var components = new List<IByteBuffer>(arrays.Length);
                    foreach (byte[] array in arrays)
                    {
                        if (array == null)
                        {
                            break;
                        }
                        if (array.Length > 0)
                        {
                            components.Add(WrappedBuffer(array));
                        }
                    }

                    if (components.Count > 0)
                    {
                        return new CompositeByteBuffer(Allocator, false, maxNumComponents, components);
                    }
                    break;
            }

            return Empty;
        }

        /// <summary>
        ///     Creates a new big-endian composite buffer which wraps the readable bytes of the specified buffers without copying them.
        ///     A modification on the content of the specified buffers will be visible to the returned buffer.
        /// </summary>
        /// <param name="maxNumComponents">Advisement as to how many independent buffers are allowed to exist before consolidation occurs.</param>
        /// <param name="buffers">The buffers to wrap. Reference count ownership of all variables is transfered to this method.</param>
        /// <returns>The readable portion of the buffers. The caller is responsible for releasing this buffer.</returns>
        public static IByteBuffer WrappedBuffer(int maxNumComponents, params IByteBuffer[] buffers)
        {
            switch (buffers.Length)
            {
                case 0:
                    break;
                case 1:
                    IByteBuffer buffer = buffers[0];
                    if (buffer.IsReadable())
                        return WrappedBuffer(buffer);
                    else
                        buffer.Release();
                    break;
                default:
                    for (int i = 0; i < buffers.Length; i++)
                    {
                        IByteBuffer buf = buffers[i];
                        if (buf.IsReadable())
                            return new CompositeByteBuffer(Allocator, false, maxNumComponents, buffers, i, buffers.Length);
                        else
                            buf.Release();
                    }
                    break;
            }

            return Empty;
        }

        public static CompositeByteBuffer CompositeBuffer() => CompositeBuffer(AbstractByteBufferAllocator.DefaultMaxComponents);

        public static CompositeByteBuffer CompositeBuffer(int maxNumComponents) => new CompositeByteBuffer(Allocator, false, maxNumComponents);

        /// <summary>
        ///     Creates a new big-endian buffer whose content is a copy of the specified array
        ///     The new buffer's <see cref="IByteBuffer.ReaderIndex" /> and <see cref="IByteBuffer.WriterIndex" />
        ///     are <c>0</c> and <see cref="Array.Length" /> respectively.
        /// </summary>
        /// <param name="array">A buffer we're going to copy.</param>
        /// <returns>The new buffer that copies the contents of array.</returns>
        public static IByteBuffer CopiedBuffer(byte[] array)
        {
            if (array.Length == 0)
            {
                return Empty;
            }

            var newArray = new byte[array.Length];
            PlatformDependent.CopyMemory(array, 0, newArray, 0, array.Length);

            return WrappedBuffer(newArray);
        }

        /// <summary>
        ///     Creates a new big-endian buffer whose content is a copy of the specified array.
        ///     The new buffer's <see cref="IByteBuffer.ReaderIndex" /> and <see cref="IByteBuffer.WriterIndex" />
        ///     are <c>0</c> and <see cref="Array.Length" /> respectively.
        /// </summary>
        /// <param name="array">A buffer we're going to copy.</param>
        /// <param name="offset">The index offset from which we're going to read array.</param>
        /// <param name="length">
        ///     The number of bytes we're going to read from array beginning from position offset.
        /// </param>
        /// <returns>The new buffer that copies the contents of array.</returns>
        public static IByteBuffer CopiedBuffer(byte[] array, int offset, int length)
        {
            if (length == 0)
            {
                return Empty;
            }

            var copy = new byte[length];
            PlatformDependent.CopyMemory(array, offset, copy, 0, length);
            return WrappedBuffer(copy);
        }

        /// <summary>
        ///     Creates a new big-endian buffer whose content is a copy of the specified <see cref="Array" />.
        ///     The new buffer's <see cref="IByteBuffer.ReaderIndex" /> and <see cref="IByteBuffer.WriterIndex" />
        ///     are <c>0</c> and <see cref="IByteBuffer.Capacity" /> respectively.
        /// </summary>
        /// <param name="buffer">A buffer we're going to copy.</param>
        /// <returns>The new buffer that copies the contents of buffer.</returns>
        public static IByteBuffer CopiedBuffer(IByteBuffer buffer)
        {
            int readable = buffer.ReadableBytes;
            if (readable > 0)
            {
                IByteBuffer copy = Buffer(readable);
                copy.WriteBytes(buffer, buffer.ReaderIndex, readable);
                return copy;
            }
            else
            {
                return Empty;
            }
        }

        /// <summary>
        ///     Creates a new big-endian buffer whose content is a merged copy of of the specified arrays.
        ///     The new buffer's <see cref="IByteBuffer.ReaderIndex" /> and <see cref="IByteBuffer.WriterIndex" />
        ///     are <c>0</c> and <see cref="Array.Length" /> respectively.
        /// </summary>
        /// <param name="arrays"></param>
        /// <returns></returns>
        public static IByteBuffer CopiedBuffer(params byte[][] arrays)
        {
            switch (arrays.Length)
            {
                case 0:
                    return Empty;
                case 1:
                    return arrays[0].Length == 0 ? Empty : CopiedBuffer(arrays[0]);
            }

            // Merge the specified arrays into one array.
            int length = 0;
            foreach (byte[] a in arrays)
            {
                if (int.MaxValue - length < a.Length)
                {
                    throw new ArgumentException("The total length of the specified arrays is too big.");
                }
                length += a.Length;
            }

            if (length == 0)
            {
                return Empty;
            }

            var mergedArray = new byte[length];
            for (int i = 0, j = 0; i < arrays.Length; i++)
            {
                byte[] a = arrays[i];
                PlatformDependent.CopyMemory(a, 0, mergedArray, j, a.Length);
                j += a.Length;
            }

            return WrappedBuffer(mergedArray);
        }

        /// <summary>
        ///     Creates a new big-endian buffer whose content  is a merged copy of the specified <see cref="Array" />.
        ///     The new buffer's <see cref="IByteBuffer.ReaderIndex" /> and <see cref="IByteBuffer.WriterIndex" />
        ///     are <c>0</c> and <see cref="IByteBuffer.Capacity" /> respectively.
        /// </summary>
        /// <param name="buffers">Buffers we're going to copy.</param>
        /// <returns>The new buffer that copies the contents of buffers.</returns>
        public static IByteBuffer CopiedBuffer(params IByteBuffer[] buffers)
        {
            switch (buffers.Length)
            {
                case 0:
                    return Empty;
                case 1:
                    return CopiedBuffer(buffers[0]);
            }

            // Merge the specified buffers into one buffer.
            int length = 0;
            foreach (IByteBuffer b in buffers)
            {
                int bLen = b.ReadableBytes;
                if (bLen <= 0)
                {
                    continue;
                }
                if (int.MaxValue - length < bLen)
                {
                    throw new ArgumentException("The total length of the specified buffers is too big.");
                }

                length += bLen;
            }

            if (length == 0)
            {
                return Empty;
            }

            var mergedArray = new byte[length];
            for (int i = 0, j = 0; i < buffers.Length; i++)
            {
                IByteBuffer b = buffers[i];
                int bLen = b.ReadableBytes;
                b.GetBytes(b.ReaderIndex, mergedArray, j, bLen);
                j += bLen;
            }

            return WrappedBuffer(mergedArray);
        }

        public static IByteBuffer CopiedBuffer(char[] array, int offset, int length, Encoding encoding)
        {
            Contract.Requires(array != null);
            return length == 0 ? Empty : CopiedBuffer(new string(array, offset, length), encoding);
        }

        static IByteBuffer CopiedBuffer(string value, Encoding encoding) => ByteBufferUtil.EncodeString0(Allocator, true, value, encoding, 0);

        /// <summary>
        ///     Creates a new 4-byte big-endian buffer that holds the specified 32-bit integer.
        /// </summary>
        public static IByteBuffer CopyInt(int value)
        {
            IByteBuffer buf = Buffer(4);
            buf.WriteInt(value);
            return buf;
        }

        /// <summary>
        ///     Create a big-endian buffer that holds a sequence of the specified 32-bit integers.
        /// </summary>
        public static IByteBuffer CopyInt(params int[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Empty;
            }

            IByteBuffer buffer = Buffer(values.Length * 4);
            foreach (int v in values)
            {
                buffer.WriteInt(v);
            }

            return buffer;
        }

        /// <summary>
        ///     Creates a new 2-byte big-endian buffer that holds the specified 16-bit integer.
        /// </summary>
        public static IByteBuffer CopyShort(int value)
        {
            IByteBuffer buf = Buffer(2);
            buf.WriteShort(value);
            return buf;
        }

        /// <summary>
        ///     Create a new big-endian buffer that holds a sequence of the specified 16-bit integers.
        /// </summary>
        public static IByteBuffer CopyShort(params short[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Empty;
            }

            IByteBuffer buffer = Buffer(values.Length * 2);
            foreach (short v in values)
            {
                buffer.WriteShort(v);
            }

            return buffer;
        }

        /// <summary>
        ///     Create a new big-endian buffer that holds a sequence of the specified 16-bit integers.
        /// </summary>
        public static IByteBuffer CopyShort(params int[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Empty;
            }

            IByteBuffer buffer = Buffer(values.Length * 2);
            foreach (int v in values)
            {
                buffer.WriteShort(v);
            }
            return buffer;
        }

        /// <summary>
        ///     Creates a new 3-byte big-endian buffer that holds the specified 24-bit integer.
        /// </summary>
        public static IByteBuffer CopyMedium(int value)
        {
            IByteBuffer buf = Buffer(3);
            buf.WriteMedium(value);
            return buf;
        }

        /// <summary>
        ///     Create a new big-endian buffer that holds a sequence of the specified 24-bit integers.
        /// </summary>
        public static IByteBuffer CopyMedium(params int[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Empty;
            }

            IByteBuffer buffer = Buffer(values.Length * 3);
            foreach (int v in values)
            {
                buffer.WriteMedium(v);
            }

            return buffer;
        }

        /// <summary>
        ///     Creates a new 8-byte big-endian buffer that holds the specified 64-bit integer.
        /// </summary>
        public static IByteBuffer CopyLong(long value)
        {
            IByteBuffer buf = Buffer(8);
            buf.WriteLong(value);
            return buf;
        }

        /// <summary>
        ///     Create a new big-endian buffer that holds a sequence of the specified 64-bit integers.
        /// </summary>
        public static IByteBuffer CopyLong(params long[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Empty;
            }

            IByteBuffer buffer = Buffer(values.Length * 8);
            foreach (long v in values)
            {
                buffer.WriteLong(v);
            }

            return buffer;
        }

        /// <summary>
        ///     Creates a new single-byte big-endian buffer that holds the specified boolean value.
        /// </summary>
        public static IByteBuffer CopyBoolean(bool value)
        {
            IByteBuffer buf = Buffer(1);
            buf.WriteBoolean(value);
            return buf;
        }

        /// <summary>
        ///     Create a new big-endian buffer that holds a sequence of the specified boolean values.
        /// </summary>
        public static IByteBuffer CopyBoolean(params bool[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Empty;
            }

            IByteBuffer buffer = Buffer(values.Length);
            foreach (bool v in values)
            {
                buffer.WriteBoolean(v);
            }

            return buffer;
        }

        /// <summary>
        ///     Creates a new 4-byte big-endian buffer that holds the specified 32-bit floating point number.
        /// </summary>
        public static IByteBuffer CopyFloat(float value)
        {
            IByteBuffer buf = Buffer(4);
            buf.WriteFloat(value);
            return buf;
        }

        /// <summary>
        ///     Create a new big-endian buffer that holds a sequence of the specified 32-bit floating point numbers.
        /// </summary>
        public static IByteBuffer CopyFloat(params float[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Empty;
            }

            IByteBuffer buffer = Buffer(values.Length * 4);
            foreach (float v in values)
            {
                buffer.WriteFloat(v);
            }

            return buffer;
        }

        /// <summary>
        ///     Creates a new 8-byte big-endian buffer that holds the specified 64-bit floating point number.
        /// </summary>
        public static IByteBuffer CopyDouble(double value)
        {
            IByteBuffer buf = Buffer(8);
            buf.WriteDouble(value);
            return buf;
        }

        /// <summary>
        ///     Create a new big-endian buffer that holds a sequence of the specified 64-bit floating point numbers.
        /// </summary>
        public static IByteBuffer CopyDouble(params double[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Empty;
            }

            IByteBuffer buffer = Buffer(values.Length * 8);
            foreach (double v in values)
            {
                buffer.WriteDouble(v);
            }

            return buffer;
        }

        /// <summary>
        ///     Return a unreleasable view on the given {@link ByteBuf} which will just ignore release and retain calls.
        /// </summary>
        public static IByteBuffer UnreleasableBuffer(IByteBuffer buffer) => new UnreleasableByteBuffer(buffer);
    }
}