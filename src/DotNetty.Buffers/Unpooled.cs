// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
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
    }
}