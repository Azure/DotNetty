// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Diagnostics.Contracts;

    /// <summary>
    /// Utility class for managing and creating unpooled buffers
    /// </summary>
    public static class Unpooled
    {
        static readonly IByteBufferAllocator Allocator = UnpooledByteBufferAllocator.Default;

        public static readonly IByteBuffer Empty = Allocator.Buffer(0, 0);

        public static IByteBuffer Buffer()
        {
            return Allocator.Buffer();
        }

        public static IByteBuffer Buffer(int initialCapacity)
        {
            Contract.Requires(initialCapacity >= 0);

            return Allocator.Buffer(initialCapacity);
        }

        public static IByteBuffer Buffer(int initialCapacity, int maxCapacity)
        {
            Contract.Requires(initialCapacity >= 0 && initialCapacity <= maxCapacity);
            Contract.Requires(maxCapacity >= 0);

            return Allocator.Buffer(initialCapacity, maxCapacity);
        }

        public static IByteBuffer WrappedBuffer(byte[] array)
        {
            Contract.Requires(array != null);

            return array.Length == 0 ? Empty : new UnpooledHeapByteBuffer(Allocator, array, array.Length);
        }

        public static IByteBuffer WrappedBuffer(byte[] array, int offset, int length)
        {
            Contract.Requires(array != null);
            Contract.Requires(offset >= 0);
            Contract.Requires(length >= 0);

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
        /// Creates a new big-endian buffer whose content is a copy of the specified <see cref="array"/>.
        /// The new buffer's <see cref="IByteBuffer.ReaderIndex"/> and <see cref="IByteBuffer.WriterIndex"/>
        /// are <c>0</c> and <see cref="Array.Length"/> respectively.
        /// </summary>
        /// <param name="array">A buffer we're going to copy.</param>
        /// <returns>The new buffer that copies the contents of <see cref="array"/>.</returns>
        public static IByteBuffer CopiedBuffer(byte[] array)
        {
            Contract.Requires(array != null);
            if (array.Length == 0)
            {
                return Empty;
            }
            byte[] newArray = new byte[array.Length];
            Array.Copy(array, newArray, array.Length);
            return WrappedBuffer(newArray);
        }

        /// <summary>
        /// Creates a new big-endian buffer whose content is a copy of the specified <see cref="array"/>.
        /// The new buffer's <see cref="IByteBuffer.ReaderIndex"/> and <see cref="IByteBuffer.WriterIndex"/>
        /// are <c>0</c> and <see cref="Array.Length"/> respectively.
        /// </summary>
        /// <param name="array">A buffer we're going to copy.</param>
        /// <param name="offset">The index offset from which we're going to read <see cref="array"/>.</param>
        /// <param name="length">The number of bytes we're going to read from <see cref="array"/> 
        /// beginning from position <see cref="offset"/>.</param>
        /// <returns>The new buffer that copies the contents of <see cref="array"/>.</returns>
        public static IByteBuffer CopiedBuffer(byte[] array, int offset, int length)
        {
            Contract.Requires(array != null);
            if (array.Length == 0)
            {
                return Empty;
            }
            byte[] copy = new byte[length];
            Array.Copy(array, offset, copy, 0, length);
            return WrappedBuffer(copy);
        }

        /// <summary>
        /// Creates a new big-endian buffer whose content is a copy of the specified <see cref="array"/>.
        /// The new buffer's <see cref="IByteBuffer.ReaderIndex"/> and <see cref="IByteBuffer.WriterIndex"/>
        /// are <c>0</c> and <see cref="IByteBuffer.Capacity"/> respectively.
        /// </summary>
        /// <param name="buffer">A buffer we're going to copy.</param>
        /// <returns>The new buffer that copies the contents of <see cref="buffer"/>.</returns>
        public static IByteBuffer CopiedBuffer(IByteBuffer buffer)
        {
            Contract.Requires(buffer != null);
            int length = buffer.ReadableBytes;
            if (length == 0)
            {
                return Empty;
            }
            byte[] copy = new byte[length];

            // Duplicate the buffer so we do not adjust our position during our get operation
            IByteBuffer duplicate = buffer.Duplicate();
            duplicate.GetBytes(0, copy);
            return WrappedBuffer(copy).WithOrder(duplicate.Order);
        }
    }
}