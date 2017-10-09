// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /// <summary>
    ///     A decoder that splits the received <see cref="DotNetty.Buffers.IByteBuffer" /> by one or more
    ///     delimiters.It is particularly useful for decoding the frames which ends
    ///     with a delimiter such as <see cref="DotNetty.Codecs.Delimiters.NullDelimiter" /> or
    ///     <see cref="DotNetty.Codecs.Delimiters.LineDelimiter" />
    ///     <h3>Specifying more than one delimiter </h3>
    ///     <see cref="DotNetty.Codecs.Delimiters.NullDelimiter" /> allows you to specify more than one
    ///     delimiter.  If more than one delimiter is found in the buffer, it chooses
    ///     the delimiter which produces the shortest frame.  For example, if you have
    ///     the following data in the buffer:
    ///     +--------------+
    ///     | ABC\nDEF\r\n |
    ///     +--------------+
    ///     a <see cref="DotNetty.Codecs.Delimiters.LineDelimiter" /> will choose '\n' as the first delimiter and produce two
    ///     frames:
    ///     +-----+-----+
    ///     | ABC | DEF |
    ///     +-----+-----+
    ///     rather than incorrectly choosing '\r\n' as the first delimiter:
    ///     +----------+
    ///     | ABC\nDEF |
    ///     +----------+
    /// </summary>
    public class DelimiterBasedFrameDecoder : ByteToMessageDecoder
    {
        readonly IByteBuffer[] delimiters;
        readonly int maxFrameLength;
        readonly bool stripDelimiter;
        readonly bool failFast;
        bool discardingTooLongFrame;
        int tooLongFrameLength;
        readonly LineBasedFrameDecoder lineBasedDecoder; // Set only when decoding with "\n" and "\r\n" as the delimiter.

        /// <summary>Common constructor</summary>
        /// <param name="maxFrameLength">
        ///     The maximum length of the decoded frame
        ///     NOTE: A see <see cref="DotNetty.Codecs.TooLongFrameException" /> is thrown if the length of the frame exceeds this
        ///     value.
        /// </param>
        /// <param name="stripDelimiter">whether the decoded frame should strip out the delimiter or not</param>
        /// <param name="failFast">
        ///     If true, a <see cref="DotNetty.Codecs.TooLongFrameException" /> is
        ///     thrown as soon as the decoder notices the length of the
        ///     frame will exceed<tt>maxFrameLength</tt> regardless of
        ///     whether the entire frame has been read.
        ///     If false, a <see cref="DotNetty.Codecs.TooLongFrameException" /> is
        ///     thrown after the entire frame that exceeds maxFrameLength has been read.
        /// </param>
        /// <param name="delimiters">delimiters</param>
        public DelimiterBasedFrameDecoder(int maxFrameLength, bool stripDelimiter, bool failFast, params IByteBuffer[] delimiters)
        {
            ValidateMaxFrameLength(maxFrameLength);
            if (delimiters == null)
                throw new NullReferenceException("delimiters");

            if (delimiters.Length == 0)
                throw new ArgumentException("empty delimiters");

            if (IsLineBased(delimiters) && !this.IsSubclass())
            {
                this.lineBasedDecoder = new LineBasedFrameDecoder(maxFrameLength, stripDelimiter, failFast);
                this.delimiters = null;
            }
            else
            {
                this.delimiters = new IByteBuffer[delimiters.Length];
                for (int i = 0; i < delimiters.Length; i++)
                {
                    IByteBuffer d = delimiters[i];
                    ValidateDelimiter(d);
                    this.delimiters[i] = d.Slice(d.ReaderIndex, d.ReadableBytes);
                }
                this.lineBasedDecoder = null;
            }
            this.maxFrameLength = maxFrameLength;
            this.stripDelimiter = stripDelimiter;
            this.failFast = failFast;
        }

        public DelimiterBasedFrameDecoder(int maxFrameLength, IByteBuffer delimiter)
            : this(maxFrameLength, true, true, new[] { delimiter })
        {
        }

        public DelimiterBasedFrameDecoder(int maxFrameLength, bool stripDelimiter, IByteBuffer delimiter)
            : this(maxFrameLength, stripDelimiter, true, new[] { delimiter })
        {
        }

        public DelimiterBasedFrameDecoder(int maxFrameLength, bool stripDelimiter, bool failFast, IByteBuffer delimiter)
            : this(maxFrameLength, stripDelimiter, failFast, new[] { delimiter })
        {
        }

        public DelimiterBasedFrameDecoder(int maxFrameLength, params IByteBuffer[] delimiters)
            : this(maxFrameLength, true, true, delimiters)
        {
        }

        public DelimiterBasedFrameDecoder(int maxFrameLength, bool stripDelimiter, params IByteBuffer[] delimiters)
            : this(maxFrameLength, stripDelimiter, true, delimiters)
        {
        }

        /// <summary>Returns true if the delimiters are "\n" and "\r\n"</summary>
        static bool IsLineBased(IByteBuffer[] delimiters)
        {
            if (delimiters.Length != 2)
            {
                return false;
            }

            IByteBuffer a = delimiters[0];
            IByteBuffer b = delimiters[1];
            if (a.Capacity < b.Capacity)
            {
                a = delimiters[1];
                b = delimiters[0];
            }
            return a.Capacity == 2 && b.Capacity == 1 && a.GetByte(0) == '\r' && a.GetByte(1) == '\n' && b.GetByte(0) == '\n';
        }

        /// <summary>ReturnsReturn true if the current instance is a subclass of DelimiterBasedFrameDecoder</summary>
        bool IsSubclass() => this.GetType() != typeof(DelimiterBasedFrameDecoder);

        protected internal override void Decode(IChannelHandlerContext ctx, IByteBuffer input, List<object> output)
        {
            object decoded = this.Decode(ctx, input);
            if (decoded != null)
                output.Add(decoded);
        }

        /// <summary>Create a frame out of the <see cref="DotNetty.Buffers.IByteBuffer" /> and return it</summary>
        /// <param name="ctx">
        ///     the <see cref="DotNetty.Transport.Channels.IChannelHandlerContext" /> which this
        ///     <see cref="DotNetty.Codecs.ByteToMessageDecoder" /> belongs to
        /// </param>
        /// <param name="buffer">the <see cref="DotNetty.Buffers.IByteBuffer" /> from which to read data</param>
        /// <returns>
        ///     the <see cref="DotNetty.Buffers.IByteBuffer" /> which represent the frame or null if no frame could be
        ///     created.
        /// </returns>
        protected virtual object Decode(IChannelHandlerContext ctx, IByteBuffer buffer)
        {
            if (this.lineBasedDecoder != null)
            {
                return this.lineBasedDecoder.Decode(ctx, buffer);
            }

            // Try all delimiters and choose the delimiter which yields the shortest frame.
            int minFrameLength = int.MaxValue;
            IByteBuffer minDelim = null;
            foreach (IByteBuffer delim in this.delimiters)
            {
                int frameLength = IndexOf(buffer, delim);
                if (frameLength >= 0 && frameLength < minFrameLength)
                {
                    minFrameLength = frameLength;
                    minDelim = delim;
                }
            }

            if (minDelim != null)
            {
                int minDelimLength = minDelim.Capacity;
                IByteBuffer frame;

                if (this.discardingTooLongFrame)
                {
                    // We've just finished discarding a very large frame.
                    // Go back to the initial state.
                    this.discardingTooLongFrame = false;
                    buffer.SkipBytes(minFrameLength + minDelimLength);

                    int tooLongFrameLength = this.tooLongFrameLength;
                    this.tooLongFrameLength = 0;
                    if (!this.failFast)
                    {
                        this.Fail(tooLongFrameLength);
                    }
                    return null;
                }

                if (minFrameLength > this.maxFrameLength)
                {
                    // Discard read frame.
                    buffer.SkipBytes(minFrameLength + minDelimLength);
                    this.Fail(minFrameLength);
                    return null;
                }

                if (this.stripDelimiter)
                {
                    frame = buffer.ReadSlice(minFrameLength);
                    buffer.SkipBytes(minDelimLength);
                }
                else
                {
                    frame = buffer.ReadSlice(minFrameLength + minDelimLength);
                }

                return frame.Retain();
            }
            else
            {
                if (!this.discardingTooLongFrame)
                {
                    if (buffer.ReadableBytes > this.maxFrameLength)
                    {
                        // Discard the content of the buffer until a delimiter is found.
                        this.tooLongFrameLength = buffer.ReadableBytes;
                        buffer.SkipBytes(buffer.ReadableBytes);
                        this.discardingTooLongFrame = true;
                        if (this.failFast)
                        {
                            this.Fail(this.tooLongFrameLength);
                        }
                    }
                }
                else
                {
                    // Still discarding the buffer since a delimiter is not found.
                    this.tooLongFrameLength += buffer.ReadableBytes;
                    buffer.SkipBytes(buffer.ReadableBytes);
                }
                return null;
            }
        }

        void Fail(long frameLength)
        {
            if (frameLength > 0)
                throw new TooLongFrameException("frame length exceeds " + this.maxFrameLength + ": " + frameLength + " - discarded");
            else
                throw new TooLongFrameException("frame length exceeds " + this.maxFrameLength + " - discarding");
        }

        /**
         * Returns the number of bytes between the readerIndex of the haystack and
         * the first needle found in the haystack.  -1 is returned if no needle is
         * found in the haystack.
         */

        static int IndexOf(IByteBuffer haystack, IByteBuffer needle)
        {
            for (int i = haystack.ReaderIndex; i < haystack.WriterIndex; i++)
            {
                int haystackIndex = i;
                int needleIndex;
                for (needleIndex = 0; needleIndex < needle.Capacity; needleIndex++)
                {
                    if (haystack.GetByte(haystackIndex) != needle.GetByte(needleIndex))
                    {
                        break;
                    }
                    else
                    {
                        haystackIndex++;
                        if (haystackIndex == haystack.WriterIndex && needleIndex != needle.Capacity - 1)
                        {
                            return -1;
                        }
                    }
                }

                if (needleIndex == needle.Capacity)
                {
                    // Found the needle from the haystack!
                    return i - haystack.ReaderIndex;
                }
            }
            return -1;
        }

        static void ValidateDelimiter(IByteBuffer delimiter)
        {
            if (delimiter == null)
                throw new NullReferenceException("delimiter");

            if (!delimiter.IsReadable())
                throw new ArgumentException("empty delimiter");
        }

        static void ValidateMaxFrameLength(int maxFrameLength)
        {
            if (maxFrameLength <= 0)
                throw new ArgumentException("maxFrameLength must be a positive integer: " + maxFrameLength);
        }
    }
}