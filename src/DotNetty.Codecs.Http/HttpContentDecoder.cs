// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;

    public abstract class HttpContentDecoder : MessageToMessageDecoder<IHttpObject>
    {
        internal static readonly AsciiString Identity = HttpHeaderValues.Identity;

        protected IChannelHandlerContext HandlerContext;
        EmbeddedChannel decoder;
        bool continueResponse;

        protected override void Decode(IChannelHandlerContext context, IHttpObject message, List<object> output)
        {
            if (message is IHttpResponse response && response.Status.Code == 100)
            {
                if (!(response is ILastHttpContent))
                {
                    this.continueResponse = true;
                }
                // 100-continue response must be passed through.
                output.Add(ReferenceCountUtil.Retain(message));
                return;
            }

            if (this.continueResponse)
            {
                if (message is ILastHttpContent)
                {
                    this.continueResponse = false;
                }
                // 100-continue response must be passed through.
                output.Add(ReferenceCountUtil.Retain(message));
                return;
            }

            if (message is IHttpMessage httpMessage)
            {
                this.Cleanup();
                HttpHeaders headers = httpMessage.Headers;

                // Determine the content encoding.
                if (headers.TryGet(HttpHeaderNames.ContentEncoding, out ICharSequence contentEncoding))
                {
                    contentEncoding = AsciiString.Trim(contentEncoding);
                }
                else
                {
                    contentEncoding = Identity;
                }
                this.decoder = this.NewContentDecoder(contentEncoding);

                if (this.decoder == null)
                {
                    if (httpMessage is IHttpContent httpContent)
                    {
                        httpContent.Retain();
                    }
                    output.Add(httpMessage);
                    return;
                }

                // Remove content-length header:
                // the correct value can be set only after all chunks are processed/decoded.
                // If buffering is not an issue, add HttpObjectAggregator down the chain, it will set the header.
                // Otherwise, rely on LastHttpContent message.
                if (headers.Contains(HttpHeaderNames.ContentLength))
                {
                    headers.Remove(HttpHeaderNames.ContentLength);
                    headers.Set(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);
                }
                // Either it is already chunked or EOF terminated.
                // See https://github.com/netty/netty/issues/5892

                // set new content encoding,
                ICharSequence targetContentEncoding = this.GetTargetContentEncoding(contentEncoding);
                if (HttpHeaderValues.Identity.ContentEquals(targetContentEncoding))
                {
                    // Do NOT set the 'Content-Encoding' header if the target encoding is 'identity'
                    // as per: http://tools.ietf.org/html/rfc2616#section-14.11
                    headers.Remove(HttpHeaderNames.ContentEncoding);
                }
                else
                {
                    headers.Set(HttpHeaderNames.ContentEncoding, targetContentEncoding);
                }

                if (httpMessage is IHttpContent)
                {
                    // If message is a full request or response object (headers + data), don't copy data part into out.
                    // Output headers only; data part will be decoded below.
                    // Note: "copy" object must not be an instance of LastHttpContent class,
                    // as this would (erroneously) indicate the end of the HttpMessage to other handlers.
                    IHttpMessage copy;
                    if (httpMessage is IHttpRequest req)
                    {
                        // HttpRequest or FullHttpRequest
                        copy = new DefaultHttpRequest(req.ProtocolVersion, req.Method, req.Uri);
                    }
                    else if (httpMessage is IHttpResponse res)
                    {
                        // HttpResponse or FullHttpResponse
                        copy = new DefaultHttpResponse(res.ProtocolVersion, res.Status);
                    }
                    else
                    {
                        throw new CodecException($"Object of class {StringUtil.SimpleClassName(httpMessage.GetType())} is not a HttpRequest or HttpResponse");
                    }
                    copy.Headers.Set(httpMessage.Headers);
                    copy.Result = httpMessage.Result;
                    output.Add(copy);
                }
                else
                {
                    output.Add(httpMessage);
                }
            }

            if (message is IHttpContent c)
            {
                if (this.decoder == null)
                {
                    output.Add(c.Retain());
                }
                else
                {
                    this.DecodeContent(c, output);
                }
            }
        }

        void DecodeContent(IHttpContent c, IList<object> output)
        {
            IByteBuffer content = c.Content;

            this.Decode(content, output);

            if (c is ILastHttpContent last)
            {
                this.FinishDecode(output);

                // Generate an additional chunk if the decoder produced
                // the last product on closure,
                HttpHeaders headers = last.TrailingHeaders;
                if (headers.IsEmpty)
                {
                    output.Add(EmptyLastHttpContent.Default);
                }
                else
                {
                    output.Add(new ComposedLastHttpContent(headers));
                }
            }
        }

        protected abstract EmbeddedChannel NewContentDecoder(ICharSequence contentEncoding);

        protected ICharSequence GetTargetContentEncoding(ICharSequence contentEncoding) => Identity;

        public override void HandlerRemoved(IChannelHandlerContext context)
        {
            this.CleanupSafely(context);
            base.HandlerRemoved(context);
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            this.CleanupSafely(context);
            base.ChannelInactive(context);
        }

        public override void HandlerAdded(IChannelHandlerContext context)
        {
            this.HandlerContext = context;
            base.HandlerAdded(context);
        }

        void Cleanup()
        {
            if (this.decoder != null)
            {
                this.decoder.FinishAndReleaseAll();
                this.decoder = null;
            }
        }

        void CleanupSafely(IChannelHandlerContext context)
        {
            try
            {
                this.Cleanup();
            }
            catch (Exception cause)
            {
                // If cleanup throws any error we need to propagate it through the pipeline
                // so we don't fail to propagate pipeline events.
                context.FireExceptionCaught(cause);
            }
        }

        void Decode(IByteBuffer buf, IList<object> output)
        {
            // call retain here as it will call release after its written to the channel
            this.decoder.WriteInbound(buf.Retain());
            this.FetchDecoderOutput(output);
        }

        void FinishDecode(ICollection<object> output)
        {
            if (this.decoder.Finish())
            {
                this.FetchDecoderOutput(output);
            }
            this.decoder = null;
        }

        void FetchDecoderOutput(ICollection<object> output)
        {
            for (;;)
            {
                var buf = this.decoder.ReadInbound<IByteBuffer>();
                if (buf == null)
                {
                    break;
                }
                if (!buf.IsReadable())
                {
                    buf.Release();
                    continue;
                }
                output.Add(new DefaultHttpContent(buf));
            }
        }
    }
}
