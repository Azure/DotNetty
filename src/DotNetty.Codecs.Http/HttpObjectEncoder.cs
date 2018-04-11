// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public abstract class HttpObjectEncoder<T> : MessageToMessageEncoder<object> where T : IHttpMessage
    {
        const float HeadersWeightNew = 1 / 5f;
        const float HeadersWeightHistorical = 1 - HeadersWeightNew;
        const float TrailersWeightNew = HeadersWeightNew;
        const float TrailersWeightHistorical = HeadersWeightHistorical;

        const int StInit = 0;
        const int StContentNonChunk = 1;
        const int StContentChunk = 2;
        const int StContentAlwaysEmpty = 3;

        int state = StInit;

        // Used to calculate an exponential moving average of the encoded size of the initial line and the headers for
        // a guess for future buffer allocations.
        float headersEncodedSizeAccumulator = 256;

        // Used to calculate an exponential moving average of the encoded size of the trailers for
        // a guess for future buffer allocations.
        float trailersEncodedSizeAccumulator = 256;

        protected override void Encode(IChannelHandlerContext context, object message, List<object> output)
        {
            IByteBuffer buf = null;
            if (message is IHttpMessage)
            {
                if (this.state != StInit)
                {
                    throw new InvalidOperationException($"unexpected message type: {StringUtil.SimpleClassName(message)}");
                }

                var m = (T)message;

                buf = context.Allocator.Buffer((int)this.headersEncodedSizeAccumulator);
                // Encode the message.
                this.EncodeInitialLine(buf, m);
                this.state = this.IsContentAlwaysEmpty(m) ? StContentAlwaysEmpty
                    : HttpUtil.IsTransferEncodingChunked(m) ? StContentChunk : StContentNonChunk;

                this.SanitizeHeadersBeforeEncode(m, this.state == StContentAlwaysEmpty);

                this.EncodeHeaders(m.Headers, buf);
                buf.WriteShort(HttpConstants.CrlfShort);

                this.headersEncodedSizeAccumulator = HeadersWeightNew * PadSizeForAccumulation(buf.ReadableBytes) 
                    + HeadersWeightHistorical * this.headersEncodedSizeAccumulator;
            }

            // Bypass the encoder in case of an empty buffer, so that the following idiom works:
            //
            //     ch.write(Unpooled.EMPTY_BUFFER).addListener(ChannelFutureListener.CLOSE);
            //
            // See https://github.com/netty/netty/issues/2983 for more information.
            if (message is IByteBuffer potentialEmptyBuf)
            {
                if (!potentialEmptyBuf.IsReadable())
                {
                    output.Add(potentialEmptyBuf.Retain());
                    return;
                }
            }

            if (message is IHttpContent || message is IByteBuffer || message is IFileRegion)
            {
                switch (this.state)
                {
                    case StInit:
                        throw new InvalidOperationException($"unexpected message type: {StringUtil.SimpleClassName(message)}");
                    case StContentNonChunk:
                        long contentLength = ContentLength(message);
                        if (contentLength > 0)
                        {
                            if (buf != null && buf.WritableBytes >= contentLength && message is IHttpContent)
                            {
                                // merge into other buffer for performance reasons
                                buf.WriteBytes(((IHttpContent)message).Content);
                                output.Add(buf);
                            }
                            else
                            {
                                if (buf != null)
                                {
                                    output.Add(buf);
                                }
                                output.Add(EncodeAndRetain(message));
                            }

                            if (message is ILastHttpContent)
                            {
                                this.state = StInit;
                            }
                            break;
                        }

                        goto case StContentAlwaysEmpty; // fall-through!
                    case StContentAlwaysEmpty:
                        // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
                        if (buf != null)
                        {
                            // We allocated a buffer so add it now.
                            output.Add(buf);
                        }
                        else
                        {
                            // Need to produce some output otherwise an
                            // IllegalStateException will be thrown as we did not write anything
                            // Its ok to just write an EMPTY_BUFFER as if there are reference count issues these will be
                            // propagated as the caller of the encode(...) method will release the original
                            // buffer.
                            // Writing an empty buffer will not actually write anything on the wire, so if there is a user
                            // error with msg it will not be visible externally
                            output.Add(Unpooled.Empty);
                        }

                        break;
                    case StContentChunk:
                        if (buf != null)
                        {
                            // We allocated a buffer so add it now.
                            output.Add(buf);
                        }
                        this.EncodeChunkedContent(context, message, ContentLength(message), output);

                        break;
                    default:
                        throw new EncoderException($"unexpected state {this.state}: {StringUtil.SimpleClassName(message)}");
                }

                if (message is ILastHttpContent)
                {
                    this.state = StInit;
                }
            }
            else if (buf != null)
            {
                output.Add(buf);
            }
        }

        protected void EncodeHeaders(HttpHeaders headers, IByteBuffer buf)
        {
            foreach (HeaderEntry<AsciiString, ICharSequence> header in headers)
            {
                HttpHeadersEncoder.EncoderHeader(header.Key, header.Value, buf);
            }
        }

        void EncodeChunkedContent(IChannelHandlerContext context, object message, long contentLength, ICollection<object> output)
        {
            if (contentLength > 0)
            {
                var lengthHex = new AsciiString(Convert.ToString(contentLength, 16), Encoding.ASCII);
                IByteBuffer buf = context.Allocator.Buffer(lengthHex.Count + 2);
                buf.WriteCharSequence(lengthHex, Encoding.ASCII);
                buf.WriteShort(HttpConstants.CrlfShort);
                output.Add(buf);
                output.Add(EncodeAndRetain(message));
                output.Add(HttpConstants.CrlfBuf.Duplicate());
            }

            if (message is ILastHttpContent content)
            {
                HttpHeaders headers = content.TrailingHeaders;
                if (headers.IsEmpty)
                {
                    output.Add(HttpConstants.ZeroCrlfCrlfBuf.Duplicate());
                }
                else
                {
                    IByteBuffer buf = context.Allocator.Buffer((int)this.trailersEncodedSizeAccumulator);
                    buf.WriteMedium(HttpConstants.ZeroCrlfMedium);
                    this.EncodeHeaders(headers, buf);
                    buf.WriteShort(HttpConstants.CrlfShort);
                    this.trailersEncodedSizeAccumulator = TrailersWeightNew * PadSizeForAccumulation(buf.ReadableBytes) 
                        + TrailersWeightHistorical * this.trailersEncodedSizeAccumulator;
                    output.Add(buf);
                }
            }
            else if (contentLength == 0)
            {
                // Need to produce some output otherwise an
                // IllegalstateException will be thrown
                output.Add(ReferenceCountUtil.Retain(message));
            }
        }

        // Allows to sanitize headers of the message before encoding these.
        protected virtual void SanitizeHeadersBeforeEncode(T msg, bool isAlwaysEmpty)
        {
            // noop
        }

        protected virtual bool IsContentAlwaysEmpty(T msg) => false;

        public override bool AcceptOutboundMessage(object msg) => msg is IHttpObject || msg is IByteBuffer || msg is IFileRegion;

        static object EncodeAndRetain(object message)
        {
            if (message is IByteBuffer buffer)
            {
                return buffer.Retain();
            }
            if (message is IHttpContent content)
            {
                return content.Content.Retain();
            }
            if (message is IFileRegion region) 
            {
                return region.Retain();
            }
            throw new InvalidOperationException($"unexpected message type: {StringUtil.SimpleClassName(message)}");
        }

        static long ContentLength(object message)
        {
            if (message is IHttpContent content)
            {
                return content.Content.ReadableBytes;
            }
            if (message is IByteBuffer buffer)
            {
                return buffer.ReadableBytes;
            }
            if (message is IFileRegion region) 
            {
                return region.Count;
            }
            throw new InvalidOperationException($"unexpected message type: {StringUtil.SimpleClassName(message)}");
        }

        // Add some additional overhead to the buffer. The rational is that it is better to slightly over allocate and waste
        // some memory, rather than under allocate and require a resize/copy.
        // @param readableBytes The readable bytes in the buffer.
        // @return The {@code readableBytes} with some additional padding.
        static int PadSizeForAccumulation(int readableBytes) => (readableBytes << 2) / 3;

        protected internal abstract void EncodeInitialLine(IByteBuffer buf, T message);
    }
}
