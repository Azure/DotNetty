// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Text;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    public static class ByteBufferUtil
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance(typeof(ByteBufferUtil));

        public static readonly IByteBufferAllocator DefaultAllocator;

        static ByteBufferUtil()
        {
            string allocType = SystemPropertyUtil.Get("io.netty.allocator.type", "pooled");
            allocType = allocType.Trim();

            IByteBufferAllocator alloc;
            if ("unpooled".Equals(allocType, StringComparison.OrdinalIgnoreCase))
            {
                alloc = UnpooledByteBufferAllocator.Default;
                Logger.Debug("-Dio.netty.allocator.type: {}", allocType);
            }
            else if ("pooled".Equals(allocType, StringComparison.OrdinalIgnoreCase))
            {
                alloc = PooledByteBufferAllocator.Default;
                Logger.Debug("-Dio.netty.allocator.type: {}", allocType);
            }
            else
            {
                alloc = PooledByteBufferAllocator.Default;
                Logger.Debug("-Dio.netty.allocator.type: pooled (unknown: {})", allocType);
            }

            DefaultAllocator = alloc;
        }

        /// <summary>
        ///     Returns a <a href="http://en.wikipedia.org/wiki/Hex_dump">hex dump</a>
        ///     of the specified buffer's sub-region.
        /// </summary>
        public static string HexDump(IByteBuffer buffer) => HexDump(buffer, buffer.ReaderIndex, buffer.ReadableBytes);

        /// <summary>
        ///     Returns a <a href="http://en.wikipedia.org/wiki/Hex_dump">hex dump</a>
        ///     of the specified buffer's sub-region.
        /// </summary>
        public static string HexDump(IByteBuffer buffer, int fromIndex, int length) => HexUtil.DoHexDump(buffer, fromIndex, length);

        /// <summary>
        ///     Returns a <a href="http://en.wikipedia.org/wiki/Hex_dump">hex dump</a>
        ///     of the specified buffer's sub-region.
        /// </summary>
        public static string HexDump(byte[] array) => HexDump(array, 0, array.Length);

        /// <summary>
        ///     Returns a <a href="http://en.wikipedia.org/wiki/Hex_dump">hex dump</a>
        ///     of the specified buffer's sub-region.
        /// </summary>
        public static string HexDump(byte[] array, int fromIndex, int length) => HexUtil.DoHexDump(array, fromIndex, length);

        public static bool EnsureWritableSuccess(int ensureWritableResult) =>  ensureWritableResult == 0 || ensureWritableResult == 2;
        
        /// <summary>
        ///     Calculates the hash code of the specified buffer.  This method is
        ///     useful when implementing a new buffer type.
        /// </summary>
        public static int HashCode(IByteBuffer buffer)
        {
            int aLen = buffer.ReadableBytes;
            int intCount = (int)((uint)aLen >> 2);
            int byteCount = aLen & 3;

            int hashCode = 1;
            int arrayIndex = buffer.ReaderIndex;
            for (int i = intCount; i > 0; i--)
            {
                hashCode = 31 * hashCode + buffer.GetInt(arrayIndex);
                arrayIndex += 4;
            }

            for (int i = byteCount; i > 0; i--)
            {
                hashCode = 31 * hashCode + buffer.GetByte(arrayIndex++);
            }

            if (hashCode == 0)
            {
                hashCode = 1;
            }

            return hashCode;
        }

        /// <summary>
        ///     Returns the reader index of needle in haystack, or -1 if needle is not in haystack.
        /// </summary>
        public static int IndexOf(IByteBuffer needle, IByteBuffer haystack)
        {
            // TODO: maybe use Boyer Moore for efficiency.
            int attempts = haystack.ReadableBytes - needle.ReadableBytes + 1;
            for (int i = 0; i < attempts; i++)
            {
                if (Equals(needle, needle.ReaderIndex, haystack, haystack.ReaderIndex + i, needle.ReadableBytes))
                {
                    return haystack.ReaderIndex + i;
                }
            }

            return -1;
        }

        /// <summary>
        ///     Returns {@code true} if and only if the two specified buffers are
        ///     identical to each other for {@code length} bytes starting at {@code aStartIndex}
        ///     index for the {@code a} buffer and {@code bStartIndex} index for the {@code b} buffer.
        ///     A more compact way to express this is:
        ///     <p />
        ///     {@code a[aStartIndex : aStartIndex + length] == b[bStartIndex : bStartIndex + length]}
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

            for (int i = longCount; i > 0; i--)
            {
                if (a.GetLong(aStartIndex) != b.GetLong(bStartIndex))
                {
                    return false;
                }
                aStartIndex += 8;
                bStartIndex += 8;
            }

            for (int i = byteCount; i > 0; i--)
            {
                if (a.GetByte(aStartIndex) != b.GetByte(bStartIndex))
                {
                    return false;
                }
                aStartIndex++;
                bStartIndex++;
            }

            return true;
        }

        /// <summary>
        ///     Returns {@code true} if and only if the two specified buffers are
        ///     identical to each other as described in {@link ByteBuf#equals(Object)}.
        ///     This method is useful when implementing a new buffer type.
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
        ///     Compares the two specified buffers as described in {@link ByteBuf#compareTo(ByteBuf)}.
        ///     This method is useful when implementing a new buffer type.
        /// </summary>
        public static int Compare(IByteBuffer bufferA, IByteBuffer bufferB)
        {
            int aLen = bufferA.ReadableBytes;
            int bLen = bufferB.ReadableBytes;
            int minLength = Math.Min(aLen, bLen);
            int uintCount = minLength.RightUShift(2);
            int byteCount = minLength & 3;

            int aIndex = bufferA.ReaderIndex;
            int bIndex = bufferB.ReaderIndex;

            if (uintCount > 0)
            {
                int uintCountIncrement = uintCount << 2;
                int res = CompareUint(bufferA, bufferB, aIndex, bIndex, uintCountIncrement);
                if (res != 0)
                {
                    return res;
                }

                aIndex += uintCountIncrement;
                bIndex += uintCountIncrement;
            }

            for (int aEnd = aIndex + byteCount; aIndex < aEnd; ++aIndex, ++bIndex)
            {
                int comp = bufferA.GetByte(aIndex) - bufferB.GetByte(bIndex);
                if (comp != 0)
                {
                    return comp;
                }
            }

            return aLen - bLen;
        }

        static int CompareUint(IByteBuffer bufferA, IByteBuffer bufferB, int aIndex, int bIndex, int uintCountIncrement)
        {
            for (int aEnd = aIndex + uintCountIncrement; aIndex < aEnd; aIndex += 4, bIndex += 4)
            {
                long va = bufferA.GetUnsignedInt(aIndex);
                long vb = bufferB.GetUnsignedInt(bIndex);
                if (va > vb)
                {
                    return 1;
                }
                if (va < vb)
                {
                    return -1;
                }
            }
            return 0;
        }

        /// <summary>
        /// The default implementation of <see cref="IByteBuffer.IndexOf(int, int, byte)"/>.
        /// This method is useful when implementing a new buffer type.
        /// </summary>
        public static int IndexOf(IByteBuffer buffer, int fromIndex, int toIndex, byte value)
        {
            if (fromIndex <= toIndex)
            {
                return FirstIndexOf(buffer, fromIndex, toIndex, value);
            }
            else
            {
                return LastIndexOf(buffer, fromIndex, toIndex, value);
            }
        }

        /// <summary>
        ///     Read the given amount of bytes into a new {@link ByteBuf} that is allocated from the {@link ByteBufAllocator}.
        /// </summary>
        public static IByteBuffer ReadBytes(IByteBufferAllocator alloc, IByteBuffer buffer, int length)
        {
            bool release = true;
            IByteBuffer dst = alloc.Buffer(length);
            try
            {
                buffer.ReadBytes(dst);
                release = false;
                return dst;
            }
            finally
            {
                if (release)
                {
                    dst.Release();
                }
            }
        }

        static int FirstIndexOf(IByteBuffer buffer, int fromIndex, int toIndex, byte value)
        {
            fromIndex = Math.Max(fromIndex, 0);
            if (fromIndex >= toIndex || buffer.Capacity == 0)
            {
                return -1;
            }

            return buffer.ForEachByte(fromIndex, toIndex - fromIndex, new IndexOfProcessor(value));
        }

        static int LastIndexOf(IByteBuffer buffer, int fromIndex, int toIndex, byte value)
        {
            fromIndex = Math.Min(fromIndex, buffer.Capacity);
            if (fromIndex < 0 || buffer.Capacity == 0)
            {
                return -1;
            }

            return buffer.ForEachByteDesc(toIndex, fromIndex - toIndex, new IndexOfProcessor(value));
        }

        /// <summary>
        ///     Encode the given <see cref="string" /> using the given <see cref="Encoding" /> into a new
        ///     <see cref="IByteBuffer" /> which
        ///     is allocated via the <see cref="IByteBufferAllocator" />.
        /// </summary>
        /// <param name="alloc">The <see cref="IByteBufferAllocator" /> to allocate {@link IByteBuffer}.</param>
        /// <param name="src">src The <see cref="string" /> to encode.</param>
        /// <param name="encoding">charset The specified <see cref="Encoding" /></param>
        public static IByteBuffer EncodeString(IByteBufferAllocator alloc, string src, Encoding encoding) => EncodeString0(alloc, false, src, encoding, 0);

        /// <summary>
        ///     Encode the given <see cref="string" /> using the given <see cref="Encoding" /> into a new
        ///     <see cref="IByteBuffer" /> which
        ///     is allocated via the <see cref="IByteBufferAllocator" />.
        /// </summary>
        /// <param name="alloc">The <see cref="IByteBufferAllocator" /> to allocate {@link IByteBuffer}.</param>
        /// <param name="src">src The <see cref="string" /> to encode.</param>
        /// <param name="encoding">charset The specified <see cref="Encoding" /></param>
        /// <param name="extraCapacity">the extra capacity to alloc except the space for decoding.</param>
        public static IByteBuffer EncodeString(IByteBufferAllocator alloc, string src, Encoding encoding, int extraCapacity) => EncodeString0(alloc, false, src, encoding, extraCapacity);

        internal static IByteBuffer EncodeString0(IByteBufferAllocator alloc, bool enforceHeap, string src, Encoding encoding, int extraCapacity)
        {
            int length = encoding.GetMaxByteCount(src.Length) + extraCapacity;
            bool release = true;

            IByteBuffer dst = enforceHeap ? alloc.HeapBuffer(length) : alloc.Buffer(length);
            Contract.Assert(dst.HasArray, "Operation expects allocator to operate array-based buffers.");

            try
            {
                int written = encoding.GetBytes(src, 0, src.Length, dst.Array, dst.ArrayOffset + dst.WriterIndex);
                dst.SetWriterIndex(dst.WriterIndex + written);
                release = false;

                return dst;
            }
            finally
            {
                if (release)
                {
                    dst.Release();
                }
            }
        }

        public static string DecodeString(IByteBuffer src, int readerIndex, int len, Encoding encoding)
        {
            if (len == 0)
            {
                return string.Empty;
            }

            if (src.IoBufferCount == 1)
            {
                ArraySegment<byte> ioBuf = src.GetIoBuffer(readerIndex, len);
                return encoding.GetString(ioBuf.Array, ioBuf.Offset, ioBuf.Count);
            }
            else
            {
                int maxLength = encoding.GetMaxCharCount(len);
                IByteBuffer buffer = src.Allocator.HeapBuffer(maxLength);
                try
                {
                    buffer.WriteBytes(src, readerIndex, len);
                    ArraySegment<byte> ioBuf = buffer.GetIoBuffer();
                    return encoding.GetString(ioBuf.Array, ioBuf.Offset, ioBuf.Count);
                }
                finally
                {
                    // Release the temporary buffer again.
                    buffer.Release();
                }
            }
        }

        /// <summary>
        ///     Returns a multi-line hexadecimal dump of the specified {@link ByteBuf} that is easy to read by humans.
        /// </summary>
        public static string PrettyHexDump(IByteBuffer buffer) => PrettyHexDump(buffer, buffer.ReaderIndex, buffer.ReadableBytes);

        /// <summary>
        ///     Returns a multi-line hexadecimal dump of the specified {@link ByteBuf} that is easy to read by humans,
        ///     starting at the given {@code offset} using the given {@code length}.
        /// </summary>
        public static string PrettyHexDump(IByteBuffer buffer, int offset, int length) => HexUtil.DoPrettyHexDump(buffer, offset, length);

        /// <summary>
        ///     Appends the prettified multi-line hexadecimal dump of the specified {@link ByteBuf} to the specified
        ///     {@link StringBuilder} that is easy to read by humans.
        /// </summary>
        public static void AppendPrettyHexDump(StringBuilder dump, IByteBuffer buf) => AppendPrettyHexDump(dump, buf, buf.ReaderIndex, buf.ReadableBytes);

        /// <summary>
        ///     Appends the prettified multi-line hexadecimal dump of the specified {@link ByteBuf} to the specified
        ///     {@link StringBuilder} that is easy to read by humans, starting at the given {@code offset} using
        ///     the given {@code length}.
        /// </summary>
        public static void AppendPrettyHexDump(StringBuilder dump, IByteBuffer buf, int offset, int length) => HexUtil.DoAppendPrettyHexDump(dump, buf, offset, length);

        static class HexUtil
        {
            static readonly char[] HexdumpTable = new char[256 * 4];
            static readonly string Newline = StringUtil.Newline;
            static readonly string[] Byte2Hex = new string[256];
            static readonly string[] HexPadding = new string[16];
            static readonly string[] BytePadding = new string[16];
            static readonly char[] Byte2Char = new char[256];
            static readonly string[] HexDumpRowPrefixes = new string[(int)((uint)65536 >> 4)];

            static HexUtil()
            {
                char[] digits = "0123456789abcdef".ToCharArray();
                for (int i = 0; i < 256; i++)
                {
                    HexdumpTable[i << 1] = digits[(int)((uint)i >> 4 & 0x0F)];
                    HexdumpTable[(i << 1) + 1] = digits[i & 0x0F];
                }

                // Generate the lookup table for byte-to-hex-dump conversion
                for (int i = 0; i < Byte2Hex.Length; i++)
                {
                    Byte2Hex[i] = ' ' + StringUtil.ByteToHexStringPadded(i);
                }

                // Generate the lookup table for hex dump paddings
                for (int i = 0; i < HexPadding.Length; i++)
                {
                    int padding = HexPadding.Length - i;
                    var buf = new StringBuilder(padding * 3);
                    for (int j = 0; j < padding; j++)
                    {
                        buf.Append("   ");
                    }
                    HexPadding[i] = buf.ToString();
                }

                // Generate the lookup table for byte dump paddings
                for (int i = 0; i < BytePadding.Length; i++)
                {
                    int padding = BytePadding.Length - i;
                    var buf = new StringBuilder(padding);
                    for (int j = 0; j < padding; j++)
                    {
                        buf.Append(' ');
                    }
                    BytePadding[i] = buf.ToString();
                }

                // Generate the lookup table for byte-to-char conversion
                for (int i = 0; i < Byte2Char.Length; i++)
                {
                    if (i <= 0x1f || i >= 0x7f)
                    {
                        Byte2Char[i] = '.';
                    }
                    else
                    {
                        Byte2Char[i] = (char)i;
                    }
                }

                // Generate the lookup table for the start-offset header in each row (up to 64KiB).
                for (int i = 0; i < HexDumpRowPrefixes.Length; i++)
                {
                    var buf = new StringBuilder(12);
                    buf.Append(Environment.NewLine);
                    buf.Append((i << 4 & 0xFFFFFFFFL | 0x100000000L).ToString("X2"));
                    buf.Insert(buf.Length - 9, '|');
                    buf.Append('|');
                    HexDumpRowPrefixes[i] = buf.ToString();
                }
            }

            public static string DoHexDump(IByteBuffer buffer, int fromIndex, int length)
            {
                Contract.Requires(length >= 0);
                if (length == 0)
                {
                    return "";
                }
                int endIndex = fromIndex + length;
                var buf = new char[length << 1];

                int srcIdx = fromIndex;
                int dstIdx = 0;
                for (; srcIdx < endIndex; srcIdx++, dstIdx += 2)
                {
                    Array.Copy(
                        HexdumpTable, buffer.GetByte(srcIdx) << 1,
                        buf, dstIdx, 2);
                }

                return new string(buf);
            }

            public static string DoHexDump(byte[] array, int fromIndex, int length)
            {
                Contract.Requires(length >= 0);

                if (length == 0)
                {
                    return "";
                }

                int endIndex = fromIndex + length;
                var buf = new char[length << 1];

                int srcIdx = fromIndex;
                int dstIdx = 0;
                for (; srcIdx < endIndex; srcIdx++, dstIdx += 2)
                {
                    Array.Copy(HexdumpTable, (array[srcIdx] & 0xFF) << 1, buf, dstIdx, 2);
                }

                return new string(buf);
            }

            public static string DoPrettyHexDump(IByteBuffer buffer, int offset, int length)
            {
                if (length == 0)
                {
                    return string.Empty;
                }
                else
                {
                    int rows = length / 16 + (length % 15 == 0 ? 0 : 1) + 4;
                    var buf = new StringBuilder(rows * 80);
                    AppendPrettyHexDump(buf, buffer, offset, length);
                    return buf.ToString();
                }
            }

            public static void DoAppendPrettyHexDump(StringBuilder dump, IByteBuffer buf, int offset, int length)
            {
                if (MathUtil.IsOutOfBounds(offset, length, buf.Capacity))
                {
                    throw new IndexOutOfRangeException(
                        $"expected: 0 <= offset({offset}) <= offset + length({length}) <= buf.capacity({buf.Capacity}{')'}");
                }
                if (length == 0)
                {
                    return;
                }
                dump.Append(
                    "         +-------------------------------------------------+" +
                    Newline + "         |  0  1  2  3  4  5  6  7  8  9  a  b  c  d  e  f |" +
                    Newline + "+--------+-------------------------------------------------+----------------+");

                int startIndex = offset;
                int fullRows = (int)((uint)length >> 4);
                int remainder = length & 0xF;

                // Dump the rows which have 16 bytes.
                for (int row = 0; row < fullRows; row++)
                {
                    int rowStartIndex = (row << 4) + startIndex;

                    // Per-row prefix.
                    AppendHexDumpRowPrefix(dump, row, rowStartIndex);

                    // Hex dump
                    int rowEndIndex = rowStartIndex + 16;
                    for (int j = rowStartIndex; j < rowEndIndex; j++)
                    {
                        dump.Append(Byte2Hex[buf.GetByte(j)]);
                    }
                    dump.Append(" |");

                    // ASCII dump
                    for (int j = rowStartIndex; j < rowEndIndex; j++)
                    {
                        dump.Append(Byte2Char[buf.GetByte(j)]);
                    }
                    dump.Append('|');
                }

                // Dump the last row which has less than 16 bytes.
                if (remainder != 0)
                {
                    int rowStartIndex = (fullRows << 4) + startIndex;
                    AppendHexDumpRowPrefix(dump, fullRows, rowStartIndex);

                    // Hex dump
                    int rowEndIndex = rowStartIndex + remainder;
                    for (int j = rowStartIndex; j < rowEndIndex; j++)
                    {
                        dump.Append(Byte2Hex[buf.GetByte(j)]);
                    }
                    dump.Append(HexPadding[remainder]);
                    dump.Append(" |");

                    // Ascii dump
                    for (int j = rowStartIndex; j < rowEndIndex; j++)
                    {
                        dump.Append(Byte2Char[buf.GetByte(j)]);
                    }
                    dump.Append(BytePadding[remainder]);
                    dump.Append('|');
                }

                dump.Append(Newline + "+--------+-------------------------------------------------+----------------+");
            }

            static void AppendHexDumpRowPrefix(StringBuilder dump, int row, int rowStartIndex)
            {
                if (row < HexDumpRowPrefixes.Length)
                {
                    dump.Append(HexDumpRowPrefixes[row]);
                }
                else
                {
                    dump.Append(Environment.NewLine);
                    dump.Append((rowStartIndex & 0xFFFFFFFFL | 0x100000000L).ToString("X2"));
                    dump.Insert(dump.Length - 9, '|');
                    dump.Append('|');
                }
            }
        }

        public static bool IsText(IByteBuffer buf, int index, int length, Encoding encoding)
        {
            Contract.Requires(buf != null);
            Contract.Requires(encoding != null);

            int maxIndex = buf.ReaderIndex + buf.ReadableBytes;
            if (index < 0 || length < 0 || index > maxIndex - length)
            {
                throw new IndexOutOfRangeException($"index: {index}length: {length}");
            }
            if (ReferenceEquals(Encoding.UTF8, encoding))
            {
                return IsUtf8(buf, index, length);
            }
            else if (ReferenceEquals(Encoding.ASCII, encoding))
            {
                return IsAscii(buf, index, length);
            }
            else
            {
                try
                {
                    if (buf.IoBufferCount == 1)
                    {
                        ArraySegment<byte> segment = buf.GetIoBuffer();
                        encoding.GetChars(segment.Array, segment.Offset, segment.Count);
                    }
                    else
                    {
                        IByteBuffer heapBuffer = buf.Allocator.HeapBuffer(length);
                        try
                        {
                            heapBuffer.WriteBytes(buf, index, length);
                            ArraySegment<byte> segment = heapBuffer.GetIoBuffer();
                            encoding.GetChars(segment.Array, segment.Offset, segment.Count);
                        }
                        finally
                        {
                            heapBuffer.Release();
                        }
                    }
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        static readonly FindNonAscii AsciiByteProcessor = new FindNonAscii();

        sealed class FindNonAscii : IByteProcessor
        {
            public bool Process(byte value) => value < 0x80;
        }

        static bool IsAscii(IByteBuffer buf, int index, int length) => buf.ForEachByte(index, length, AsciiByteProcessor) == -1;

        static bool IsUtf8(IByteBuffer buf, int index, int length)
        {
            int endIndex = index + length;
            while (index < endIndex)
            {
                byte b1 = buf.GetByte(index++);
                byte b2, b3;
                if ((b1 & 0x80) == 0)
                {
                    // 1 byte
                    continue;
                }
                if ((b1 & 0xE0) == 0xC0)
                {
                    // 2 bytes
                    //
                    // Bit/Byte pattern
                    // 110xxxxx    10xxxxxx
                    // C2..DF      80..BF
                    if (index >= endIndex)
                    { // no enough bytes
                        return false;
                    }
                    b2 = buf.GetByte(index++);
                    if ((b2 & 0xC0) != 0x80)
                    { // 2nd byte not starts with 10
                        return false;
                    }
                    if ((b1 & 0xFF) < 0xC2)
                    { // out of lower bound
                        return false;
                    }
                }
                else if ((b1 & 0xF0) == 0xE0)
                {
                    // 3 bytes
                    //
                    // Bit/Byte pattern
                    // 1110xxxx    10xxxxxx    10xxxxxx
                    // E0          A0..BF      80..BF
                    // E1..EC      80..BF      80..BF
                    // ED          80..9F      80..BF
                    // E1..EF      80..BF      80..BF
                    if (index > endIndex - 2)
                    { // no enough bytes
                        return false;
                    }
                    b2 = buf.GetByte(index++);
                    b3 = buf.GetByte(index++);
                    if ((b2 & 0xC0) != 0x80 || (b3 & 0xC0) != 0x80)
                    { // 2nd or 3rd bytes not start with 10
                        return false;
                    }
                    if ((b1 & 0x0F) == 0x00 && (b2 & 0xFF) < 0xA0)
                    { // out of lower bound
                        return false;
                    }
                    if ((b1 & 0x0F) == 0x0D && (b2 & 0xFF) > 0x9F)
                    { // out of upper bound
                        return false;
                    }
                }
                else if ((b1 & 0xF8) == 0xF0)
                {
                    // 4 bytes
                    //
                    // Bit/Byte pattern
                    // 11110xxx    10xxxxxx    10xxxxxx    10xxxxxx
                    // F0          90..BF      80..BF      80..BF
                    // F1..F3      80..BF      80..BF      80..BF
                    // F4          80..8F      80..BF      80..BF
                    if (index > endIndex - 3)
                    { // no enough bytes
                        return false;
                    }
                    b2 = buf.GetByte(index++);
                    b3 = buf.GetByte(index++);
                    byte b4 = buf.GetByte(index++);
                    if ((b2 & 0xC0) != 0x80 || (b3 & 0xC0) != 0x80 || (b4 & 0xC0) != 0x80)
                    {
                        // 2nd, 3rd or 4th bytes not start with 10
                        return false;
                    }
                    if ((b1 & 0xFF) > 0xF4 // b1 invalid
                        || (b1 & 0xFF) == 0xF0 && (b2 & 0xFF) < 0x90    // b2 out of lower bound
                        || (b1 & 0xFF) == 0xF4 && (b2 & 0xFF) > 0x8F)
                    { // b2 out of upper bound
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        public static unsafe int SingleToInt32Bits(float value)
        {
            return *(int*)(&value);
        }

        public static unsafe float Int32BitsToSingle(int value)
        {
            return *(float*)(&value);
        }

        /// <summary>
        ///     Toggles the endianness of the specified 64-bit long integer.
        /// </summary>
        public static long SwapLong(long value)
            => ((SwapInt((int)value) & 0xFFFFFFFF) << 32)
                | (SwapInt((int)(value >> 32)) & 0xFFFFFFFF);

        /// <summary>
        ///     Toggles the endianness of the specified 32-bit integer.
        /// </summary>
        public static int SwapInt(int value)
            => ((SwapShort((short)value) & 0xFFFF) << 16)
                | (SwapShort((short)(value >> 16)) & 0xFFFF);

        /// <summary>
        ///     Toggles the endianness of the specified 16-bit integer.
        /// </summary>
        public static short SwapShort(short value) => (short)(((value & 0xFF) << 8) | (value >> 8) & 0xFF);

    }
}