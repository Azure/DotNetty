// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    ///     A decoder that splits the received {@link ByteBuf}s on line endings.
    ///     Both {@code "\n"} and {@code "\r\n"} are handled.
    ///     For a more general delimiter-based decoder, see {@link DelimiterBasedFrameDecoder}.
    /// </summary>
    public class LineBasedFrameDecoder : ByteToMessageDecoder
    {
        /** Maximum length of a frame we're willing to decode.  */
        readonly int maxLength;
        /** Whether or not to throw an exception as soon as we exceed maxLength. */
        readonly bool failFast;
        readonly bool stripDelimiter;

        /** True if we're discarding input because we're already over maxLength.  */
        bool discarding;
        int discardedBytes;

        /// <summary>
        ///     Initializes a new instance of the <see cref="DotNetty.Codecs.LineBasedFrameDecoder" /> class.
        /// </summary>
        /// <param name="maxLength">
        ///     the maximum length of the decoded frame.
        ///     A {@link TooLongFrameException} is thrown if
        ///     the length of the frame exceeds this value.
        /// </param>
        public LineBasedFrameDecoder(int maxLength)
            : this(maxLength, true, false)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="DotNetty.Codecs.LineBasedFrameDecoder" /> class.
        /// </summary>
        /// <param name="maxLength">
        ///     the maximum length of the decoded frame.
        ///     A {@link TooLongFrameException} is thrown if
        ///     the length of the frame exceeds this value.
        /// </param>
        /// <param name="stripDelimiter">
        ///     whether the decoded frame should strip out the
        ///     delimiter or not
        /// </param>
        /// <param name="failFast">
        ///     If <tt>true</tt>, a {@link TooLongFrameException} is
        ///     thrown as soon as the decoder notices the length of the
        ///     frame will exceed <tt>maxFrameLength</tt> regardless of
        ///     whether the entire frame has been read.
        ///     If <tt>false</tt>, a {@link TooLongFrameException} is
        ///     thrown after the entire frame that exceeds
        ///     <tt>maxFrameLength</tt> has been read.
        /// </param>
        public LineBasedFrameDecoder(int maxLength, bool stripDelimiter, bool failFast)
        {
            this.maxLength = maxLength;
            this.failFast = failFast;
            this.stripDelimiter = stripDelimiter;
        }

        protected internal override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            object decode = this.Decode(context, input);
            if (decode != null)
            {
                output.Add(decode);
            }
        }

        /// <summary>
        ///     Create a frame out of the {@link ByteBuf} and return it.
        /// </summary>
        /// <param name="ctx">the {@link ChannelHandlerContext} which this {@link ByteToMessageDecoder} belongs to</param>
        /// <param name="buffer">the {@link ByteBuf} from which to read data</param>
        protected virtual internal object Decode(IChannelHandlerContext ctx, IByteBuffer buffer)
        {
            int eol = this.FindEndOfLine(buffer);
            if (!this.discarding)
            {
                if (eol >= 0)
                {
                    IByteBuffer frame;
                    int length = eol - buffer.ReaderIndex;
                    int delimLength = buffer.GetByte(eol) == '\r' ? 2 : 1;

                    if (length > this.maxLength)
                    {
                        buffer.SetReaderIndex(eol + delimLength);
                        this.Fail(ctx, length);
                        return null;
                    }

                    if (this.stripDelimiter)
                    {
                        frame = buffer.ReadSlice(length);
                        buffer.SkipBytes(delimLength);
                    }
                    else
                    {
                        frame = buffer.ReadSlice(length + delimLength);
                    }

                    return frame.Retain();
                }
                else
                {
                    int length = buffer.ReadableBytes;
                    if (length > this.maxLength)
                    {
                        this.discardedBytes = length;
                        buffer.SetReaderIndex(buffer.WriterIndex);
                        this.discarding = true;
                        if (this.failFast)
                        {
                            this.Fail(ctx, "over " + this.discardedBytes);
                        }
                    }
                    return null;
                }
            }
            else
            {
                if (eol >= 0)
                {
                    int length = this.discardedBytes + eol - buffer.ReaderIndex;
                    int delimLength = buffer.GetByte(eol) == '\r' ? 2 : 1;
                    buffer.SetReaderIndex(eol + delimLength);
                    this.discardedBytes = 0;
                    this.discarding = false;
                    if (!this.failFast)
                    {
                        this.Fail(ctx, length);
                    }
                }
                else
                {
                    this.discardedBytes += buffer.ReadableBytes;
                    buffer.SetReaderIndex(buffer.WriterIndex);
                }
                return null;
            }
        }

        void Fail(IChannelHandlerContext ctx, int length) => this.Fail(ctx, length.ToString());

        void Fail(IChannelHandlerContext ctx, string length)
        {
            ctx.FireExceptionCaught(
                new TooLongFrameException(
                    $"frame length ({length}) exceeds the allowed maximum ({this.maxLength})"));
        }

        int FindEndOfLine(IByteBuffer buffer)
        {
            int i = buffer.ForEachByte(ByteProcessor.FindLF);
            if (i > 0 && buffer.GetByte(i - 1) == '\r')
            {
                i--;
            }

            return i;
        }
    }
}