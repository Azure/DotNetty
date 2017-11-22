// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    using System.Runtime.CompilerServices;

    static class PlatformDependent0
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe bool ByteArrayEquals(byte* bytes1, int startPos1, byte* bytes2, int startPos2, int length)
        {
            if (length <= 0)
            {
                return true;
            }

            byte* baseOffset1 = bytes1 + startPos1;
            byte* baseOffset2 = bytes2 + startPos2;
            int remainingBytes = length & 7;
            byte* end = baseOffset1 + remainingBytes;
            for (byte* i = baseOffset1 - 8 + length, j = baseOffset2 - 8 + length; i >= end; i -= 8, j -= 8)
            {
                if (Unsafe.Read<long>(i) != Unsafe.Read<long>(j))
                {
                    return false;
                }
            }

            if (remainingBytes >= 4)
            {
                remainingBytes -= 4;
                if (Unsafe.Read<int>(baseOffset1 + remainingBytes) != Unsafe.Read<int>(baseOffset2 + remainingBytes))
                {
                    return false;
                }
            }
            if (remainingBytes >= 2)
            {
                return Unsafe.Read<short>(baseOffset1) == Unsafe.Read<short>(baseOffset2) 
                    && (remainingBytes == 2 || *(bytes1 + startPos1 + 2) == *(bytes2 + startPos2 + 2));
            }
            return *baseOffset1 == *baseOffset2;
        }
    }
}
