// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /*
    * Decodes a received {@link ByteBuf} into a {@link String}.  Please
    * note that this decoder must be used with a proper {@link ByteToMessageDecoder}
    * such as {@link DelimiterBasedFrameDecoder} or {@link LineBasedFrameDecoder}
    * if you are using a stream-based transport such as TCP/IP.  A typical setup for a
    * text-based line protocol in a TCP/IP socket would be:
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

    public class StringDecoder : MessageToMessageDecoder<IByteBuffer>
    {
        readonly Encoding encoding;

        /// <summary>
        ///     Initializes a new instance of the <see cref="DotNetty.Codecs.StringDecoder" /> class with the current system
        ///     character set.
        /// </summary>
        public StringDecoder()
            : this(Encoding.GetEncoding(0))
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="DotNetty.Codecs.StringDecoder" /> class with the specified character
        ///     set..
        /// </summary>
        /// <param name="encoding">Encoding.</param>
        public StringDecoder(Encoding encoding)
        {
            if (encoding == null)
            {
                throw new NullReferenceException("encoding");
            }

            this.encoding = encoding;
        }

        public override bool IsSharable => true;

        protected internal override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            string decoded = this.Decode(context, input);
            output.Add(decoded);
        }

        protected string Decode(IChannelHandlerContext context, IByteBuffer input) => input.ToString(this.encoding);
    }
}