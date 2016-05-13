// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using DotNetty.Buffers;

    public class Delimiters
    {
        /// <summary>Returns a null (0x00) delimiter, which could be used for Flash XML socket or any similar protocols</summary>
        public static IByteBuffer[] NullDelimiter() => new[] { Unpooled.WrappedBuffer(new byte[] { 0 }) };

        /// <summary>
        ///     Returns {@code CR ('\r')} and {@code LF ('\n')} delimiters, which could
        ///     be used for text-based line protocols.
        /// </summary>
        public static IByteBuffer[] LineDelimiter()
        {
            return new[]
            {
                Unpooled.WrappedBuffer(new[] { (byte)'\r', (byte)'\n' }),
                Unpooled.WrappedBuffer(new[] { (byte)'\n' }),
            };
        }

        Delimiters()
        {
            // Unused
        }
    }
}