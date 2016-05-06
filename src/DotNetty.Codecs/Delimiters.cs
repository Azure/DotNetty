// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using DotNetty.Buffers;

namespace DotNetty.Codecs
{
    public class Delimiters
    {
        /// <summary>Returns a null (0x00) delimiter, which could be used for Flash XML socket or any similar protocols</summary>
        public static IByteBuffer[] NullDelimiter()
        {
            return new IByteBuffer[] {	Unpooled.WrappedBuffer(new byte[] { 0 }) };
        }

        /// <summary>Returns {@code CR ('\r')} and {@code LF ('\n')} delimiters, which could
        /// be used for text-based line protocols.</summary>
        public static IByteBuffer[] LineDelimiter()
        {
            return new IByteBuffer[]
                {
                    Unpooled.WrappedBuffer(new byte[] { (byte)'\r', (byte)'\n' }),
                    Unpooled.WrappedBuffer(new byte[] { (byte)'\n' }),
                };
        }

        private Delimiters()
        {
            // Unused
        }
    }
}
