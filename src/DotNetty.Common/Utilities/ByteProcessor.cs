// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Diagnostics.Contracts;

    using static ByteProcessorUtils;

    /// <summary>
    ///     Provides a mechanism to iterate over a collection of bytes.
    /// </summary>
    public interface IByteProcessor
    {
        bool Process(byte value);
    }

    public sealed class IndexOfProcessor : IByteProcessor
    {
        readonly byte byteToFind;

        public IndexOfProcessor(byte byteToFind)
        {
            this.byteToFind = byteToFind;
        }

        public bool Process(byte value) => value != this.byteToFind;
    }

    public sealed class IndexNotOfProcessor : IByteProcessor
    {
        readonly byte byteToNotFind;

        public IndexNotOfProcessor(byte byteToNotFind)
        {
            this.byteToNotFind = byteToNotFind;
        }

        public bool Process(byte value) => value == this.byteToNotFind;
    }

    public sealed class ByteProcessor : IByteProcessor
    {
        readonly Func<byte, bool> customHandler;

        public ByteProcessor(Func<byte, bool> customHandler)
        {
            Contract.Assert(customHandler != null, "'customHandler' is required parameter.");
            this.customHandler = customHandler;
        }

        public bool Process(byte value) => this.customHandler(value);


        /// <summary>
        ///     Aborts on a <c>NUL (0x00)</c>.
        /// </summary>
        public static IByteProcessor FindNul = new IndexOfProcessor(0);

        /// <summary>
        ///     Aborts on a non-{@code NUL (0x00)}.
        /// </summary>
        public static IByteProcessor FindNonNul = new IndexNotOfProcessor(0);

        /// <summary>
        ///     Aborts on a {@code CR ('\r')}.
        /// </summary>
        public static IByteProcessor FindCR = new IndexOfProcessor(CarriageReturn);

        /// <summary>
        ///     Aborts on a non-{@code CR ('\r')}.
        /// </summary>
        public static IByteProcessor FindNonCR = new IndexNotOfProcessor(CarriageReturn);

        /// <summary>
        ///     Aborts on a {@code LF ('\n')}.
        /// </summary>
        public static IByteProcessor FindLF = new IndexOfProcessor(LineFeed);

        /// <summary>
        ///     Aborts on a non-{@code LF ('\n')}.
        /// </summary>
        public static IByteProcessor FindNonLF = new IndexNotOfProcessor(LineFeed);

        /// <summary>
        ///     Aborts on a {@code CR (';')}.
        /// </summary>
        public static IByteProcessor FindSemicolon = new IndexOfProcessor((byte)';');

        /// <summary>
        ///     Aborts on a comma {@code (',')}.
        /// </summary>
        public static IByteProcessor FindComma = new IndexOfProcessor((byte)',');

        /// <summary>
        ///     Aborts on a ascii space character ({@code ' '}).
        /// </summary>
        public static IByteProcessor FindAsciiSpace = new IndexOfProcessor(Space);

        /// <summary>
        ///     Aborts on a {@code CR ('\r')} or a {@code LF ('\n')}.
        /// </summary>
        public static IByteProcessor FindCrlf = new ByteProcessor(new Func<byte, bool>(value => value != CarriageReturn && value != LineFeed));

        /// <summary>
        ///     Aborts on a byte which is neither a {@code CR ('\r')} nor a {@code LF ('\n')}.
        /// </summary>
        public static IByteProcessor FindNonCrlf = new ByteProcessor(new Func<byte, bool>(value => value == CarriageReturn || value == LineFeed));

        /// <summary>
        ///     Aborts on a linear whitespace (a ({@code ' '} or a {@code '\t'}).
        /// </summary>
        public static IByteProcessor FindLinearWhitespace = new ByteProcessor(new Func<byte, bool>(value => value != Space && value != HTab));

        /// <summary>
        ///     Aborts on a byte which is not a linear whitespace (neither {@code ' '} nor {@code '\t'}).
        /// </summary>
        public static IByteProcessor FindNonLinearWhitespace = new ByteProcessor(new Func<byte, bool>(value => value == Space || value == HTab));
    }
}