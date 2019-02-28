// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Text;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    public class WebSocketClientHandshaker08 : WebSocketClientHandshaker
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<WebSocketClientHandshaker08>();

        public static readonly string MagicGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        AsciiString expectedChallengeResponseString;

        readonly bool allowExtensions;
        readonly bool performMasking;
        readonly bool allowMaskMismatch;

        public WebSocketClientHandshaker08(Uri webSocketUrl, WebSocketVersion version, string subprotocol,
            bool allowExtensions, HttpHeaders customHeaders, int maxFramePayloadLength)
            : this(webSocketUrl, version, subprotocol, allowExtensions, customHeaders, maxFramePayloadLength, true, false)
        {
        }

        public WebSocketClientHandshaker08(Uri webSocketUrl, WebSocketVersion version, string subprotocol,
            bool allowExtensions, HttpHeaders customHeaders, int maxFramePayloadLength,
            bool performMasking, bool allowMaskMismatch)
            : base(webSocketUrl, version, subprotocol, customHeaders, maxFramePayloadLength)
        {
            this.allowExtensions = allowExtensions;
            this.performMasking = performMasking;
            this.allowMaskMismatch = allowMaskMismatch;
        }

        protected internal override IFullHttpRequest NewHandshakeRequest()
        {
            // Get path
            Uri wsUrl = this.Uri;
            string path = RawPath(wsUrl);

            // Get 16 bit nonce and base 64 encode it
            byte[] nonce = WebSocketUtil.RandomBytes(16);
            string key = WebSocketUtil.Base64String(nonce);

            string acceptSeed = key + MagicGuid;
            byte[] sha1 = WebSocketUtil.Sha1(Encoding.ASCII.GetBytes(acceptSeed));
            this.expectedChallengeResponseString = new AsciiString(WebSocketUtil.Base64String(sha1));

            if (Logger.DebugEnabled)
            {
                Logger.Debug("WebSocket version 08 client handshake key: {}, expected response: {}",
                    key, this.expectedChallengeResponseString);
            }

            // Format request
            var request = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, path);
            HttpHeaders headers = request.Headers;

            headers.Add(HttpHeaderNames.Upgrade, HttpHeaderValues.Websocket)
                .Add(HttpHeaderNames.Connection, HttpHeaderValues.Upgrade)
                .Add(HttpHeaderNames.SecWebsocketKey, key)
                .Add(HttpHeaderNames.Host, WebsocketHostValue(wsUrl))
                .Add(HttpHeaderNames.SecWebsocketOrigin, WebsocketOriginValue(wsUrl));

            string expectedSubprotocol = this.ExpectedSubprotocol;
            if (!string.IsNullOrEmpty(expectedSubprotocol))
            {
                headers.Add(HttpHeaderNames.SecWebsocketProtocol, expectedSubprotocol);
            }

            headers.Add(HttpHeaderNames.SecWebsocketVersion, "8");

            if (this.CustomHeaders != null)
            {
                headers.Add(this.CustomHeaders);
            }
            return request;
        }

        protected override void Verify(IFullHttpResponse response)
        {
            HttpResponseStatus status = HttpResponseStatus.SwitchingProtocols;
            HttpHeaders headers = response.Headers;

            if (!response.Status.Equals(status))
            {
                throw new WebSocketHandshakeException($"Invalid handshake response getStatus: {response.Status}");
            }

            if (!headers.TryGet(HttpHeaderNames.Upgrade, out ICharSequence upgrade) 
                || !HttpHeaderValues.Websocket.ContentEqualsIgnoreCase(upgrade))
            {
                throw new WebSocketHandshakeException($"Invalid handshake response upgrade: {upgrade}");
            }

            if (!headers.ContainsValue(HttpHeaderNames.Connection, HttpHeaderValues.Upgrade, true))
            {
                headers.TryGet(HttpHeaderNames.Connection, out upgrade);
                throw new WebSocketHandshakeException($"Invalid handshake response connection: {upgrade}");
            }

            if (!headers.TryGet(HttpHeaderNames.SecWebsocketAccept, out ICharSequence accept) 
                || !accept.Equals(this.expectedChallengeResponseString))
            {
                throw new WebSocketHandshakeException($"Invalid challenge. Actual: {accept}. Expected: {this.expectedChallengeResponseString}");
            }
        }

        protected internal override IWebSocketFrameDecoder NewWebSocketDecoder() => new WebSocket08FrameDecoder(
            false, this.allowExtensions, this.MaxFramePayloadLength, this.allowMaskMismatch);

        protected internal override IWebSocketFrameEncoder NewWebSocketEncoder() => new WebSocket08FrameEncoder(this.performMasking);
    }
}
