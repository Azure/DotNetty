// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Utilities;

    static class PlatformDependent0
    {
        internal static readonly int HashCodeAsciiSeed = unchecked((int)0xc2b2ae35);
        internal static readonly int HashCodeC1 = unchecked((int)0xcc9e2d51);
        internal static readonly int HashCodeC2 = 0x1b873593;

        internal static unsafe bool ByteArrayEquals(byte* bytes1, byte* bytes2, int length)
        {
            if (length <= 0)
            {
                return true;
            }

            byte* baseOffset1 = bytes1;
            byte* baseOffset2 = bytes2;
            int remainingBytes = length & 7;
            byte* end = baseOffset1 + remainingBytes;
            for (byte* i = baseOffset1 - 8 + length, j = baseOffset2 - 8 + length; i >= end; i -= 8, j -= 8)
            {
                if (Unsafe.ReadUnaligned<long>(i) != Unsafe.ReadUnaligned<long>(j))
                {
                    return false;
                }
            }

            if (remainingBytes >= 4)
            {
                remainingBytes -= 4;
                if (Unsafe.ReadUnaligned<int>(baseOffset1 + remainingBytes) != Unsafe.ReadUnaligned<int>(baseOffset2 + remainingBytes))
                {
                    return false;
                }
            }
            if (remainingBytes >= 2)
            {
                return Unsafe.ReadUnaligned<short>(baseOffset1) == Unsafe.ReadUnaligned<short>(baseOffset2) 
                    && (remainingBytes == 2 || *(bytes1 + 2) == *(bytes2 + 2));
            }
            return *baseOffset1 == *baseOffset2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe int HashCodeAscii(byte* bytes,  int length)
        {
            int hash = HashCodeAsciiSeed;
            int remainingBytes = length & 7;
            byte* end = bytes + remainingBytes;
            for (byte* i = bytes - 8 + length; i >= end; i -= 8)
            {
                hash = HashCodeAsciiCompute(Unsafe.ReadUnaligned<long>(i), hash);
            }

            switch (remainingBytes)
            {
                case 7:
                    return ((hash * HashCodeC1 + HashCodeAsciiSanitize(*bytes))
                        * HashCodeC2 + HashCodeAsciiSanitize(Unsafe.ReadUnaligned<short>(bytes + 1)))
                        * HashCodeC1 + HashCodeAsciiSanitize(Unsafe.ReadUnaligned<int>(bytes + 3));
                case 6:
                    return (hash * HashCodeC1 + HashCodeAsciiSanitize(Unsafe.ReadUnaligned<short>(bytes)))
                        * HashCodeC2 + HashCodeAsciiSanitize(Unsafe.ReadUnaligned<int>(bytes + 2));
                case 5:
                    return (hash * HashCodeC1 + HashCodeAsciiSanitize(*bytes))
                        * HashCodeC2 + HashCodeAsciiSanitize(Unsafe.ReadUnaligned<int>(bytes + 1));
                case 4:
                    return hash * HashCodeC1 + HashCodeAsciiSanitize(Unsafe.ReadUnaligned<int>(bytes));
                case 3:
                    return (hash * HashCodeC1 + HashCodeAsciiSanitize(*bytes))
                        * HashCodeC2 + HashCodeAsciiSanitize(Unsafe.ReadUnaligned<short>(bytes + 1));
                case 2:
                    return hash * HashCodeC1 + HashCodeAsciiSanitize(Unsafe.ReadUnaligned<short>(bytes));
                case 1:
                    return hash * HashCodeC1 + HashCodeAsciiSanitize(*bytes);
                default:
                    return hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int HashCodeAsciiCompute(long value, int hash)
        {
            // masking with 0x1f reduces the number of overall bits that impact the hash code but makes the hash
            // code the same regardless of character case (upper case or lower case hash is the same).
            unchecked
            {
                return hash * HashCodeC1 +
                    // Low order int
                    HashCodeAsciiSanitize((int)value) * HashCodeC2 +
                    // High order int
                    (int)(value & 0x1f1f1f1f00000000L).RightUShift(32);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int HashCodeAsciiSanitize(int value) => value & 0x1f1f1f1f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int HashCodeAsciiSanitize(short value) =>  value & 0x1f1f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int HashCodeAsciiSanitize(byte value) => value & 0x1f;
    }
}
