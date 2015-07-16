// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;

    public class ByteBufferUtil
    {
        /// <summary>
        ///  Returns {@code true} if and only if the two specified buffers are
        ///  identical to each other for {@code length} bytes starting at {@code aStartIndex}
        ///  index for the {@code a} buffer and {@code bStartIndex} index for the {@code b} buffer.
        ///  A more compact way to express this is:
        ///  <p>
        ///  {@code a[aStartIndex : aStartIndex + length] == b[bStartIndex : bStartIndex + length]}
        /// </summary>
        public static bool Equals(IByteBuffer a, int aStartIndex, IByteBuffer b, int bStartIndex, int length)
        {
            if (aStartIndex < 0 || bStartIndex < 0 || length < 0)
            {
                throw new ArgumentException("All indexes and lengths must be non-negative");
            }
            if (a.WriterIndex - length < aStartIndex || b.WriterIndex - length < bStartIndex)
            {
                return false;
            }

            int longCount = unchecked((int)((uint)length >> 3));
            int byteCount = length & 7;

            if (a.Order == b.Order)
            {
                for (int i = longCount; i > 0; i --)
                {
                    if (a.GetLong(aStartIndex) != b.GetLong(bStartIndex))
                    {
                        return false;
                    }
                    aStartIndex += 8;
                    bStartIndex += 8;
                }
            }
            else
            {
                for (int i = longCount; i > 0; i --)
                {
                    if (a.GetLong(aStartIndex) != SwapLong(b.GetLong(bStartIndex)))
                    {
                        return false;
                    }
                    aStartIndex += 8;
                    bStartIndex += 8;
                }
            }

            for (int i = byteCount; i > 0; i --)
            {
                if (a.GetByte(aStartIndex) != b.GetByte(bStartIndex))
                {
                    return false;
                }
                aStartIndex ++;
                bStartIndex ++;
            }

            return true;
        }

        /// <summary>
        ///  Returns {@code true} if and only if the two specified buffers are
        ///  identical to each other as described in {@link ByteBuf#equals(Object)}.
        ///  This method is useful when implementing a new buffer type.
        /// </summary>
        public static bool Equals(IByteBuffer bufferA, IByteBuffer bufferB)
        {
            int aLen = bufferA.ReadableBytes;
            if (aLen != bufferB.ReadableBytes)
            {
                return false;
            }
            return Equals(bufferA, bufferA.ReaderIndex, bufferB, bufferB.ReaderIndex, aLen);
        }

        /// <summary>
        ///  Toggles the endianness of the specified 64-bit long integer.
        /// </summary>
        public static long SwapLong(long value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            return BitConverter.ToInt64(bytes, 0);
        }
    }
}