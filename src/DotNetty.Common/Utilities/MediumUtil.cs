// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;

    public static class MediumUtil
    {
        //High byte bit-mask used when a 24-bit integer is stored within a 32-bit integer.
        const uint MediumBitMask = 0xff000000;

        public static int ToMediumInt(this int value)
        {
            // Check bit 23, the sign bit in a signed 24-bit integer
            if ((value & 0x00800000) > 0)
            {
                // If the sign-bit is set, this number will be negative - set all high-byte bits (keeps 32-bit number in 24-bit range)
                value |= unchecked((int)MediumBitMask);
            }
            else
            {
                // If the sign-bit is not set, this number will be positive - clear all high-byte bits (keeps 32-bit number in 24-bit range)
                value &= ~unchecked((int)MediumBitMask);
            }

            return value;
        }

        public static int ToUnsignedMediumInt(this int value)
        {
            return (int)((uint)value & ~MediumBitMask);
        }
    }
}
