// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Runtime.CompilerServices;
    using DotNetty.Buffers;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public abstract class HttpObjectDecoder : ByteToMessageDecoder
    {
        readonly int maxChunkSize;
        readonly bool chunkedSupported;
        protected readonly bool ValidateHeaders;
        readonly HeaderParser headerParser;
        readonly LineParser lineParser;

        IHttpMessage message;
        long chunkSize;
        long contentLength = long.MinValue;
        volatile bool resetRequested;

        // These will be updated by splitHeader(...)
        AsciiString name;
        AsciiString value;

        ILastHttpContent trailer;

        enum State
        {
            SkipControlChars,
            ReadInitial,
            ReadHeader,
            ReadVariableLengthContent,
            ReadFixedLengthContent,
            ReadChunkSize,
            ReadChunkedContent,
            ReadChunkDelimiter,
            ReadChunkFooter,
            BadMessage,
            Upgraded
        }

        State currentState = State.SkipControlChars;

        protected HttpObjectDecoder() : this(4096, 8192, 8192, true)
        {
        }

        protected HttpObjectDecoder(
            int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool chunkedSupported)
            : this(maxInitialLineLength, maxHeaderSize, maxChunkSize, chunkedSupported, true)
        {
        }

        protected HttpObjectDecoder(
            int maxInitialLineLength, int maxHeaderSize, int maxChunkSize,
            bool chunkedSupported, bool validateHeaders)
            : this(maxInitialLineLength, maxHeaderSize, maxChunkSize, chunkedSupported, validateHeaders, 128)
        {
        }

        protected HttpObjectDecoder(
            int maxInitialLineLength, int maxHeaderSize, int maxChunkSize,
            bool chunkedSupported, bool validateHeaders, int initialBufferSize)
        {
            Contract.Requires(maxInitialLineLength > 0);
            Contract.Requires(maxHeaderSize > 0);
            Contract.Requires(maxChunkSize > 0);

            var seq = new AppendableCharSequence(initialBufferSize);
            this.lineParser = new LineParser(seq, maxInitialLineLength);
            this.headerParser = new HeaderParser(seq, maxHeaderSize);
            this.maxChunkSize = maxChunkSize;
            this.chunkedSupported = chunkedSupported;
            this.ValidateHeaders = validateHeaders;
        }

        protected override void Decode(IChannelHandlerContext context, IByteBuffer buffer, List<object> output)
        {
            if (this.resetRequested)
            {
                this.ResetNow();
            }

            switch (this.currentState)
            {
                case State.SkipControlChars:
                {
                    if (!SkipControlCharacters(buffer))
                    {
                        return;
                    }
                    this.currentState = State.ReadInitial;
                    goto case State.ReadInitial; // Fall through
                }
                case State.ReadInitial:
                {
                    try
                    {
                        AppendableCharSequence line = this.lineParser.Parse(buffer);
                        if (line == null)
                        {
                            return;
                        }
                        AsciiString[] initialLine = SplitInitialLine(line);
                        if (initialLine.Length < 3)
                        {
                            // Invalid initial line - ignore.
                            this.currentState = State.SkipControlChars;
                            return;
                        }

                        this.message = this.CreateMessage(initialLine);
                        this.currentState = State.ReadHeader;
                        goto case State.ReadHeader; // Fall through
                    }
                    catch (Exception e)
                    {
                        output.Add(this.InvalidMessage(buffer, e));
                        return;
                    }
                }
                case State.ReadHeader:
                {
                    try
                    {
                        State? nextState = this.ReadHeaders(buffer);
                        if (nextState == null)
                        {
                            return;
                        }
                        this.currentState = nextState.Value;
                        switch (nextState.Value)
                        {
                            case State.SkipControlChars:
                            {
                                // fast-path
                                // No content is expected.
                                output.Add(this.message);
                                output.Add(EmptyLastHttpContent.Default);
                                this.ResetNow();
                                return;
                            }
                            case State.ReadChunkSize:
                            {
                                if (!this.chunkedSupported)
                                {
                                    throw new ArgumentException("Chunked messages not supported");
                                }
                                // Chunked encoding - generate HttpMessage first.  HttpChunks will follow.
                                output.Add(this.message);
                                return;
                            }
                            default:
                            {
                                // <a href="https://tools.ietf.org/html/rfc7230#section-3.3.3">RFC 7230, 3.3.3</a> states that if a
                                // request does not have either a transfer-encoding or a content-length header then the message body
                                // length is 0. However for a response the body length is the number of octets received prior to the
                                // server closing the connection. So we treat this as variable length chunked encoding.
                                long length = this.ContentLength();
                                if (length == 0 || length == -1 && this.IsDecodingRequest())
                                {
                                    output.Add(this.message);
                                    output.Add(EmptyLastHttpContent.Default);
                                    this.ResetNow();
                                    return;
                                }

                                Debug.Assert(nextState.Value == State.ReadFixedLengthContent
                                    || nextState.Value == State.ReadVariableLengthContent);

                                output.Add(this.message);

                                if (nextState == State.ReadFixedLengthContent)
                                {
                                    // chunkSize will be decreased as the READ_FIXED_LENGTH_CONTENT state reads data chunk by chunk.
                                    this.chunkSize = length;
                                }

                                // We return here, this forces decode to be called again where we will decode the content
                                return;
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        output.Add(this.InvalidMessage(buffer, exception));
                        return;
                    }
                }
                case State.ReadVariableLengthContent:
                {
                    // Keep reading data as a chunk until the end of connection is reached.
                    int toRead = Math.Min(buffer.ReadableBytes, this.maxChunkSize);
                    if (toRead > 0)
                    {
                        IByteBuffer content = buffer.ReadRetainedSlice(toRead);
                        output.Add(new DefaultHttpContent(content));
                    }
                    return;
                }
                case State.ReadFixedLengthContent:
                {
                    int readLimit = buffer.ReadableBytes;

                    // Check if the buffer is readable first as we use the readable byte count
                    // to create the HttpChunk. This is needed as otherwise we may end up with
                    // create a HttpChunk instance that contains an empty buffer and so is
                    // handled like it is the last HttpChunk.
                    //
                    // See https://github.com/netty/netty/issues/433
                    if (readLimit == 0)
                    {
                        return;
                    }

                    int toRead = Math.Min(readLimit, this.maxChunkSize);
                    if (toRead > this.chunkSize)
                    {
                        toRead = (int)this.chunkSize;
                    }
                    IByteBuffer content = buffer.ReadRetainedSlice(toRead);
                    this.chunkSize -= toRead;

                    if (this.chunkSize == 0)
                    {
                        // Read all content.
                        output.Add(new DefaultLastHttpContent(content, this.ValidateHeaders));
                        this.ResetNow();
                    }
                    else
                    {
                        output.Add(new DefaultHttpContent(content));
                    }
                    return;
                }
                //  everything else after this point takes care of reading chunked content. basically, read chunk size,
                //  read chunk, read and ignore the CRLF and repeat until 0
                case State.ReadChunkSize:
                {
                    try
                    {
                        AppendableCharSequence line = this.lineParser.Parse(buffer);
                        if (line == null)
                        {
                            return;
                        }
                        int size = GetChunkSize(line.ToAsciiString());
                        this.chunkSize = size;
                        if (size == 0)
                        {
                            this.currentState = State.ReadChunkFooter;
                            return;
                        }
                        this.currentState = State.ReadChunkedContent;
                        goto case State.ReadChunkedContent; // fall-through
                    }
                    catch (Exception e)
                    {
                        output.Add(this.InvalidChunk(buffer, e));
                        return;
                    }
                }
                case State.ReadChunkedContent:
                {
                    Debug.Assert(this.chunkSize <= int.MaxValue);

                    int toRead = Math.Min((int)this.chunkSize, this.maxChunkSize);
                    toRead = Math.Min(toRead, buffer.ReadableBytes);
                    if (toRead == 0)
                    {
                        return;
                    }
                    IHttpContent chunk = new DefaultHttpContent(buffer.ReadRetainedSlice(toRead));
                    this.chunkSize -= toRead;

                    output.Add(chunk);

                    if (this.chunkSize != 0)
                    {
                        return;
                    }
                    this.currentState = State.ReadChunkDelimiter;
                    goto case State.ReadChunkDelimiter; // fall-through
                }
                case State.ReadChunkDelimiter:
                {
                    int wIdx = buffer.WriterIndex;
                    int rIdx = buffer.ReaderIndex;
                    while (wIdx > rIdx)
                    {
                        byte next = buffer.GetByte(rIdx++);
                        if (next == HttpConstants.LineFeed)
                        {
                            this.currentState = State.ReadChunkSize;
                            break;
                        }
                    }
                    buffer.SetReaderIndex(rIdx);
                    return;
                }
                case State.ReadChunkFooter:
                {
                    try
                    {
                        ILastHttpContent lastTrialer = this.ReadTrailingHeaders(buffer);
                        if (lastTrialer == null)
                        {
                            return;
                        }
                        output.Add(lastTrialer);
                        this.ResetNow();
                        return;
                    }
                    catch (Exception exception)
                    {
                        output.Add(this.InvalidChunk(buffer, exception));
                        return;
                    }
                }
                case State.BadMessage:
                {
                    // Keep discarding until disconnection.
                    buffer.SkipBytes(buffer.ReadableBytes);
                    break;
                }
                case State.Upgraded:
                {
                    int readableBytes = buffer.ReadableBytes;
                    if (readableBytes > 0)
                    {
                        // Keep on consuming as otherwise we may trigger an DecoderException,
                        // other handler will replace this codec with the upgraded protocol codec to
                        // take the traffic over at some point then.
                        // See https://github.com/netty/netty/issues/2173
                        output.Add(buffer.ReadBytes(readableBytes));
                    }
                    break;
                }
            }
        }

        protected override void DecodeLast(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            base.DecodeLast(context, input, output);

            if (this.resetRequested)
            {
                // If a reset was requested by decodeLast() we need to do it now otherwise we may produce a
                // LastHttpContent while there was already one.
                this.ResetNow();
            }

            // Handle the last unfinished message.
            if (this.message != null)
            {
                bool chunked = HttpUtil.IsTransferEncodingChunked(this.message);
                if (this.currentState == State.ReadVariableLengthContent 
                    && !input.IsReadable() && !chunked)
                {
                    // End of connection.
                    output.Add(EmptyLastHttpContent.Default);
                    this.ResetNow();
                    return;
                }

                if (this.currentState == State.ReadHeader)
                {
                    // If we are still in the state of reading headers we need to create a new invalid message that
                    // signals that the connection was closed before we received the headers.
                    output.Add(this.InvalidMessage(Unpooled.Empty, 
                        new PrematureChannelClosureException("Connection closed before received headers")));
                    this.ResetNow();
                    return;
                }

                // Check if the closure of the connection signifies the end of the content.
                bool prematureClosure;
                if (this.IsDecodingRequest() || chunked)
                {
                    // The last request did not wait for a response.
                    prematureClosure = true;
                }
                else
                {
                    // Compare the length of the received content and the 'Content-Length' header.
                    // If the 'Content-Length' header is absent, the length of the content is determined by the end of the
                    // connection, so it is perfectly fine.
                    prematureClosure = this.ContentLength() > 0;
                }

                if (!prematureClosure)
                {
                    output.Add(EmptyLastHttpContent.Default);
                }
                this.ResetNow();
            }
        }

        public override void UserEventTriggered(IChannelHandlerContext context, object evt)
        {
            if (evt is HttpExpectationFailedEvent)
            {
                switch (this.currentState)
                {
                    case State.ReadFixedLengthContent:
                    case State.ReadVariableLengthContent:
                    case State.ReadChunkSize:
                        this.Reset();
                        break;
                }
            }
            base.UserEventTriggered(context, evt);
        }

        protected virtual bool IsContentAlwaysEmpty(IHttpMessage msg)
        {
            if (msg is IHttpResponse res)
            {
                int code = res.Status.Code;

                // Correctly handle return codes of 1xx.
                //
                // See:
                //     - http://www.w3.org/Protocols/rfc2616/rfc2616-sec4.html Section 4.4
                //     - https://github.com/netty/netty/issues/222
                if (code >= 100 && code < 200)
                {
                    // One exception: Hixie 76 websocket handshake response
                    return !(code == 101 && !res.Headers.Contains(HttpHeaderNames.SecWebsocketAccept)
                        && res.Headers.Contains(HttpHeaderNames.Upgrade, HttpHeaderValues.Websocket, true));
                }
                switch (code)
                {
                    case 204:
                    case 304:
                        return true;
                }
            }
            return false;
        }

        protected bool IsSwitchingToNonHttp1Protocol(IHttpResponse msg)
        {
            if (msg.Status.Code != HttpResponseStatus.SwitchingProtocols.Code)
            {
                return false;
            }

            return !msg.Headers.TryGet(HttpHeaderNames.Upgrade, out ICharSequence newProtocol)
                || !AsciiString.Contains(newProtocol, HttpVersion.Http10String) 
                   && !AsciiString.Contains(newProtocol, HttpVersion.Http11String);
        }

        // Resets the state of the decoder so that it is ready to decode a new message.
        // This method is useful for handling a rejected request with {@code Expect: 100-continue} header.
        public void Reset() => this.resetRequested = true;

        void ResetNow()
        {
            IHttpMessage msg = this.message;
            this.message = null;
            this.name = null;
            this.value = null;
            this.contentLength = long.MinValue;
            this.lineParser.Reset();
            this.headerParser.Reset();
            this.trailer = null;
            if (!this.IsDecodingRequest())
            {
                if (msg is IHttpResponse res && this.IsSwitchingToNonHttp1Protocol(res))
                {
                    this.currentState = State.Upgraded;
                    return;
                }
            }

            this.resetRequested = false;
            this.currentState = State.SkipControlChars;
        }

        IHttpMessage InvalidMessage(IByteBuffer buf, Exception cause)
        {
            this.currentState = State.BadMessage;

            // Advance the readerIndex so that ByteToMessageDecoder does not complain
            // when we produced an invalid message without consuming anything.
            buf.SkipBytes(buf.ReadableBytes);

            if (this.message != null)
            {
                this.message.Result = DecoderResult.Failure(cause);
            }
            else
            {
                this.message = this.CreateInvalidMessage();
                this.message.Result = DecoderResult.Failure(cause);
            }

            IHttpMessage ret = this.message;
            this.message = null;
            return ret;
        }

        IHttpContent InvalidChunk(IByteBuffer buf, Exception cause)
        {
            this.currentState = State.BadMessage;

            // Advance the readerIndex so that ByteToMessageDecoder does not complain
            // when we produced an invalid message without consuming anything.
            buf.SkipBytes(buf.ReadableBytes);

            IHttpContent chunk = new DefaultLastHttpContent(Unpooled.Empty);
            chunk.Result = DecoderResult.Failure(cause);
            this.message = null;
            this.trailer = null;
            return chunk;
        }

        static bool SkipControlCharacters(IByteBuffer buffer)
        {
            bool skiped = false;
            int wIdx = buffer.WriterIndex;
            int rIdx = buffer.ReaderIndex;
            while (wIdx > rIdx)
            {
                byte c = buffer.GetByte(rIdx++);
                if (!CharUtil.IsISOControl(c) && !IsWhiteSpace(c))
                {
                    rIdx--;
                    skiped = true;
                    break;
                }
            }
            buffer.SetReaderIndex(rIdx);
            return skiped;
        }

        State? ReadHeaders(IByteBuffer buffer)
        {
            IHttpMessage httpMessage = this.message;
            HttpHeaders headers = httpMessage.Headers;

            AppendableCharSequence line = this.headerParser.Parse(buffer);
            if (line == null)
            {
                return null;
            }
            // ReSharper disable once ConvertIfDoToWhile
            if (line.Count > 0)
            {
                do
                {
                    byte firstChar = line.Bytes[0];
                    if (this.name != null && (firstChar == ' ' || firstChar == '\t'))
                    {
                        ICharSequence trimmedLine = CharUtil.Trim(line);
                        this.value = new AsciiString($"{this.value} {trimmedLine}");
                    }
                    else
                    {
                        if (this.name != null)
                        {
                            headers.Add(this.name, this.value);
                        }
                        this.SplitHeader(line);
                    }

                    line = this.headerParser.Parse(buffer);
                    if (line == null)
                    {
                        return null;
                    }
                } while (line.Count > 0);
            }

            // Add the last header.
            if (this.name != null)
            {
                headers.Add(this.name, this.value);
            }
            // reset name and value fields
            this.name = null;
            this.value = null;

            State nextState;

            if (this.IsContentAlwaysEmpty(httpMessage))
            {
                HttpUtil.SetTransferEncodingChunked(httpMessage, false);
                nextState = State.SkipControlChars;
            }
            else if (HttpUtil.IsTransferEncodingChunked(httpMessage))
            {
                nextState = State.ReadChunkSize;
            }
            else if (this.ContentLength() >= 0)
            {
                nextState = State.ReadFixedLengthContent;
            }
            else
            {
                nextState = State.ReadVariableLengthContent;
            }
            return nextState;
        }

        long ContentLength()
        {
            if (this.contentLength == long.MinValue)
            {
                this.contentLength = HttpUtil.GetContentLength(this.message, -1L);
            }
            return this.contentLength;
        }

        ILastHttpContent ReadTrailingHeaders(IByteBuffer buffer)
        {
            AppendableCharSequence line = this.headerParser.Parse(buffer);
            if (line == null)
            {
                return null;
            }
            AsciiString lastHeader = null;
            if (line.Count > 0)
            {
                ILastHttpContent trailingHeaders = this.trailer;
                if (trailingHeaders == null)
                {
                    trailingHeaders = new DefaultLastHttpContent(Unpooled.Empty, this.ValidateHeaders);
                    this.trailer = trailingHeaders;
                }
                do
                {
                    byte firstChar = line.Bytes[0];
                    if (lastHeader != null && (firstChar == ' ' || firstChar == '\t'))
                    {
                        IList<ICharSequence> current = trailingHeaders.TrailingHeaders.GetAll(lastHeader);
                        if (current.Count > 0)
                        {
                            int lastPos = current.Count - 1;
                            ICharSequence lineTrimmed = CharUtil.Trim(line);
                            current[lastPos] = new AsciiString($"{current[lastPos]} {lineTrimmed}");
                        }
                    }
                    else
                    {
                        this.SplitHeader(line);
                        AsciiString headerName = this.name;
                        if (!HttpHeaderNames.ContentLength.ContentEqualsIgnoreCase(headerName) 
                            && !HttpHeaderNames.TransferEncoding.ContentEqualsIgnoreCase(headerName) 
                            && !HttpHeaderNames.Trailer.ContentEqualsIgnoreCase(headerName))
                        {
                            trailingHeaders.TrailingHeaders.Add(headerName, this.value);
                        }
                        lastHeader = this.name;
                        // reset name and value fields
                        this.name = null;
                        this.value = null;
                    }

                    line = this.headerParser.Parse(buffer);
                    if (line == null)
                    {
                        return null;
                    }
                } while (line.Count > 0);

                this.trailer = null;
                return trailingHeaders;
            }

            return EmptyLastHttpContent.Default;
        }

        protected abstract bool IsDecodingRequest();

        protected abstract IHttpMessage CreateMessage(AsciiString[] initialLine);

        protected abstract IHttpMessage CreateInvalidMessage();

        static int GetChunkSize(AsciiString hex)
        {
            hex = hex.Trim();
            for (int i = hex.Offset; i < hex.Count; i++)
            {
                byte c = hex.Array[i];
                if (c == ';' || IsWhiteSpace(c) || CharUtil.IsISOControl(c))
                {
                    hex = (AsciiString)hex.SubSequence(0, i);
                    break;
                }
            }

            return hex.ParseInt(16);
        }

        static AsciiString[] SplitInitialLine(AppendableCharSequence sb)
        {
            byte[] chars = sb.Bytes;
            int length = sb.Count;

            int aStart = FindNonWhitespace(chars, 0, length);
            int aEnd = FindWhitespace(chars, aStart, length);

            int bStart = FindNonWhitespace(chars, aEnd, length);
            int bEnd = FindWhitespace(chars, bStart, length);

            int cStart = FindNonWhitespace(chars, bEnd, length);
            int cEnd = FindEndOfString(chars, length);

            return new[]
            {
                sb.SubStringUnsafe(aStart, aEnd),
                sb.SubStringUnsafe(bStart, bEnd),
                cStart < cEnd ? sb.SubStringUnsafe(cStart, cEnd) : AsciiString.Empty
            };
        }

        void SplitHeader(AppendableCharSequence sb)
        {
            byte[] chars = sb.Bytes;
            int length = sb.Count;
            int nameEnd;
            int colonEnd;

            int nameStart = FindNonWhitespace(chars, 0, length);
            for (nameEnd = nameStart; nameEnd < length; nameEnd++)
            {
                byte ch = chars[nameEnd];
                if (ch == ':' || IsWhiteSpace(ch))
                {
                    break;
                }
            }

            for (colonEnd = nameEnd; colonEnd < length; colonEnd++)
            {
                if (chars[colonEnd] == ':')
                {
                    colonEnd++;
                    break;
                }
            }

            this.name = sb.SubStringUnsafe(nameStart, nameEnd);
            int valueStart = FindNonWhitespace(chars, colonEnd, length);
            if (valueStart == length)
            {
                this.value = AsciiString.Empty;
            }
            else
            {
                int valueEnd = FindEndOfString(chars, length);
                this.value = sb.SubStringUnsafe(valueStart, valueEnd);
            }
        }

        static int FindNonWhitespace(byte[] sb, int offset, int length)
        {
            for (int result = offset; result < length; ++result)
            {
                if (!IsWhiteSpace(sb[result]))
                {
                    return result;
                }
            }
            return length;
        }

        static int FindWhitespace(byte[] sb, int offset, int length)
        {
            for (int result = offset; result < length; ++result)
            {
                if (IsWhiteSpace(sb[result]))
                {
                    return result;
                }
            }
            return length;
        }

        static int FindEndOfString(byte[] sb, int length)
        {
            for (int result = length - 1; result > 0; --result)
            {
                if (!IsWhiteSpace(sb[result]))
                {
                    return result + 1;
                }
            }
            return 0;
        }

        class HeaderParser : IByteProcessor
        {
            readonly AppendableCharSequence seq;
            readonly int maxLength;
            int size;

            internal HeaderParser(AppendableCharSequence seq, int maxLength)
            {
                this.seq = seq;
                this.maxLength = maxLength;
            }

            public virtual AppendableCharSequence Parse(IByteBuffer buffer)
            {
                int oldSize = this.size;
                this.seq.Reset();
                int i = buffer.ForEachByte(this);
                if (i == -1)
                {
                    this.size = oldSize;
                    return null;
                }
                buffer.SetReaderIndex(i + 1);
                return this.seq;
            }

            public void Reset() => this.size = 0;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Process(byte value)
            {
                if (value == HttpConstants.CarriageReturn)
                {
                    return true;
                }
                if (value == HttpConstants.LineFeed)
                {
                    return false;
                }

                if (++this.size > this.maxLength)
                {
                    // TODO: Respond with Bad Request and discard the traffic
                    //    or close the connection.
                    //       No need to notify the upstream handlers - just log.
                    //       If decoding a response, just throw an exception.
                    ThrowTooLongFrameException(this, this.maxLength);
                }

                this.seq.Append(value);
                return true;
            }

            static void ThrowTooLongFrameException(HeaderParser parser, int length)
            {
                throw GetTooLongFrameException();

                TooLongFrameException GetTooLongFrameException()
                {
                    return new TooLongFrameException(parser.NewExceptionMessage(length));
                }
            }

            protected virtual string NewExceptionMessage(int length) => $"HTTP header is larger than {length} bytes.";
        }

        sealed class LineParser : HeaderParser
        {
            internal LineParser(AppendableCharSequence seq, int maxLength) 
                : base(seq, maxLength)
            {
            }

            public override AppendableCharSequence Parse(IByteBuffer buffer)
            {
                this.Reset();
                return base.Parse(buffer);
            }

            protected override string NewExceptionMessage(int maxLength) => $"An HTTP line is larger than {maxLength} bytes.";
        }

        // Similar to char.IsWhiteSpace for ascii
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsWhiteSpace(byte c)
        {
            switch (c)
            {
                case HttpConstants.HorizontalSpace:
                case HttpConstants.HorizontalTab:
                case HttpConstants.CarriageReturn:
                    return true;
            }
            return false;
        }
    }
}
