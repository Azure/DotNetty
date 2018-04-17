// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    using static HttpVersion;

    public class WebSocketServerProtocolHandler : WebSocketProtocolHandler
    {
        public sealed class HandshakeComplete
        {
            readonly string requestUri;
            readonly HttpHeaders requestHeaders;
            readonly string selectedSubprotocol;

            internal HandshakeComplete(string requestUri, HttpHeaders requestHeaders, string selectedSubprotocol)
            {
                this.requestUri = requestUri;
                this.requestHeaders = requestHeaders;
                this.selectedSubprotocol = selectedSubprotocol;
            }

            public string RequestUri => this.requestUri;

            public HttpHeaders RequestHeaders => this.requestHeaders;

            public string SelectedSubprotocol => this.selectedSubprotocol;
        }

        static readonly AttributeKey<WebSocketServerHandshaker> HandshakerAttrKey = 
            AttributeKey<WebSocketServerHandshaker>.ValueOf("HANDSHAKER");

        readonly string websocketPath;
        readonly string subprotocols;
        readonly bool allowExtensions;
        readonly int maxFramePayloadLength;
        readonly bool allowMaskMismatch;
        readonly bool checkStartsWith;

        public WebSocketServerProtocolHandler(string websocketPath)
            : this(websocketPath, null, false)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, bool checkStartsWith)
            : this(websocketPath, null, false, 65536, false, checkStartsWith)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols)
            : this(websocketPath, subprotocols, false)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols, bool allowExtensions)
            : this(websocketPath, subprotocols, allowExtensions, 65536)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols,
            bool allowExtensions, int maxFrameSize)
            : this(websocketPath, subprotocols, allowExtensions, maxFrameSize, false)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols,
            bool allowExtensions, int maxFrameSize, bool allowMaskMismatch)
            : this(websocketPath, subprotocols, allowExtensions, maxFrameSize, allowMaskMismatch, false)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols, bool allowExtensions,
            int maxFrameSize, bool allowMaskMismatch, bool checkStartsWith)
            : this(websocketPath, subprotocols, allowExtensions, maxFrameSize, allowMaskMismatch, checkStartsWith, true)
        {
        }

        public WebSocketServerProtocolHandler(string websocketPath, string subprotocols,
            bool allowExtensions, int maxFrameSize, bool allowMaskMismatch, bool checkStartsWith, bool dropPongFrames)
            : base(dropPongFrames)
        {
            this.websocketPath = websocketPath;
            this.subprotocols = subprotocols;
            this.allowExtensions = allowExtensions;
            this.maxFramePayloadLength = maxFrameSize;
            this.allowMaskMismatch = allowMaskMismatch;
            this.checkStartsWith = checkStartsWith;
        }

        public override void HandlerAdded(IChannelHandlerContext ctx)
        {
            IChannelPipeline cp = ctx.Channel.Pipeline;
            if (cp.Get<WebSocketServerProtocolHandshakeHandler>() == null)
            {
                // Add the WebSocketHandshakeHandler before this one.
                ctx.Channel.Pipeline.AddBefore(ctx.Name, nameof(WebSocketServerProtocolHandshakeHandler),
                    new WebSocketServerProtocolHandshakeHandler(
                        this.websocketPath, 
                        this.subprotocols,
                        this.allowExtensions,
                        this.maxFramePayloadLength,
                        this.allowMaskMismatch,
                        this.checkStartsWith));
            }

            if (cp.Get<Utf8FrameValidator>() == null)
            {
                // Add the UFT8 checking before this one.
                ctx.Channel.Pipeline.AddBefore(ctx.Name, nameof(Utf8FrameValidator), new Utf8FrameValidator());
            }
        }

        protected override void Decode(IChannelHandlerContext ctx, WebSocketFrame frame, List<object> output)
        {
            if (frame is CloseWebSocketFrame socketFrame)
            {
                WebSocketServerHandshaker handshaker = GetHandshaker(ctx.Channel);
                if (handshaker != null)
                {
                    frame.Retain();
                    handshaker.CloseAsync(ctx.Channel, socketFrame);
                }
                else
                {
                    ctx.WriteAndFlushAsync(Unpooled.Empty)
                        .ContinueWith((t, c) => ((IChannelHandlerContext)c).CloseAsync(),
                            ctx, TaskContinuationOptions.ExecuteSynchronously);
                }

                return;
            }

            base.Decode(ctx, frame, output);
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            if (cause is WebSocketHandshakeException)
            {
                var response = new DefaultFullHttpResponse(Http11, HttpResponseStatus.BadRequest,
                    Unpooled.WrappedBuffer(Encoding.ASCII.GetBytes(cause.Message)));
                ctx.Channel.WriteAndFlushAsync(response)
                    .ContinueWith((t, c) => ((IChannelHandlerContext)c).CloseAsync(),
                        ctx, TaskContinuationOptions.ExecuteSynchronously);
            }
            else
            {
                ctx.FireExceptionCaught(cause);
                ctx.CloseAsync();
            }
        }

        internal static WebSocketServerHandshaker GetHandshaker(IChannel channel) => channel.GetAttribute(HandshakerAttrKey).Get();

        internal static void SetHandshaker(IChannel channel, WebSocketServerHandshaker handshaker) => channel.GetAttribute(HandshakerAttrKey).Set(handshaker);

        internal static IChannelHandler ForbiddenHttpRequestResponder() => new ForbiddenResponseHandler();

        sealed class ForbiddenResponseHandler : ChannelHandlerAdapter
        {
            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                if (msg is IFullHttpRequest request)
                {
                    request.Release();
                    var response = new DefaultFullHttpResponse(Http11, HttpResponseStatus.Forbidden);
                    ctx.Channel.WriteAndFlushAsync(response);
                }
                else
                {
                    ctx.FireChannelRead(msg);
                }
            }
        }
    }
}
