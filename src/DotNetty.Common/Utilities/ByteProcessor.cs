// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Diagnostics.Contracts;
    /// <summary>
    /// Provides a mechanism to iterate over a collection of bytes.
    /// </summary>
    public abstract class ByteProcessor
    {
        /// <summary>
        /// A <see cref="ByteProcessor"/> which finds the first appearance of a specific byte.
        /// </summary>
        sealed public class IndexOfProcessor : ByteProcessor
        {
            readonly byte byteToFind;

            public IndexOfProcessor(byte byteToFind)
            {
                this.byteToFind = byteToFind;
            }

            public override bool Process(byte value)
            {
                return value != byteToFind;
            }
        }

        sealed public class IndexNotOfProcessor : ByteProcessor
        {
            readonly byte byteToNotFind;

            public IndexNotOfProcessor(byte byteToNotFind)
            {
                this.byteToNotFind = byteToNotFind;
            }

            public override bool Process(byte value)
            {
                return value == byteToNotFind;
            }
        }

        sealed public class CustomProcessor : ByteProcessor
        {
            readonly Func<byte, bool> customHandler;

            public CustomProcessor(Func<byte, bool> customHandler)
            {
                Contract.Assert(customHandler != null, "'customHandler' is required parameter.");
                this.customHandler = customHandler;
            }

            public override bool Process(byte value)
            {
                return this.customHandler(value);
            }
        }

        /// <summary>
        /// Aborts on a <c>NUL (0x00)</c>.
        /// </summary>
        public static ByteProcessor FIND_NUL = new IndexOfProcessor((byte)0);

        /// <summary>
        /// Aborts on a non-{@code NUL (0x00)}.
        /// </summary>
        public static ByteProcessor FIND_NON_NUL = new IndexNotOfProcessor((byte)0);

        /// <summary>
        /// Aborts on a {@code CR ('\r')}.
        /// </summary>
        public static ByteProcessor FIND_CR = new IndexOfProcessor((byte)'\r');

        /// <summary>
        /// Aborts on a non-{@code CR ('\r')}.
        /// </summary>
        public static ByteProcessor FIND_NON_CR = new IndexNotOfProcessor((byte)'\r');

        /// <summary>
        /// Aborts on a {@code LF ('\n')}.
        /// </summary>
        public static ByteProcessor FIND_LF = new IndexOfProcessor((byte)'\n');

        /// <summary>
        /// Aborts on a non-{@code LF ('\n')}.
        /// </summary>
        public static ByteProcessor FIND_NON_LF = new IndexNotOfProcessor((byte)'\n');

        /// <summary>
        /// Aborts on a {@code CR (';')}.
        /// </summary>
        public static ByteProcessor FIND_SEMI_COLON = new IndexOfProcessor((byte)';');

        /// <summary>
        /// Aborts on a {@code CR ('\r')} or a {@code LF ('\n')}.
        /// </summary>
        public static ByteProcessor FIND_CRLF = new CustomProcessor(new Func<byte, bool>(value => value != '\r' && value != '\n'));

        /// <summary>
        /// Aborts on a byte which is neither a {@code CR ('\r')} nor a {@code LF ('\n')}.
        /// </summary>
        public static ByteProcessor FIND_NON_CRLF = new CustomProcessor(new Func<byte, bool>(value => value == '\r' || value == '\n'));

        /// <summary>
        /// Aborts on a linear whitespace (a ({@code ' '} or a {@code '\t'}).
        /// </summary>
        public static ByteProcessor FIND_LINEAR_WHITESPACE = new CustomProcessor(new Func<byte, bool>(value => value != ' ' && value != '\t'));

        /// <summary>
        /// Aborts on a byte which is not a linear whitespace (neither {@code ' '} nor {@code '\t'}).
        /// </summary>
        public static ByteProcessor FIND_NON_LINEAR_WHITESPACE = new CustomProcessor(new Func<byte, bool>(value => value == ' ' || value == '\t'));

        /*
        * @return {@code true} if the processor wants to continue the loop and handle the next byte in the buffer.
        *         {@code false} if the processor wants to stop handling bytes and abort the loop.
        */
        public abstract bool Process(byte value);
    }
}

