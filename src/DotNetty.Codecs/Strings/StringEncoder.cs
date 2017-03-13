// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /**
    * Encodes the requested {@link String} into a {@link ByteBuf}.
    * A typical setup for a text-based line protocol in a TCP/IP socket would be:
    * <pre>
    * {@link ChannelPipeline} pipeline = ...;
    *
    * // Decoders
    * pipeline.addLast("frameDecoder", new {@link LineBasedFrameDecoder}(80));
    * pipeline.addLast("stringDecoder", new {@link StringDecoder}(CharsetUtil.UTF_8));
    *
    * // Encoder
    * pipeline.addLast("stringEncoder", new {@link StringEncoder}(CharsetUtil.UTF_8));
    * </pre>
    * and then you can use a {@link String} instead of a {@link ByteBuf}
    * as a message:
    * <pre>
    * void channelRead({@link ChannelHandlerContext} ctx, {@link String} msg) {
    *     ch.write("Did you say '" + msg + "'?\n");
    * }
    * </pre>
    */

    public class StringEncoder : MessageToMessageEncoder<string>
    {
        readonly Encoding encoding;

        /// <summary>
        ///     Initializes a new instance of the <see cref="StringEncoder" /> class with the current system
        ///     character set.
        /// </summary>
        public StringEncoder()
            : this(Encoding.GetEncoding(0))
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="StringEncoder" /> class with the specified character
        ///     set..
        /// </summary>
        /// <param name="encoding">Encoding.</param>
        public StringEncoder(Encoding encoding)
        {
            if (encoding == null)
            {
                throw new NullReferenceException("encoding");
            }

            this.encoding = encoding;
        }

        public override bool IsSharable => true;

        protected internal override void Encode(IChannelHandlerContext context, string message, List<object> output)
        {
            if (message.Length == 0)
            {
                return;
            }

            output.Add(ByteBufferUtil.EncodeString(context.Allocator, message, this.encoding));
        }
    }
}