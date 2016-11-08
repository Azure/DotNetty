// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Base64
{
    public struct Base64Dialect
    {
        /// <summary>
        ///     http://www.faqs.org/rfcs/rfc3548.html
        ///     Table 1: The Base 64 Alphabet
        /// </summary>
        public static readonly Base64Dialect STANDARD = new Base64Dialect(
            new byte[]
            {
                (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E',
                (byte)'F', (byte)'G', (byte)'H', (byte)'I', (byte)'J',
                (byte)'K', (byte)'L', (byte)'M', (byte)'N', (byte)'O',
                (byte)'P', (byte)'Q', (byte)'R', (byte)'S', (byte)'T',
                (byte)'U', (byte)'V', (byte)'W', (byte)'X', (byte)'Y',
                (byte)'Z', (byte)'a', (byte)'b', (byte)'c', (byte)'d',
                (byte)'e', (byte)'f', (byte)'g', (byte)'h', (byte)'i',
                (byte)'j', (byte)'k', (byte)'l', (byte)'m', (byte)'n',
                (byte)'o', (byte)'p', (byte)'q', (byte)'r', (byte)'s',
                (byte)'t', (byte)'u', (byte)'v', (byte)'w', (byte)'x',
                (byte)'y', (byte)'z', (byte)'0', (byte)'1', (byte)'2',
                (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7',
                (byte)'8', (byte)'9', (byte)'+', (byte)'/'
            },
            new sbyte[]
            {
                -9, -9, -9, -9, -9, -9,
                -9, -9, -9, // Decimal  0 -  8
                -5, -5, // Whitespace: Tab and Linefeed
                -9, -9, // Decimal 11 - 12
                -5, // Whitespace: Carriage Return
                -9, -9, -9, -9, -9, -9, -9, -9, -9, -9, -9, -9, -9, // Decimal 14 - 26
                -9, -9, -9, -9, -9, // Decimal 27 - 31
                -5, // Whitespace: Space
                -9, -9, -9, -9, -9, -9, -9, -9, -9, -9, // Decimal 33 - 42
                62, // Plus sign at decimal 43
                -9, -9, -9, // Decimal 44 - 46
                63, // Slash at decimal 47
                52, 53, 54, 55, 56, 57, 58, 59, 60, 61, // Numbers zero through nine
                -9, -9, -9, // Decimal 58 - 60
                -1, // Equals sign at decimal 61
                -9, -9, -9, // Decimal 62 - 64
                0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, // Letters 'A' through 'N'
                14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, // Letters 'O' through 'Z'
                -9, -9, -9, -9, -9, -9, // Decimal 91 - 96
                26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, // Letters 'a' through 'm'
                39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, // Letters 'n' through 'z'
                -9, -9, -9, -9, // Decimal 123 - 126
            },
            true);

        /// <summary>
        ///     http://www.faqs.org/rfcs/rfc3548.html
        ///     Table 2: The "URL and Filename safe" Base 64 Alphabet
        /// </summary>
        public static readonly Base64Dialect URL_SAFE = new Base64Dialect(
            new byte[]
            {
                (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E',
                (byte)'F', (byte)'G', (byte)'H', (byte)'I', (byte)'J',
                (byte)'K', (byte)'L', (byte)'M', (byte)'N', (byte)'O',
                (byte)'P', (byte)'Q', (byte)'R', (byte)'S', (byte)'T',
                (byte)'U', (byte)'V', (byte)'W', (byte)'X', (byte)'Y',
                (byte)'Z', (byte)'a', (byte)'b', (byte)'c', (byte)'d',
                (byte)'e', (byte)'f', (byte)'g', (byte)'h', (byte)'i',
                (byte)'j', (byte)'k', (byte)'l', (byte)'m', (byte)'n',
                (byte)'o', (byte)'p', (byte)'q', (byte)'r', (byte)'s',
                (byte)'t', (byte)'u', (byte)'v', (byte)'w', (byte)'x',
                (byte)'y', (byte)'z', (byte)'0', (byte)'1', (byte)'2',
                (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7',
                (byte)'8', (byte)'9', (byte)'-', (byte)'_'
            },
            new sbyte[]
            {
                -9, -9, -9, -9, -9, -9,
                -9, -9, -9, // Decimal  0 -  8
                -5, -5, // Whitespace: Tab and Linefeed
                -9, -9, // Decimal 11 - 12
                -5, // Whitespace: Carriage Return
                -9, -9, -9, -9, -9, -9, -9, -9, -9, -9, -9, -9, -9, // Decimal 14 - 26
                -9, -9, -9, -9, -9, // Decimal 27 - 31
                -5, // Whitespace: Space
                -9, -9, -9, -9, -9, -9, -9, -9, -9, -9, // Decimal 33 - 42
                -9, // Plus sign at decimal 43
                -9, // Decimal 44
                62, // Minus sign at decimal 45
                -9, // Decimal 46
                -9, // Slash at decimal 47
                52, 53, 54, 55, 56, 57, 58, 59, 60, 61, // Numbers zero through nine
                -9, -9, -9, // Decimal 58 - 60
                -1, // Equals sign at decimal 61
                -9, -9, -9, // Decimal 62 - 64
                0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, // Letters 'A' through 'N'
                14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, // Letters 'O' through 'Z'
                -9, -9, -9, -9, // Decimal 91 - 94
                63, // Underscore at decimal 95
                -9, // Decimal 96
                26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, // Letters 'a' through 'm'
                39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, // Letters 'n' through 'z'
                -9, -9, -9, -9, // Decimal 123 - 126
            },
            false);

        public readonly byte[] alphabet;
        public readonly sbyte[] decodabet;
        public readonly bool breakLinesByDefault;

        Base64Dialect(byte[] alphabet, sbyte[] decodabet, bool breakLinesByDefault)
        {
            this.alphabet = alphabet;
            this.decodabet = decodabet;
            this.breakLinesByDefault = breakLinesByDefault;
        }
    }
}