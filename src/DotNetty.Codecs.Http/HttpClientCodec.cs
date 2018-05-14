// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System.Collections.Generic;
    using System.Threading;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class HttpClientCodec : CombinedChannelDuplexHandler<HttpResponseDecoder, HttpRequestEncoder>, 
        HttpClientUpgradeHandler.ISourceCodec
    {
        // A queue that is used for correlating a request and a response.
        readonly Queue<HttpMethod> queue = new Queue<HttpMethod>();
        readonly bool parseHttpAfterConnectRequest;

        // If true, decoding stops (i.e. pass-through)
        bool done;

        long requestResponseCounter;
        readonly bool failOnMissingResponse;

        public HttpClientCodec() : this(4096, 8192, 8192, false)
        {
        }

        public HttpClientCodec(int maxInitialLineLength, int maxHeaderSize, int maxChunkSize)
            : this(maxInitialLineLength, maxHeaderSize, maxChunkSize, false)
        {
        }

        public HttpClientCodec(
            int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool failOnMissingResponse)
            : this(maxInitialLineLength, maxHeaderSize, maxChunkSize, failOnMissingResponse, true)
        {
        }

        public HttpClientCodec(
            int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool failOnMissingResponse,
            bool validateHeaders)
            : this(maxInitialLineLength, maxHeaderSize, maxChunkSize, failOnMissingResponse, validateHeaders, false)
        {
        }

        public HttpClientCodec(
            int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool failOnMissingResponse, 
            bool validateHeaders, bool parseHttpAfterConnectRequest)
        {
            this.Init(new Decoder(this, maxInitialLineLength, maxHeaderSize, maxChunkSize, validateHeaders), new Encoder(this));
            this.failOnMissingResponse = failOnMissingResponse;
            this.parseHttpAfterConnectRequest = parseHttpAfterConnectRequest;
        }

        public HttpClientCodec(
            int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool failOnMissingResponse,
            bool validateHeaders, int initialBufferSize)
            : this(maxInitialLineLength, maxHeaderSize, maxChunkSize, failOnMissingResponse, validateHeaders, initialBufferSize, false)
        {
        }

        public HttpClientCodec(
            int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool failOnMissingResponse,
            bool validateHeaders, int initialBufferSize, bool parseHttpAfterConnectRequest)
        {
            this.Init(new Decoder(this, maxInitialLineLength, maxHeaderSize, maxChunkSize, validateHeaders, initialBufferSize), new Encoder(this));
            this.parseHttpAfterConnectRequest = parseHttpAfterConnectRequest;
            this.failOnMissingResponse = failOnMissingResponse;
        }

        public void PrepareUpgradeFrom(IChannelHandlerContext ctx) => ((Encoder)this.OutboundHandler).Upgraded = true;

        public void UpgradeFrom(IChannelHandlerContext ctx)
        {
            IChannelPipeline p = ctx.Channel.Pipeline;
            p.Remove(this);
        }

        public bool SingleDecode
        {
            get => this.InboundHandler.SingleDecode;
            set => this.InboundHandler.SingleDecode = value;
        }

        sealed class Encoder : HttpRequestEncoder
        {
            readonly HttpClientCodec clientCodec;
            internal bool Upgraded;

            public Encoder(HttpClientCodec clientCodec)
            {
                this.clientCodec = clientCodec;
            }

            protected override void Encode(IChannelHandlerContext context, object message, List<object> output)
            {
                if (this.Upgraded)
                {
                    output.Add(ReferenceCountUtil.Retain(message));
                    return;
                }

                if (message is IHttpRequest request && !this.clientCodec.done)
                {
                    this.clientCodec.queue.Enqueue(request.Method);
                }

                base.Encode(context, message, output);

                if (this.clientCodec.failOnMissingResponse && !this.clientCodec.done)
                {
                    // check if the request is chunked if so do not increment
                    if (message is ILastHttpContent)
                    {
                        // increment as its the last chunk
                        Interlocked.Increment(ref this.clientCodec.requestResponseCounter);
                    }
                }
            }
        }

        sealed class Decoder : HttpResponseDecoder
        {
            readonly HttpClientCodec clientCodec;

            internal Decoder(HttpClientCodec clientCodec, int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool validateHeaders)
                : base(maxInitialLineLength, maxHeaderSize, maxChunkSize, validateHeaders)
            {
                this.clientCodec = clientCodec;
            }

            internal Decoder(HttpClientCodec clientCodec, int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool validateHeaders, int initialBufferSize)
                : base(maxInitialLineLength, maxHeaderSize, maxChunkSize, validateHeaders, initialBufferSize)
            {
                this.clientCodec = clientCodec;
            }

            protected override void Decode(IChannelHandlerContext context, IByteBuffer buffer, List<object> output)
            {
                if (this.clientCodec.done)
                {
                    int readable = this.ActualReadableBytes;
                    if (readable == 0)
                    {
                        // if non is readable just return null
                        // https://github.com/netty/netty/issues/1159
                        return;
                    }
                    output.Add(buffer.ReadBytes(readable));
                }
                else
                {
                    int oldSize = output.Count;
                    base.Decode(context, buffer, output);
                    if (this.clientCodec.failOnMissingResponse)
                    {
                        int size = output.Count;
                        for (int i = oldSize; i < size; i++)
                        {
                            this.Decrement(output[i]);
                        }
                    }
                }
            }

            void Decrement(object msg)
            {
                if (ReferenceEquals(null, msg))
                {
                    return;
                }

                // check if it's an Header and its transfer encoding is not chunked.
                if (msg is ILastHttpContent)
                {
                    Interlocked.Decrement(ref this.clientCodec.requestResponseCounter);
                }
            }

            protected override bool IsContentAlwaysEmpty(IHttpMessage msg)
            {
                int statusCode = ((IHttpResponse)msg).Status.Code;
                if (statusCode == 100 || statusCode == 101)
                {
                    // 100-continue and 101 switching protocols response should be excluded from paired comparison.
                    // Just delegate to super method which has all the needed handling.
                    return  base.IsContentAlwaysEmpty(msg);
                }

                // Get the getMethod of the HTTP request that corresponds to the
                // current response.
                HttpMethod method = this.clientCodec.queue.Dequeue();

                char firstChar = method.AsciiName[0];
                switch (firstChar)
                {
                    case 'H':
                        // According to 4.3, RFC2616:
                        // All responses to the HEAD request getMethod MUST NOT include a
                        // message-body, even though the presence of entity-header fields
                        // might lead one to believe they do.
                        if (HttpMethod.Head.Equals(method))
                        {
                            return true;

                            // The following code was inserted to work around the servers
                            // that behave incorrectly.  It has been commented out
                            // because it does not work with well behaving servers.
                            // Please note, even if the 'Transfer-Encoding: chunked'
                            // header exists in the HEAD response, the response should
                            // have absolutely no content.
                            //
                            // Interesting edge case:
                            // Some poorly implemented servers will send a zero-byte
                            // chunk if Transfer-Encoding of the response is 'chunked'.
                            //
                            // return !msg.isChunked();
                        }
                        break;
                    case 'C':
                        // Successful CONNECT request results in a response with empty body.
                        if (statusCode == 200)
                        {
                            if (HttpMethod.Connect.Equals(method))
                            {
                                // Proxy connection established - Parse HTTP only if configured by parseHttpAfterConnectRequest,
                                // else pass through.
                                if (!this.clientCodec.parseHttpAfterConnectRequest)
                                {
                                    this.clientCodec.done = true;
                                    this.clientCodec.queue.Clear();
                                }
                                return true;
                            }
                        }
                        break;
                }

                return base.IsContentAlwaysEmpty(msg);
            }

            public override void ChannelInactive(IChannelHandlerContext ctx)
            {
                base.ChannelInactive(ctx);

                if (this.clientCodec.failOnMissingResponse)
                {
                    long missingResponses = Interlocked.Read(ref this.clientCodec.requestResponseCounter);
                    if (missingResponses > 0)
                    {
                        ctx.FireExceptionCaught(new PrematureChannelClosureException(
                            $"channel gone inactive with {missingResponses} missing response(s)"));
                    }
                }
            }
        }
    }
}
