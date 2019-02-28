// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
// ReSharper disable once ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoPropertyWhenPossible
namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Transport.Channels;

    public class WebSocketClientProtocolHandler : WebSocketProtocolHandler
    {
        readonly WebSocketClientHandshaker handshaker;
        readonly bool handleCloseFrames;

        public WebSocketClientHandshaker Handshaker => this.handshaker;

        /// <summary>
        /// Events that are fired to notify about handshake status
        /// </summary>
        public enum ClientHandshakeStateEvent
        {
            /// <summary>
            /// The Handshake was started but the server did not response yet to the request
            /// </summary>
            HandshakeIssued,

            /// <summary>
            /// The Handshake was complete succesful and so the channel was upgraded to websockets
            /// </summary>
            HandshakeComplete
        }

        public WebSocketClientProtocolHandler(Uri webSocketUrl, WebSocketVersion version, string subprotocol,
            bool allowExtensions, HttpHeaders customHeaders,
            int maxFramePayloadLength, bool handleCloseFrames,
            bool performMasking, bool allowMaskMismatch)
            : this(WebSocketClientHandshakerFactory.NewHandshaker(webSocketUrl, version, subprotocol,
                allowExtensions, customHeaders, maxFramePayloadLength,
                performMasking, allowMaskMismatch), handleCloseFrames)
        {
        }

        public WebSocketClientProtocolHandler(Uri webSocketUrl, WebSocketVersion version, string subprotocol,
            bool allowExtensions, HttpHeaders customHeaders,
            int maxFramePayloadLength, bool handleCloseFrames)
            : this(webSocketUrl, version, subprotocol, allowExtensions, customHeaders, maxFramePayloadLength,
                handleCloseFrames, true, false)
        {
        }

        public WebSocketClientProtocolHandler(Uri webSocketUrl, WebSocketVersion version, string subprotocol,
            bool allowExtensions, HttpHeaders customHeaders,
            int maxFramePayloadLength)
            : this(webSocketUrl, version, subprotocol, allowExtensions, customHeaders, maxFramePayloadLength, true)
        {
        }

        public WebSocketClientProtocolHandler(WebSocketClientHandshaker handshaker, bool handleCloseFrames)
        {
            this.handshaker = handshaker;
            this.handleCloseFrames = handleCloseFrames;
        }

        public WebSocketClientProtocolHandler(WebSocketClientHandshaker handshaker)
            : this(handshaker, true)
        {
        }

        protected override void Decode(IChannelHandlerContext ctx, WebSocketFrame frame, List<object> output)
        {
            if (this.handleCloseFrames && frame is CloseWebSocketFrame)
            {
                ctx.CloseAsync();
                return;
            }

            base.Decode(ctx, frame, output);
        }

        public override void HandlerAdded(IChannelHandlerContext ctx)
        {
            IChannelPipeline cp = ctx.Channel.Pipeline;
            if (cp.Get<WebSocketClientProtocolHandshakeHandler>() == null)
            {
                // Add the WebSocketClientProtocolHandshakeHandler before this one.
                ctx.Channel.Pipeline.AddBefore(ctx.Name, nameof(WebSocketClientProtocolHandshakeHandler),
                    new WebSocketClientProtocolHandshakeHandler(this.handshaker));
            }
            if (cp.Get<Utf8FrameValidator>() == null)
            {
                // Add the UFT8 checking before this one.
                ctx.Channel.Pipeline.AddBefore(ctx.Name, nameof(Utf8FrameValidator),
                    new Utf8FrameValidator());
            }
        }
    }
}
