// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /// <summary>
    ///     An encoder that prepends the length of the message.  The length value is
    ///     prepended as a binary form.
    ///     <p />
    ///     For example, <tt>{@link LengthFieldPrepender}(2)</tt> will encode the
    ///     following 12-bytes string:
    ///     <pre>
    ///         +----------------+
    ///         | "HELLO, WORLD" |
    ///         +----------------+
    ///     </pre>
    ///     into the following:
    ///     <pre>
    ///         +--------+----------------+
    ///         + 0x000C | "HELLO, WORLD" |
    ///         +--------+----------------+
    ///     </pre>
    ///     If you turned on the {@code lengthIncludesLengthFieldLength} flag in the
    ///     constructor, the encoded data would look like the following
    ///     (12 (original data) + 2 (prepended data) = 14 (0xE)):
    ///     <pre>
    ///         +--------+----------------+
    ///         + 0x000E | "HELLO, WORLD" |
    ///         +--------+----------------+
    ///     </pre>
    /// </summary>
    public class LengthFieldPrepender : MessageToMessageEncoder<IByteBuffer>
    {
        readonly ByteOrder byteOrder;
        readonly int lengthFieldLength;
        readonly bool lengthFieldIncludesLengthFieldLength;
        readonly int lengthAdjustment;

        /// <summary>
        ///     Creates a new <see cref="LengthFieldPrepender" /> instance.
        /// </summary>
        /// <param name="lengthFieldLength">
        ///     The length of the prepended length field.
        ///     Only 1, 2, 3, 4, and 8 are allowed.
        /// </param>
        public LengthFieldPrepender(int lengthFieldLength)
            : this(lengthFieldLength, false)
        {
        }

        /// <summary>
        ///     Creates a new <see cref="LengthFieldPrepender" /> instance.
        /// </summary>
        /// <param name="lengthFieldLength">
        ///     The length of the prepended length field.
        ///     Only 1, 2, 3, 4, and 8 are allowed.
        /// </param>
        /// <param name="lengthFieldIncludesLengthFieldLength">
        ///     If <c>true</c>, the length of the prepended length field is added
        ///     to the value of the prepended length field.
        /// </param>
        public LengthFieldPrepender(int lengthFieldLength, bool lengthFieldIncludesLengthFieldLength)
            : this(lengthFieldLength, 0, lengthFieldIncludesLengthFieldLength)
        {
        }

        /// <summary>
        ///     Creates a new <see cref="LengthFieldPrepender" /> instance.
        /// </summary>
        /// <param name="lengthFieldLength">
        ///     The length of the prepended length field.
        ///     Only 1, 2, 3, 4, and 8 are allowed.
        /// </param>
        /// <param name="lengthAdjustment">The compensation value to add to the value of the length field.</param>
        public LengthFieldPrepender(int lengthFieldLength, int lengthAdjustment)
            : this(lengthFieldLength, lengthAdjustment, false)
        {
        }

        /// <summary>
        ///     Creates a new <see cref="LengthFieldPrepender" /> instance.
        /// </summary>
        /// <param name="lengthFieldLength">
        ///     The length of the prepended length field.
        ///     Only 1, 2, 3, 4, and 8 are allowed.
        /// </param>
        /// <param name="lengthFieldIncludesLengthFieldLength">
        ///     If <c>true</c>, the length of the prepended length field is added
        ///     to the value of the prepended length field.
        /// </param>
        /// <param name="lengthAdjustment">The compensation value to add to the value of the length field.</param>
        public LengthFieldPrepender(int lengthFieldLength, int lengthAdjustment, bool lengthFieldIncludesLengthFieldLength)
            : this(ByteOrder.BigEndian, lengthFieldLength, lengthAdjustment, lengthFieldIncludesLengthFieldLength)
        {
        }

        /// <summary>
        ///     Creates a new <see cref="LengthFieldPrepender" /> instance.
        /// </summary>
        /// <param name="byteOrder">The <see cref="ByteOrder" /> of the length field.</param>
        /// <param name="lengthFieldLength">
        ///     The length of the prepended length field.
        ///     Only 1, 2, 3, 4, and 8 are allowed.
        /// </param>
        /// <param name="lengthFieldIncludesLengthFieldLength">
        ///     If <c>true</c>, the length of the prepended length field is added
        ///     to the value of the prepended length field.
        /// </param>
        /// <param name="lengthAdjustment">The compensation value to add to the value of the length field.</param>
        public LengthFieldPrepender(ByteOrder byteOrder, int lengthFieldLength, int lengthAdjustment, bool lengthFieldIncludesLengthFieldLength)
        {
            if (lengthFieldLength != 1 && lengthFieldLength != 2 && lengthFieldLength != 3 &&
                lengthFieldLength != 4 && lengthFieldLength != 8)
            {
                throw new ArgumentException(
                    "lengthFieldLength must be either 1, 2, 3, 4, or 8: " +
                        lengthFieldLength, nameof(lengthFieldLength));
            }

            this.byteOrder = byteOrder;
            this.lengthFieldLength = lengthFieldLength;
            this.lengthFieldIncludesLengthFieldLength = lengthFieldIncludesLengthFieldLength;
            this.lengthAdjustment = lengthAdjustment;
        }

        protected internal override void Encode(IChannelHandlerContext context, IByteBuffer message, List<object> output)
        {
            int length = message.ReadableBytes + this.lengthAdjustment;
            if (this.lengthFieldIncludesLengthFieldLength)
            {
                length += this.lengthFieldLength;
            }

            if (length < 0)
            {
                throw new ArgumentException("Adjusted frame length (" + length + ") is less than zero");
            }

            switch (this.lengthFieldLength)
            {
                case 1:
                    if (length >= 256)
                    {
                        throw new ArgumentException("length of object does not fit into one byte: " + length);
                    }
                    output.Add(context.Allocator.Buffer(1).WriteByte((byte)length));
                    break;
                case 2:
                    if (length >= 65536)
                    {
                        throw new ArgumentException("length of object does not fit into a short integer: " + length);
                    }
                    output.Add(this.byteOrder == ByteOrder.BigEndian 
                        ? context.Allocator.Buffer(2).WriteShort((short)length) 
                        : context.Allocator.Buffer(2).WriteShortLE((short)length));
                    break;
                case 3:
                    if (length >= 16777216)
                    {
                        throw new ArgumentException("length of object does not fit into a medium integer: " + length);
                    }
                    output.Add(this.byteOrder == ByteOrder.BigEndian
                        ? context.Allocator.Buffer(3).WriteMedium(length)
                        : context.Allocator.Buffer(3).WriteMediumLE(length));
                    break;
                case 4:
                    output.Add(this.byteOrder == ByteOrder.BigEndian
                        ? context.Allocator.Buffer(4).WriteInt(length)
                        : context.Allocator.Buffer(4).WriteIntLE(length));
                    break;
                case 8:
                    output.Add(this.byteOrder == ByteOrder.BigEndian
                        ? context.Allocator.Buffer(8).WriteLong(length)
                        : context.Allocator.Buffer(8).WriteLongLE(length));
                    break;
                default:
                    throw new Exception("Unknown length field length");
            }

            output.Add(message.Retain());
        }
    }
}
