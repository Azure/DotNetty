// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    public static class IntegerExtensions
    {
        static readonly int[] MultiplyDeBruijnBitPosition =
        {
            0, 9, 1, 10, 13, 21, 2, 29, 11, 14, 16, 18, 22, 25, 3, 30,
            8, 12, 20, 28, 15, 17, 24, 7, 19, 27, 23, 6, 26, 5, 4, 31
        };

        public const int SizeInBits = sizeof(int) * 8;

        public static int RoundUpToPowerOfTwo(int res)
        {
            if (res <= 2)
            {
                return 2;
            }
            res--;
            res |= res >> 1;
            res |= res >> 2;
            res |= res >> 4;
            res |= res >> 8;
            res |= res >> 16;
            res++;
            return res;
        }

        public static int Log2(int v)
        {
            v |= v >> 1; // first round down to one less than a power of 2 
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;

            return MultiplyDeBruijnBitPosition[unchecked((uint)(v * 0x07C4ACDDU) >> 27)];
        }
    }
}