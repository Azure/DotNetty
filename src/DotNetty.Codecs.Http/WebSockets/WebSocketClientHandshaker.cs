// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWhenPossible
namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public abstract class WebSocketClientHandshaker
    {
        static readonly ClosedChannelException DefaultClosedChannelException = new ClosedChannelException();

        static readonly string HttpSchemePrefix = HttpScheme.Http + "://";
        static readonly string HttpsSchemePrefix = HttpScheme.Https + "://";

        readonly Uri uri;

        readonly WebSocketVersion version;

        volatile bool handshakeComplete;

        readonly string expectedSubprotocol;

        volatile string actualSubprotocol;

        protected readonly HttpHeaders CustomHeaders;

        readonly int maxFramePayloadLength;

        protected WebSocketClientHandshaker(Uri uri, WebSocketVersion version, string subprotocol,
            HttpHeaders customHeaders, int maxFramePayloadLength)
        {
            this.uri = uri;
            this.version = version;
            this.expectedSubprotocol = subprotocol;
            this.CustomHeaders = customHeaders;
            this.maxFramePayloadLength = maxFramePayloadLength;
        }

        public Uri Uri => this.uri;

        public WebSocketVersion Version => this.version;

        public int MaxFramePayloadLength => this.maxFramePayloadLength;

        public bool IsHandshakeComplete => this.handshakeComplete;

        void SetHandshakeComplete() => this.handshakeComplete = true;

        public string ExpectedSubprotocol => this.expectedSubprotocol;

        public string ActualSubprotocol
        { 
            get => this.actualSubprotocol;
            private set => this.actualSubprotocol = value;
        }

        public Task HandshakeAsync(IChannel channel)
        {
            IFullHttpRequest request = this.NewHandshakeRequest();

            var decoder = channel.Pipeline.Get<HttpResponseDecoder>();
            if (decoder == null)
            {
                var codec = channel.Pipeline.Get<HttpClientCodec>();
                if (codec == null)
                {
                    return TaskEx.FromException(new InvalidOperationException("ChannelPipeline does not contain a HttpResponseDecoder or HttpClientCodec"));
                }
            }

            var completion = new TaskCompletionSource();
            channel.WriteAndFlushAsync(request).ContinueWith((t, state) =>
                {
                    var tcs = (TaskCompletionSource)state;
                    switch (t.Status)
                    {
                        case TaskStatus.RanToCompletion:
                            IChannelPipeline p = channel.Pipeline;
                            IChannelHandlerContext ctx = p.Context<HttpRequestEncoder>() ?? p.Context<HttpClientCodec>();
                            if (ctx == null)
                            {
                                tcs.TrySetException(new InvalidOperationException("ChannelPipeline does not contain a HttpRequestEncoder or HttpClientCodec"));
                                return;
                            }

                            p.AddAfter(ctx.Name, "ws-encoder", this.NewWebSocketEncoder());
                            tcs.TryComplete();
                            break;
                        case TaskStatus.Canceled:
                            tcs.TrySetCanceled();
                            break;
                        case TaskStatus.Faulted:
                            tcs.TryUnwrap(t.Exception);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                },
                completion,
                TaskContinuationOptions.ExecuteSynchronously);

            return completion.Task;
        }

        protected internal abstract IFullHttpRequest NewHandshakeRequest();

        public void FinishHandshake(IChannel channel, IFullHttpResponse response)
        {
            this.Verify(response);

            // Verify the subprotocol that we received from the server.
            // This must be one of our expected subprotocols - or null/empty if we didn't want to speak a subprotocol
            string receivedProtocol = null;
            if (response.Headers.TryGet(HttpHeaderNames.SecWebsocketProtocol, out ICharSequence headerValue))
            {
                receivedProtocol = headerValue.ToString().Trim();
            }

            string expectedProtocol = this.expectedSubprotocol ?? "";
            bool protocolValid = false;

            if (expectedProtocol.Length == 0 && receivedProtocol == null)
            {
                // No subprotocol required and none received
                protocolValid = true;
                this.ActualSubprotocol = this.expectedSubprotocol; // null or "" - we echo what the user requested
            }
            else if (expectedProtocol.Length > 0 && !string.IsNullOrEmpty(receivedProtocol))
            {
                // We require a subprotocol and received one -> verify it
                foreach (string protocol in expectedProtocol.Split(','))
                {
                    if (protocol.Trim().Equals(receivedProtocol))
                    {
                        protocolValid = true;
                        this.ActualSubprotocol = receivedProtocol;
                        break;
                    }
                }
            } // else mixed cases - which are all errors

            if (!protocolValid)
            {
                throw new WebSocketHandshakeException($"Invalid subprotocol. Actual: {receivedProtocol}. Expected one of: {this.expectedSubprotocol}");
            }

            this.SetHandshakeComplete();

            IChannelPipeline p = channel.Pipeline;
            // Remove decompressor from pipeline if its in use
            var decompressor = p.Get<HttpContentDecompressor>();
            if (decompressor != null)
            {
                p.Remove(decompressor);
            }

            // Remove aggregator if present before
            var aggregator = p.Get<HttpObjectAggregator>();
            if (aggregator != null)
            {
                p.Remove(aggregator);
            }

            IChannelHandlerContext ctx = p.Context<HttpResponseDecoder>();
            if (ctx == null)
            {
                ctx = p.Context<HttpClientCodec>();
                if (ctx == null)
                {
                    throw new InvalidOperationException("ChannelPipeline does not contain a HttpRequestEncoder or HttpClientCodec");
                }

                var codec = (HttpClientCodec)ctx.Handler;
                // Remove the encoder part of the codec as the user may start writing frames after this method returns.
                codec.RemoveOutboundHandler();

                p.AddAfter(ctx.Name, "ws-decoder", this.NewWebSocketDecoder());

                // Delay the removal of the decoder so the user can setup the pipeline if needed to handle
                // WebSocketFrame messages.
                // See https://github.com/netty/netty/issues/4533
                channel.EventLoop.Execute(() => p.Remove(codec));
            }
            else
            {
                if (p.Get<HttpRequestEncoder>() != null)
                {
                    // Remove the encoder part of the codec as the user may start writing frames after this method returns.
                    p.Remove<HttpRequestEncoder>();
                }

                IChannelHandlerContext context = ctx;
                p.AddAfter(context.Name, "ws-decoder", this.NewWebSocketDecoder());

                // Delay the removal of the decoder so the user can setup the pipeline if needed to handle
                // WebSocketFrame messages.
                // See https://github.com/netty/netty/issues/4533
                channel.EventLoop.Execute(() => p.Remove(context.Handler));
            }
        }

        public Task ProcessHandshakeAsync(IChannel channel, IHttpResponse response)
        {
            var completionSource = new TaskCompletionSource();
            if (response is IFullHttpResponse res)
            {
                try
                {
                    this.FinishHandshake(channel, res);
                    completionSource.TryComplete();
                }
                catch (Exception cause)
                {
                    completionSource.TrySetException(cause);
                }
            }
            else
            {
                IChannelPipeline p = channel.Pipeline;
                IChannelHandlerContext ctx = p.Context<HttpResponseDecoder>();
                if (ctx == null)
                {
                    ctx = p.Context<HttpClientCodec>();
                    if (ctx == null)
                    {
                        completionSource.TrySetException(new InvalidOperationException("ChannelPipeline does not contain a HttpResponseDecoder or HttpClientCodec"));
                    }
                }
                else
                {
                    // Add aggregator and ensure we feed the HttpResponse so it is aggregated. A limit of 8192 should be more
                    // then enough for the websockets handshake payload.
                    //
                    // TODO: Make handshake work without HttpObjectAggregator at all.
                    const string AggregatorName = "httpAggregator";
                    p.AddAfter(ctx.Name, AggregatorName, new HttpObjectAggregator(8192));
                    p.AddAfter(AggregatorName, "handshaker", new Handshaker(this, channel, completionSource));
                    try
                    {
                        ctx.FireChannelRead(ReferenceCountUtil.Retain(response));
                    }
                    catch (Exception cause)
                    {
                        completionSource.TrySetException(cause);
                    }
                }
            }

            return completionSource.Task;
        }

        sealed class Handshaker : SimpleChannelInboundHandler<IFullHttpResponse>
        {
            readonly WebSocketClientHandshaker clientHandshaker;
            readonly IChannel channel;
            readonly TaskCompletionSource completion;

            public Handshaker(WebSocketClientHandshaker clientHandshaker, IChannel channel, TaskCompletionSource completion)
            {
                this.clientHandshaker = clientHandshaker;
                this.channel = channel;
                this.completion = completion;
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, IFullHttpResponse msg)
            {
                // Remove and do the actual handshake
                ctx.Channel.Pipeline.Remove(this);
                try
                {
                    this.clientHandshaker.FinishHandshake(this.channel, msg);
                    this.completion.TryComplete();
                }
                catch (Exception cause)
                {
                    this.completion.TrySetException(cause);
                }
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                // Remove ourself and fail the handshake promise.
                ctx.Channel.Pipeline.Remove(this);
                this.completion.TrySetException(cause);
            }

            public override void ChannelInactive(IChannelHandlerContext ctx)
            {
                // Fail promise if Channel was closed
                this.completion.TrySetException(DefaultClosedChannelException);
                ctx.FireChannelInactive();
            }
        }

        protected abstract void Verify(IFullHttpResponse response);

        protected internal abstract IWebSocketFrameDecoder NewWebSocketDecoder();

        protected internal abstract IWebSocketFrameEncoder NewWebSocketEncoder();

        public Task CloseAsync(IChannel channel, CloseWebSocketFrame frame)
        {
            Contract.Requires(channel != null);
            return channel.WriteAndFlushAsync(frame);
        }

        internal static string RawPath(Uri wsUrl) => wsUrl.IsAbsoluteUri ? wsUrl.PathAndQuery : "/";

        internal static string WebsocketHostValue(Uri wsUrl)
        {
            string scheme;
            Uri uri;
            if (wsUrl.IsAbsoluteUri)
            {
                scheme = wsUrl.Scheme;
                uri = wsUrl;
            }
            else
            {
                scheme = null;
                uri = AbsoluteUri(wsUrl);
            }

            int port = OriginalPort(uri);
            if (port == -1)
            {
                return uri.Host;
            }
            string host = uri.Host;
            if (port == HttpScheme.Http.Port)
            {
                return HttpScheme.Http.Name.ContentEquals(scheme)
                    || WebSocketScheme.WS.Name.ContentEquals(scheme)
                        ? host : NetUtil.ToSocketAddressString(host, port);
            }
            if (port == HttpScheme.Https.Port)
            {
                return HttpScheme.Https.Name.ToString().Equals(scheme)
                    || WebSocketScheme.WSS.Name.ToString().Equals(scheme) 
                        ? host : NetUtil.ToSocketAddressString(host, port);
            }

            // if the port is not standard (80/443) its needed to add the port to the header.
            // See http://tools.ietf.org/html/rfc6454#section-6.2
            return NetUtil.ToSocketAddressString(host, port);
        }

        internal static string WebsocketOriginValue(Uri wsUrl)
        {
            string scheme;
            Uri uri;
            if (wsUrl.IsAbsoluteUri)
            {
                scheme = wsUrl.Scheme;
                uri = wsUrl;
            }
            else
            {
                scheme = null;
                uri = AbsoluteUri(wsUrl);
            }

            string schemePrefix;
            int port = uri.Port;
            int defaultPort;

            if (WebSocketScheme.WSS.Name.ContentEquals(scheme)
                || HttpScheme.Https.Name.ContentEquals(scheme)
                || (scheme == null && port == WebSocketScheme.WSS.Port))
            {

                schemePrefix = HttpsSchemePrefix;
                defaultPort = WebSocketScheme.WSS.Port;
            }
            else
            {
                schemePrefix = HttpSchemePrefix;
                defaultPort = WebSocketScheme.WS.Port;
            }

            // Convert uri-host to lower case (by RFC 6454, chapter 4 "Origin of a URI")
            string host = uri.Host.ToLower();

            if (port != defaultPort && port != -1)
            {
                // if the port is not standard (80/443) its needed to add the port to the header.
                // See http://tools.ietf.org/html/rfc6454#section-6.2
                return schemePrefix + NetUtil.ToSocketAddressString(host, port);
            }
            return schemePrefix + host;
        }

        static Uri AbsoluteUri(Uri uri)
        {
            if (uri.IsAbsoluteUri)
            {
                return uri;
            }

            string relativeUri = uri.OriginalString;
            return new Uri(relativeUri.StartsWith("//")
                ? HttpScheme.Http + ":" + relativeUri
                : HttpSchemePrefix + relativeUri);
        }

        static int OriginalPort(Uri uri)
        {
            int index = uri.Scheme.Length + 3 + uri.Host.Length;

            if (index < uri.OriginalString.Length 
                && uri.OriginalString[index] == ':')
            {
                return uri.Port;
            }
            return -1;
        }
    }
}
