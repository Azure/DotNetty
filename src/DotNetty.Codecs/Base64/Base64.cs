// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Base64
{
    using System;
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;

    public static class Base64
    {
        const int MAX_LINE_LENGTH = 76;
        const byte EQUALS_SIGN = (byte)'='; //pad
        const byte NEW_LINE = (byte)'\n';
        const sbyte WHITE_SPACE_ENC = -5; // Indicates white space in encoding
        const sbyte EQUALS_SIGN_ENC = -1; // Indicates equals sign in encoding

        public static IByteBuffer Encode(IByteBuffer src) => Encode(src, Base64Dialect.STANDARD);

        public static IByteBuffer Encode(IByteBuffer src, Base64Dialect dialect) => Encode(src, src.ReaderIndex, src.ReadableBytes, dialect.breakLinesByDefault, dialect);

        public static IByteBuffer Encode(IByteBuffer src, bool breakLines, Base64Dialect dialect) => Encode(src, src.ReaderIndex, src.ReadableBytes, breakLines, dialect);

        public static IByteBuffer Encode(IByteBuffer src, int offset, int length, bool breakLines, Base64Dialect dialect) => Encode(src, offset, length, breakLines, dialect, src.Allocator);

        static unsafe int EncodeUsingPointer(byte* alphabet, IByteBuffer src, IByteBuffer dest, int offset, int length, bool breakLines)
        {
            //avoid unnecessary range checking
            fixed (byte* srcArray = src.Array, d = dest.Array)
            {
                byte* destArray = d + dest.ArrayOffset + dest.WriterIndex;
                int j = 0;
                int charCount = 0;
                //the real offset of the array, is ArrayOfffset + offset
                int i = src.ArrayOffset + offset;
                int remainderLength = length % 3;
                int calcLength = src.ArrayOffset + offset + length - remainderLength;
                for (; i < calcLength; i += 3)
                {
                    if (breakLines)
                    {
                        if (charCount == MAX_LINE_LENGTH)
                        {
                            destArray[j++] = NEW_LINE;
                            charCount = 0;
                        }
                        charCount += 4;
                    }

                    destArray[j + 0] = alphabet[(srcArray[i] & 0xfc) >> 2];
                    destArray[j + 1] = alphabet[((srcArray[i] & 0x03) << 4) | ((srcArray[i + 1] & 0xf0) >> 4)];
                    destArray[j + 2] = alphabet[((srcArray[i + 1] & 0x0f) << 2) | ((srcArray[i + 2] & 0xc0) >> 6)];
                    destArray[j + 3] = alphabet[(srcArray[i + 2] & 0x3f)];
                    j += 4;
                }

                i = calcLength;

                if (breakLines && (remainderLength != 0) && (charCount == MAX_LINE_LENGTH))
                {
                    destArray[j++] = NEW_LINE;
                }
                switch (remainderLength)
                {
                    case 2:
                        destArray[j + 0] = alphabet[(srcArray[i] & 0xfc) >> 2];
                        destArray[j + 1] = alphabet[((srcArray[i] & 0x03) << 4) | ((srcArray[i + 1] & 0xf0) >> 4)];
                        destArray[j + 2] = alphabet[(srcArray[i + 1] & 0x0f) << 2];
                        destArray[j + 3] = EQUALS_SIGN;
                        j += 4;
                        break;
                    case 1:
                        destArray[j + 0] = alphabet[(srcArray[i] & 0xfc) >> 2];
                        destArray[j + 1] = alphabet[(srcArray[i] & 0x03) << 4];
                        destArray[j + 2] = EQUALS_SIGN;
                        destArray[j + 3] = EQUALS_SIGN;
                        j += 4;
                        break;
                }
                //remove last byte if it's NewLine
                int destLength = destArray[j - 1] == NEW_LINE ? j - 1 : j;
                return destLength;
            }
        }

        static unsafe int EncodeUsingGetSet(byte* alphabet, IByteBuffer src, IByteBuffer dest, int offset, int length, bool breakLines)
        {
            int i = 0;
            int j = 0;
            int charCount = 0;
            int remainderLength = length % 3;
            byte b0 = 0, b1 = 0, b2 = 0;
            int calcLength = offset + length - remainderLength;
            for (i = offset; i < calcLength; i += 3)
            {
                if (breakLines)
                {
                    if (charCount == MAX_LINE_LENGTH)
                    {
                        dest.SetByte(j++, NEW_LINE);
                        charCount = 0;
                    }
                    charCount += 4;
                }
                b0 = src.GetByte(i);
                b1 = src.GetByte(i + 1);
                b2 = src.GetByte(i + 2);

                dest.SetByte(j + 0, alphabet[(b0 & 0xfc) >> 2]);
                dest.SetByte(j + 1, alphabet[((b0 & 0x03) << 4) | ((b1 & 0xf0) >> 4)]);
                dest.SetByte(j + 2, alphabet[((b1 & 0x0f) << 2) | ((b2 & 0xc0) >> 6)]);
                dest.SetByte(j + 3, alphabet[(b2 & 0x3f)]);
                j += 4;
            }

            i = calcLength;

            if (breakLines && (remainderLength != 0) && (charCount == MAX_LINE_LENGTH))
            {
                dest.SetByte(j++, NEW_LINE);
            }
            switch (remainderLength)
            {
                case 2:
                    b0 = src.GetByte(i);
                    b1 = src.GetByte(i + 1);
                    dest.SetByte(j + 0, alphabet[(b0 & 0xfc) >> 2]);
                    dest.SetByte(j + 1, alphabet[((b0 & 0x03) << 4) | ((b1 & 0xf0) >> 4)]);
                    dest.SetByte(j + 2, alphabet[(b1 & 0x0f) << 2]);
                    dest.SetByte(j + 3, EQUALS_SIGN);
                    j += 4;
                    break;
                case 1:
                    b0 = src.GetByte(i);
                    dest.SetByte(j + 0, alphabet[(b0 & 0xfc) >> 2]);
                    dest.SetByte(j + 1, alphabet[(b0 & 0x03) << 4]);
                    dest.SetByte(j + 2, EQUALS_SIGN);
                    dest.SetByte(j + 3, EQUALS_SIGN);
                    j += 4;
                    break;
            }
            //remove last byte if it's NewLine
            int destLength = dest.GetByte(j - 1) == NEW_LINE ? j - 1 : j;
            return destLength;
        }

        public static unsafe IByteBuffer Encode(IByteBuffer src, int offset, int length, bool breakLines, Base64Dialect dialect, IByteBufferAllocator allocator)
        {
            if (src == null)
            {
                throw new ArgumentNullException(nameof(src));
            }
            if (dialect.alphabet == null)
            {
                throw new ArgumentNullException(nameof(dialect.alphabet));
            }
            Contract.Assert(dialect.alphabet.Length == 64, "alphabet.Length must be 64!");
            if ((offset < src.ReaderIndex) || (offset + length > src.ReaderIndex + src.ReadableBytes))
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            if (length <= 0)
            {
                return Unpooled.Empty;
            }

            int remainderLength = length % 3;
            int outLength = length / 3 * 4 + (remainderLength > 0 ? 4 : 0);
            outLength += breakLines ? outLength / MAX_LINE_LENGTH : 0;
            IByteBuffer dest = allocator.Buffer(outLength);
            int destLength = 0;
            int destIndex = dest.WriterIndex;

            fixed (byte* alphabet = dialect.alphabet)
            {
                if ((src.IoBufferCount == 1) && (dest.IoBufferCount == 1))
                {
                    destLength = EncodeUsingPointer(alphabet, src, dest, offset, length, breakLines);
                }
                else
                {
                    destLength = EncodeUsingGetSet(alphabet, src, dest, offset, length, breakLines);
                }
            }
            return dest.SetIndex(destIndex, destIndex + destLength);
        }

        public static IByteBuffer Decode(IByteBuffer src) => Decode(src, Base64Dialect.STANDARD);

        public static IByteBuffer Decode(IByteBuffer src, Base64Dialect dialect) => Decode(src, src.ReaderIndex, src.ReadableBytes, dialect);

        public static IByteBuffer Decode(IByteBuffer src, int offset, int length, Base64Dialect dialect) => Decode(src, offset, length, dialect, src.Allocator);

        static unsafe int DecodeUsingPointer(IByteBuffer src, IByteBuffer dest, sbyte* decodabet, int offset, int length)
        {
            int charCount = 0;
            fixed (byte* srcArray = src.Array, d = dest.Array)
            {
                byte* destArray = d + dest.ArrayOffset + dest.WriterIndex;
                byte* b4 = stackalloc byte[4];
                int b4Count = 0;
                int i = src.ArrayOffset + offset;
                int calcLength = src.ArrayOffset + offset + length;
                for (; i < calcLength; ++i)
                {
                    sbyte value = (sbyte)(srcArray[i] & 0x7F);
                    if (decodabet[value] < WHITE_SPACE_ENC)
                    {
                        throw new ArgumentException(string.Format("bad Base64 input character at {0}:{1}",
                            i, value));
                    }
                    if (decodabet[value] >= EQUALS_SIGN_ENC)
                    {
                        b4[b4Count++] = (byte)value;
                        if (b4Count <= 3)
                            continue;

                        if (b4[2] == EQUALS_SIGN)
                        {
                            int output = ((decodabet[b4[0]] & 0xFF) << 18) |
                                ((decodabet[b4[1]] & 0xFF) << 12);
                            destArray[charCount++] = (byte)((uint)output >> 16);
                        }
                        else if (b4[3] == EQUALS_SIGN)
                        {
                            int output = ((decodabet[b4[0]] & 0xFF) << 18) |
                                ((decodabet[b4[1]] & 0xFF) << 12) |
                                ((decodabet[b4[2]] & 0xFF) << 6);
                            destArray[charCount++] = (byte)((uint)output >> 16);
                            destArray[charCount++] = (byte)((uint)output >> 8);
                        }
                        else
                        {
                            int output = ((decodabet[b4[0]] & 0xFF) << 18) |
                                ((decodabet[b4[1]] & 0xFF) << 12) |
                                ((decodabet[b4[2]] & 0xFF) << 6) |
                                ((decodabet[b4[3]] & 0xFF) << 0);
                            destArray[charCount++] = (byte)((uint)output >> 16);
                            destArray[charCount++] = (byte)((uint)output >> 8);
                            destArray[charCount++] = (byte)((uint)output >> 0);
                        }

                        b4Count = 0;
                        if (value == EQUALS_SIGN)
                        {
                            break;
                        }
                    }
                }
            }
            return charCount;
        }

        static unsafe int DecodeUsingGetSet(IByteBuffer src, IByteBuffer dest, sbyte* decodabet, int offset, int length)
        {
            int charCount = 0;

            byte* b4 = stackalloc byte[4];
            int b4Count = 0;
            int i = 0;

            for (i = offset; i < offset + length; ++i)
            {
                sbyte value = (sbyte)(src.GetByte(i) & 0x7F);
                if (decodabet[value] < WHITE_SPACE_ENC)
                {
                    throw new ArgumentException(string.Format("bad Base64 input character at {0}:{1}",
                        i, value));
                }
                if (decodabet[value] >= EQUALS_SIGN_ENC)
                {
                    b4[b4Count++] = (byte)value;
                    if (b4Count <= 3)
                        continue;

                    if (b4[2] == EQUALS_SIGN)
                    {
                        int output = ((decodabet[b4[0]] & 0xFF) << 18) |
                            ((decodabet[b4[1]] & 0xFF) << 12);
                        dest.SetByte(charCount++, (int)((uint)output >> 16));
                    }
                    else if (b4[3] == EQUALS_SIGN)
                    {
                        int output = ((decodabet[b4[0]] & 0xFF) << 18) |
                            ((decodabet[b4[1]] & 0xFF) << 12) |
                            ((decodabet[b4[2]] & 0xFF) << 6);
                        dest.SetByte(charCount++, (int)((uint)output >> 16));
                        dest.SetByte(charCount++, (int)((uint)output >> 8));
                    }
                    else
                    {
                        int output = ((decodabet[b4[0]] & 0xFF) << 18) |
                            ((decodabet[b4[1]] & 0xFF) << 12) |
                            ((decodabet[b4[2]] & 0xFF) << 6) |
                            ((decodabet[b4[3]] & 0xFF) << 0);
                        dest.SetByte(charCount++, (int)((uint)output >> 16));
                        dest.SetByte(charCount++, (int)((uint)output >> 8));
                        dest.SetByte(charCount++, (int)((uint)output >> 0));
                    }

                    b4Count = 0;
                    if (value == EQUALS_SIGN)
                    {
                        break;
                    }
                }
            }
            return charCount;
        }

        public static unsafe IByteBuffer Decode(IByteBuffer src, int offset, int length, Base64Dialect dialect, IByteBufferAllocator allocator)
        {
            if (src == null)
            {
                throw new ArgumentNullException(nameof(src));
            }
            if (dialect.decodabet == null)
            {
                throw new ArgumentNullException(nameof(dialect.decodabet));
            }
            if ((offset < src.ReaderIndex) || (offset + length > src.ReaderIndex + src.ReadableBytes))
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            Contract.Assert(dialect.decodabet.Length == 127, "decodabet.Length must be 127!");
            if (length <= 0)
            {
                return Unpooled.Empty;
            }

            int outLength = length * 3 / 4;
            IByteBuffer dest = allocator.Buffer(outLength);
            int charCount = 0;
            int destIndex = dest.WriterIndex;

            fixed (sbyte* decodabet = dialect.decodabet)
            {
                if ((src.IoBufferCount == 1) && (dest.IoBufferCount == 1))
                {
                    charCount = DecodeUsingPointer(src, dest, decodabet, offset, length);
                }
                else
                {
                    charCount = DecodeUsingGetSet(src, dest, decodabet, offset, length);
                }
            }

            return dest.SetIndex(destIndex, destIndex + charCount);
        }
    }
}