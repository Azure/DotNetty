namespace DotNetty.Codecs.CoapTcp.util
{
    using System;
    using System.Collections.Generic;

    class VariableLengthIntegerCodec
    {
        private const byte FOUR_BIT_CODE_MAX = 12;
        private const byte EIGHT_BIT_CODE = 13;
        private const byte SIXTEEN_BIT_CODE = 14;
        private const byte THIRTYTWO_BIT_CODE = 15;

        private static int FOUR_BIT_MAX_VALUE = 12;
        private static int EIGHT_BIT_MAX_VALUE = 268;
        private static int SIXTEEN_BIT_MAX_VALUE = 65804;

        public static int ExtraBytesForFourBitCode(byte code)
        {
            if (code <= FOUR_BIT_CODE_MAX)
            {
                return 0;
            }

            switch (code)
            {
                case EIGHT_BIT_CODE:
                    return 1;
                case SIXTEEN_BIT_CODE:
                    return 2;
                default: // THIRTYTWO_BIT_CODE
                    return 4;
            }
        }

        public static int OffsetForFourBitCode(byte code)
        {
            if (code < FOUR_BIT_CODE_MAX)
            {
                return 0;
            }
            switch (code)
            {
                case EIGHT_BIT_CODE:
                    return FOUR_BIT_MAX_VALUE+1;
                case SIXTEEN_BIT_CODE:
                    return EIGHT_BIT_MAX_VALUE+1;
                case THIRTYTWO_BIT_CODE:
                    return SIXTEEN_BIT_MAX_VALUE+1;
            }
            return 0;
        }

        /// <summary>
        /// Encode generate a 4-bit code (first item), an encoded x-byte int
        /// (x=0,1,2,4; second item) as well as the number of bytes x for 
        /// the int (third item).
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static Tuple<byte, int, int> Encode(int value)
        {
            if (value <= FOUR_BIT_MAX_VALUE)
            {
                return Tuple.Create((byte)value, 0, 0);
            }
            if (value <= EIGHT_BIT_MAX_VALUE)
            {
                return Tuple.Create((byte)EIGHT_BIT_CODE, value - FOUR_BIT_MAX_VALUE - 1, 1);
            }
            if (value <= SIXTEEN_BIT_MAX_VALUE)
            {
                return Tuple.Create((byte)SIXTEEN_BIT_CODE, value - EIGHT_BIT_MAX_VALUE - 1, 2);
            }
            return Tuple.Create(THIRTYTWO_BIT_CODE, value - SIXTEEN_BIT_MAX_VALUE - 1, 4);
        }
    }
}
