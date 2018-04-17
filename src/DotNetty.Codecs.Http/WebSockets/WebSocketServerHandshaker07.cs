// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System.Text;
    using DotNetty.Common.Utilities;

    public class WebSocketServerHandshaker07 : WebSocketServerHandshaker
    {
        public static readonly string Websocket07AcceptGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        readonly bool allowExtensions;
        readonly bool allowMaskMismatch;

        public WebSocketServerHandshaker07(string webSocketUrl, string subprotocols, bool allowExtensions, int maxFramePayloadLength)
            : this(webSocketUrl, subprotocols, allowExtensions, maxFramePayloadLength, false)
        {
        }

        public WebSocketServerHandshaker07(string webSocketUrl, string subprotocols, bool allowExtensions, int maxFramePayloadLength,
            bool allowMaskMismatch)
            : base(WebSocketVersion.V07, webSocketUrl, subprotocols, maxFramePayloadLength)
        {
            this.allowExtensions = allowExtensions;
            this.allowMaskMismatch = allowMaskMismatch;
        }

        protected override IFullHttpResponse NewHandshakeResponse(IFullHttpRequest req, HttpHeaders headers)
        {
            var res = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.SwitchingProtocols);

            if (headers != null)
            {
                res.Headers.Add(headers);
            }

            if (!req.Headers.TryGet(HttpHeaderNames.SecWebsocketKey, out ICharSequence key) 
                || key == null)
            {
                throw new WebSocketHandshakeException("not a WebSocket request: missing key");
            }
            string acceptSeed = key + Websocket07AcceptGuid;
            byte[] sha1 = WebSocketUtil.Sha1(Encoding.ASCII.GetBytes(acceptSeed));
            string accept = WebSocketUtil.Base64String(sha1);

            if (Logger.DebugEnabled)
            {
                Logger.Debug("WebSocket version 07 server handshake key: {}, response: {}.", key, accept);
            }

            res.Headers.Add(HttpHeaderNames.Upgrade, HttpHeaderValues.Websocket);
            res.Headers.Add(HttpHeaderNames.Connection, HttpHeaderValues.Upgrade);
            res.Headers.Add(HttpHeaderNames.SecWebsocketAccept, accept);

            
            if (req.Headers.TryGet(HttpHeaderNames.SecWebsocketProtocol, out ICharSequence subprotocols) 
                && subprotocols != null)
            {
                string selectedSubprotocol = this.SelectSubprotocol(subprotocols.ToString());
                if (selectedSubprotocol == null)
                {
                    if (Logger.DebugEnabled)
                    {
                        Logger.Debug("Requested subprotocol(s) not supported: {}", subprotocols);
                    }
                }
                else
                {
                    res.Headers.Add(HttpHeaderNames.SecWebsocketProtocol, selectedSubprotocol);
                }
            }
            return res;
        }

        protected internal override IWebSocketFrameDecoder NewWebsocketDecoder() =>  new WebSocket07FrameDecoder(
            true, this.allowExtensions, this.MaxFramePayloadLength, this.allowMaskMismatch);

        protected internal override IWebSocketFrameEncoder NewWebSocketEncoder() => new WebSocket07FrameEncoder(false);
    }
}
