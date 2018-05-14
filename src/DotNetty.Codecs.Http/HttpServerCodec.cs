// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    public class HttpServerCodec : CombinedChannelDuplexHandler<HttpRequestDecoder, HttpResponseEncoder>,
        HttpServerUpgradeHandler.ISourceCodec
    {
        /** A queue that is used for correlating a request and a response. */
        readonly Queue<HttpMethod> queue = new Queue<HttpMethod>();

        public HttpServerCodec() : this(4096, 8192, 8192)
        {
        }

        public HttpServerCodec(int maxInitialLineLength, int maxHeaderSize, int maxChunkSize)
        {
            this.Init(new HttpServerRequestDecoder(this, maxInitialLineLength, maxHeaderSize, maxChunkSize),
                new HttpServerResponseEncoder(this));
        }

        public HttpServerCodec(int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool validateHeaders)
        {
            this.Init(new HttpServerRequestDecoder(this, maxInitialLineLength, maxHeaderSize, maxChunkSize, validateHeaders), 
                new HttpServerResponseEncoder(this));
        }

        public HttpServerCodec(int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool validateHeaders, int initialBufferSize)
        {
            this.Init(new HttpServerRequestDecoder(this, maxInitialLineLength, maxHeaderSize, maxChunkSize, validateHeaders, initialBufferSize), 
                new HttpServerResponseEncoder(this));
        }

        public void UpgradeFrom(IChannelHandlerContext ctx) => ctx.Channel.Pipeline.Remove(this);

        sealed class HttpServerRequestDecoder : HttpRequestDecoder
        {
            readonly HttpServerCodec serverCodec;

            public HttpServerRequestDecoder(HttpServerCodec serverCodec, int maxInitialLineLength, int maxHeaderSize, int maxChunkSize)
                : base(maxInitialLineLength, maxHeaderSize, maxChunkSize)
            {
                this.serverCodec = serverCodec;
            }

            public HttpServerRequestDecoder(HttpServerCodec serverCodec, int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool validateHeaders)
                :base(maxInitialLineLength, maxHeaderSize, maxChunkSize, validateHeaders)
            {
                this.serverCodec = serverCodec;
            }

            public HttpServerRequestDecoder(HttpServerCodec serverCodec, 
                int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool validateHeaders, int initialBufferSize)
                : base(maxInitialLineLength, maxHeaderSize, maxChunkSize, validateHeaders, initialBufferSize)
            {
                this.serverCodec = serverCodec;
            }

            protected override void Decode(IChannelHandlerContext context, IByteBuffer buffer, List<object> output)
            {
                int oldSize = output.Count;
                base.Decode(context, buffer, output);
                int size = output.Count;
                for (int i = oldSize; i < size; i++)
                {
                    if (output[i] is IHttpRequest request)
                    {
                        this.serverCodec.queue.Enqueue(request.Method);
                    }
                }
            }
        }

        sealed class HttpServerResponseEncoder : HttpResponseEncoder
        {
            readonly HttpServerCodec serverCodec;
            HttpMethod method;

            public HttpServerResponseEncoder(HttpServerCodec serverCodec)
            {
                this.serverCodec = serverCodec;
            }

            protected override void SanitizeHeadersBeforeEncode(IHttpResponse msg, bool isAlwaysEmpty)
            {
                if (!isAlwaysEmpty && ReferenceEquals(this.method, HttpMethod.Connect) && msg.Status.CodeClass == HttpStatusClass.Success)
                {
                    // Stripping Transfer-Encoding:
                    // See https://tools.ietf.org/html/rfc7230#section-3.3.1
                    msg.Headers.Remove(HttpHeaderNames.TransferEncoding);
                    return;
                }

                base.SanitizeHeadersBeforeEncode(msg, isAlwaysEmpty);
            }


            protected override bool IsContentAlwaysEmpty(IHttpResponse msg)
            {
                this.method = this.serverCodec.queue.Count > 0 ? this.serverCodec.queue.Dequeue() : null;
                return HttpMethod.Head.Equals(this.method) || base.IsContentAlwaysEmpty(msg);
            }
        }
    }
}
