// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Internal;

    static unsafe class UnsafeByteBufferUtil
    {
        const byte Zero = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static short GetShort(byte* bytes) =>
            unchecked((short)(((*bytes) << 8) | *(bytes + 1)));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static short GetShortLE(byte* bytes) =>
            unchecked((short)((*bytes) | (*(bytes + 1) << 8)));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetUnsignedMedium(byte* bytes) =>
            *bytes << 16 | 
            *(bytes + 1) << 8 | 
            *(bytes + 2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetUnsignedMediumLE(byte* bytes) =>
            *bytes | 
            *(bytes + 1) << 8 | 
            *(bytes + 2) << 16;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetInt(byte* bytes) =>
            (*bytes << 24) | 
            (*(bytes + 1) << 16) | 
            (*(bytes + 2) << 8) | 
            (*(bytes + 3));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetIntLE(byte* bytes) =>
            *bytes | 
            (*(bytes + 1) << 8) |
            (*(bytes + 2) << 16) | 
            (*(bytes + 3) << 24);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long GetLong(byte* bytes)
        {
            unchecked
            {
                int i1 = (*bytes << 24) | (*(bytes + 1) << 16) | (*(bytes + 2) << 8) | (*(bytes + 3));
                int i2 = (*(bytes + 4) << 24) | (*(bytes + 5) << 16) | (*(bytes + 6) << 8) | *(bytes + 7);
                return (uint)i2 | ((long)i1 << 32);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long GetLongLE(byte* bytes)
        {
            unchecked
            {
                int i1 = *bytes | (*(bytes + 1) << 8) | (*(bytes + 2) << 16) | (*(bytes + 3) << 24);
                int i2 = *(bytes + 4) | (*(bytes + 5) << 8) | (*(bytes + 6) << 16) | (*(bytes + 7) << 24);
                return (uint)i1 | ((long)i2 << 32);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SetShort(byte* bytes, int value)
        {
            unchecked
            {
                *bytes = (byte)((ushort)value >> 8);
                *(bytes + 1) = (byte)value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SetShortLE(byte* bytes, int value)
        {
            unchecked
            {
                *bytes = (byte)value;
                *(bytes + 1) = (byte)((ushort)value >> 8);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SetMedium(byte* bytes, int value)
        {
            unchecked
            {
                uint unsignedValue = (uint)value;
                *bytes = (byte)(unsignedValue >> 16);
                *(bytes + 1) = (byte)(unsignedValue >> 8);
                *(bytes + 2) = (byte)unsignedValue;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SetMediumLE(byte* bytes, int value)
        {
            unchecked
            {
                uint unsignedValue = (uint)value;
                *bytes = (byte)unsignedValue;
                *(bytes + 1) = (byte)(unsignedValue >> 8);
                *(bytes + 2) = (byte)(unsignedValue >> 16);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SetInt(byte* bytes, int value)
        {
            unchecked
            {
                uint unsignedValue = (uint)value;
                *bytes = (byte)(unsignedValue >> 24);
                *(bytes + 1) = (byte)(unsignedValue >> 16);
                *(bytes + 2) = (byte)(unsignedValue >> 8);
                *(bytes + 3) = (byte)unsignedValue;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SetIntLE(byte* bytes, int value)
        {
            unchecked
            {
                uint unsignedValue = (uint)value;
                *bytes = (byte)unsignedValue;
                *(bytes + 1) = (byte)(unsignedValue >> 8);
                *(bytes + 2) = (byte)(unsignedValue >> 16);
                *(bytes + 3) = (byte)(unsignedValue >> 24);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SetLong(byte* bytes, long value)
        {
            unchecked
            {
                ulong unsignedValue = (ulong)value;
                *bytes = (byte)(unsignedValue >> 56);
                *(bytes + 1) = (byte)(unsignedValue >> 48);
                *(bytes + 2) = (byte)(unsignedValue >> 40);
                *(bytes + 3) = (byte)(unsignedValue >> 32);
                *(bytes + 4) = (byte)(unsignedValue >> 24);
                *(bytes + 5) = (byte)(unsignedValue >> 16);
                *(bytes + 6) = (byte)(unsignedValue >> 8);
                *(bytes + 7) = (byte)unsignedValue;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SetLongLE(byte* bytes, long value)
        {
            unchecked
            {
                ulong unsignedValue = (ulong)value;
                *bytes = (byte)unsignedValue;
                *(bytes + 1) = (byte)(unsignedValue >> 8);
                *(bytes + 2) = (byte)(unsignedValue >> 16);
                *(bytes + 3) = (byte)(unsignedValue >> 24);
                *(bytes + 4) = (byte)(unsignedValue >> 32);
                *(bytes + 5) = (byte)(unsignedValue >> 40);
                *(bytes + 6) = (byte)(unsignedValue >> 48);
                *(bytes + 7) = (byte)(unsignedValue >> 56);
            }
        }

        internal static void SetZero(byte[] array, int index, int length)
        {
            if (length == 0)
            {
                return;
            }
            PlatformDependent.SetMemory(array, index, length, Zero);
        }

        internal static IByteBuffer Copy(AbstractByteBuffer buf, byte* addr, int index, int length)
        {
            IByteBuffer copy = buf.Allocator.Buffer(length, buf.MaxCapacity);
            if (length != 0)
            {
                if (copy.HasMemoryAddress)
                {
                    fixed (byte* dst = &copy.GetPinnableMemoryAddress())
                    {
                        PlatformDependent.CopyMemory(addr, dst, length);
                    }
                    copy.SetIndex(0, length);
                }
                else
                {
                    copy.WriteBytes(buf, index, length);
                }
            }
            return copy;
        }

        internal static int SetBytes(AbstractByteBuffer buf, byte* addr, int index, Stream input, int length)
        {
            IByteBuffer tmpBuf = buf.Allocator.HeapBuffer(length);
            try
            {
                byte[] tmp = tmpBuf.Array;
                int offset = tmpBuf.ArrayOffset;
                int readBytes = input.Read(tmp, offset, length);
                if (readBytes > 0)
                {
                    PlatformDependent.CopyMemory(tmp, offset, addr, readBytes);
                }

                return readBytes;
            }
            finally
            {
                tmpBuf.Release();
            }
        }

        internal static void GetBytes(AbstractByteBuffer buf, byte* addr, int index, IByteBuffer dst, int dstIndex, int length)
        {
            Contract.Requires(dst != null);

            if (MathUtil.IsOutOfBounds(dstIndex, length, dst.Capacity))
            {
                throw new IndexOutOfRangeException($"dstIndex: {dstIndex}");
            }

            if (dst.HasMemoryAddress)
            {
                fixed (byte* destination = &dst.GetPinnableMemoryAddress())
                {
                    PlatformDependent.CopyMemory(addr, destination + dstIndex, length);
                }
            }
            else if (dst.HasArray)
            {
                PlatformDependent.CopyMemory(addr, dst.Array, dst.ArrayOffset + dstIndex, length);
            }
            else
            {
                dst.SetBytes(dstIndex, buf, index, length);
            }
        }

        internal static void GetBytes(AbstractByteBuffer buf, byte* addr, int index, byte[] dst, int dstIndex, int length)
        {
            Contract.Requires(dst != null);

            if (MathUtil.IsOutOfBounds(dstIndex, length, dst.Length))
            {
                throw new IndexOutOfRangeException($"dstIndex: {dstIndex}");
            }
            if (length != 0)
            {
                PlatformDependent.CopyMemory(addr, dst, dstIndex, length);
            }
        }

        internal static void SetBytes(AbstractByteBuffer buf, byte* addr, int index, IByteBuffer src, int srcIndex, int length)
        {
            Contract.Requires(src != null);

            if (MathUtil.IsOutOfBounds(srcIndex, length, src.Capacity))
            {
                throw new IndexOutOfRangeException($"srcIndex: {srcIndex}");
            }

            if (length != 0)
            {
                if (src.HasMemoryAddress)
                {
                    fixed (byte* source = &src.GetPinnableMemoryAddress())
                    {
                        PlatformDependent.CopyMemory(source + srcIndex, addr, length);
                    }
                }
                else if (src.HasArray)
                {
                    PlatformDependent.CopyMemory(src.Array, src.ArrayOffset + srcIndex, addr, length);
                }
                else
                {
                    src.GetBytes(srcIndex, buf, index, length);
                }
            }
        }

        // No need to check length zero, the calling method already done it
        internal static void SetBytes(AbstractByteBuffer buf, byte* addr, int index, byte[] src, int srcIndex, int length) =>
                PlatformDependent.CopyMemory(src, srcIndex, addr, length);

        internal static void GetBytes(AbstractByteBuffer buf, byte* addr, int index, Stream output, int length)
        {
            if (length != 0)
            {
                IByteBuffer tmpBuf = buf.Allocator.HeapBuffer(length);
                try
                {
                    byte[] tmp = tmpBuf.Array;
                    int offset = tmpBuf.ArrayOffset;
                    PlatformDependent.CopyMemory(addr, tmp, offset, length);
                    output.Write(tmp, offset, length);
                }
                finally
                {
                    tmpBuf.Release();
                }
            }
        }

        internal static void SetZero(byte* addr, int length)
        {
            if (length == 0)
            {
                return;
            }
            PlatformDependent.SetMemory(addr, length, Zero);
        }

        internal static UnpooledUnsafeDirectByteBuffer NewUnsafeDirectByteBuffer(
            IByteBufferAllocator alloc, int initialCapacity, int maxCapacity) => 
                new UnpooledUnsafeDirectByteBuffer(alloc, initialCapacity, maxCapacity);
    }
}
