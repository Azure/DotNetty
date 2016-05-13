// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System.Runtime.CompilerServices;

    public static class BitOps
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RightUShift(this int value, int bits) => unchecked((int)((uint)value >> bits));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long RightUShift(this long value, int bits) => unchecked((long)((ulong)value >> bits));
    }
}