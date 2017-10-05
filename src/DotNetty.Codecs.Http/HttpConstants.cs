// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System.Text;
    using DotNetty.Buffers;

    public static class HttpConstants
    {
        // Horizontal space
        public const byte HorizontalSpace = 32;

        // Horizontal tab
        public const byte HorizontalTab = 9;

        // Carriage return
        public const byte CarriageReturn = 13;

        // Equals '='
        public const byte EqualsSign = 61;

        // Line feed character
        public const byte LineFeed = 10;

        // Colon ':'
        public const byte Colon = 58;

        // Semicolon ';'
        public const byte Semicolon = 59;

        // Comma ','
        public const byte Comma = 44;

        // Double quote '"'
        public const byte DoubleQuote = (byte)'"';

         // Default character set (UTF-8)
        public static readonly Encoding DefaultEncoding = Encoding.UTF8;

        // Horizontal space in char
        public static readonly char HorizontalSpaceChar = (char)HorizontalSpace;

        // For HttpObjectEncoder
        internal static readonly int CrlfShort = (CarriageReturn << 8) | LineFeed;

        internal static readonly int ZeroCrlfMedium = ('0' << 16) | CrlfShort;

        internal static readonly byte[] ZeroCrlfCrlf = { (byte)'0', CarriageReturn, LineFeed, CarriageReturn, LineFeed };

        internal static readonly IByteBuffer CrlfBuf = Unpooled.UnreleasableBuffer(Unpooled.WrappedBuffer(new[] { CarriageReturn, LineFeed }));

        internal static readonly IByteBuffer ZeroCrlfCrlfBuf = Unpooled.UnreleasableBuffer(Unpooled.WrappedBuffer(ZeroCrlfCrlf));
    }
}
