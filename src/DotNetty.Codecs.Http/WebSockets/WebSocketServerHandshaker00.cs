// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System.Diagnostics;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class WebSocketServerHandshaker00 : WebSocketServerHandshaker
    {
        static readonly Regex BeginningDigit = new Regex("[^0-9]", RegexOptions.Compiled);
        static readonly Regex BeginningSpace = new Regex("[^ ]", RegexOptions.Compiled);

        public WebSocketServerHandshaker00(string webSocketUrl, string subprotocols, int maxFramePayloadLength)
            : base(WebSocketVersion.V00, webSocketUrl, subprotocols, maxFramePayloadLength)
        {
        }

        protected override IFullHttpResponse NewHandshakeResponse(IFullHttpRequest req, HttpHeaders headers)
        {
            // Serve the WebSocket handshake request.
            if (!req.Headers.ContainsValue(HttpHeaderNames.Connection, HttpHeaderValues.Upgrade, true)
                || !req.Headers.TryGet(HttpHeaderNames.Upgrade, out ICharSequence value)
                || !HttpHeaderValues.Websocket.ContentEqualsIgnoreCase(value))
            {
                throw new WebSocketHandshakeException("not a WebSocket handshake request: missing upgrade");
            }

            // Hixie 75 does not contain these headers while Hixie 76 does
            bool isHixie76 = req.Headers.Contains(HttpHeaderNames.SecWebsocketKey1)
                && req.Headers.Contains(HttpHeaderNames.SecWebsocketKey2);

            // Create the WebSocket handshake response.
            var res = new DefaultFullHttpResponse(HttpVersion.Http11,
                new HttpResponseStatus(101, new AsciiString(isHixie76 ? "WebSocket Protocol Handshake" : "Web Socket Protocol Handshake")));
            if (headers != null)
            {
                res.Headers.Add(headers);
            }

            res.Headers.Add(HttpHeaderNames.Upgrade, HttpHeaderValues.Websocket);
            res.Headers.Add(HttpHeaderNames.Connection, HttpHeaderValues.Upgrade);

            // Fill in the headers and contents depending on handshake getMethod.
            if (isHixie76)
            {
                // New handshake getMethod with a challenge:
                value = req.Headers.Get(HttpHeaderNames.Origin, null);
                Debug.Assert(value != null);
                res.Headers.Add(HttpHeaderNames.SecWebsocketOrigin, value);
                res.Headers.Add(HttpHeaderNames.SecWebsocketLocation, this.Uri);

                if (req.Headers.TryGet(HttpHeaderNames.SecWebsocketProtocol, out ICharSequence subprotocols))
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

                // Calculate the answer of the challenge.
                value = req.Headers.Get(HttpHeaderNames.SecWebsocketKey1, null);
                Debug.Assert(value != null, $"{HttpHeaderNames.SecWebsocketKey1} must exist");
                string key1 = value.ToString();
                value = req.Headers.Get(HttpHeaderNames.SecWebsocketKey2, null);
                Debug.Assert(value != null, $"{HttpHeaderNames.SecWebsocketKey2} must exist");
                string key2 = value.ToString();
                int a = (int)(long.Parse(BeginningDigit.Replace(key1, "")) /
                    BeginningSpace.Replace(key1, "").Length);
                int b = (int)(long.Parse(BeginningDigit.Replace(key2, "")) /
                    BeginningSpace.Replace(key2, "").Length);
                long c = req.Content.ReadLong();
                IByteBuffer input = Unpooled.Buffer(16);
                input.WriteInt(a);
                input.WriteInt(b);
                input.WriteLong(c);
                res.Content.WriteBytes(WebSocketUtil.Md5(input.Array));
            }
            else
            {
                // Old Hixie 75 handshake getMethod with no challenge:
                value = req.Headers.Get(HttpHeaderNames.Origin, null);
                Debug.Assert(value != null);
                res.Headers.Add(HttpHeaderNames.WebsocketOrigin, value);
                res.Headers.Add(HttpHeaderNames.WebsocketLocation, this.Uri);

                if (req.Headers.TryGet(HttpHeaderNames.WebsocketProtocol, out ICharSequence protocol))
                {
                    res.Headers.Add(HttpHeaderNames.WebsocketProtocol, this.SelectSubprotocol(protocol.ToString()));
                }
            }

            return res;
        }

        public override Task CloseAsync(IChannel channel, CloseWebSocketFrame frame) => channel.WriteAndFlushAsync(frame);

        protected internal override IWebSocketFrameDecoder NewWebsocketDecoder() => new WebSocket00FrameDecoder(this.MaxFramePayloadLength);

        protected internal override IWebSocketFrameEncoder NewWebSocketEncoder() => new WebSocket00FrameEncoder();
    }
}
