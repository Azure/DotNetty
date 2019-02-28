// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class WebSocketServerHandshakerFactory
    {
        readonly string webSocketUrl;

        readonly string subprotocols;

        readonly bool allowExtensions;

        readonly int maxFramePayloadLength;

        readonly bool allowMaskMismatch;

        public WebSocketServerHandshakerFactory(string webSocketUrl, string subprotocols, bool allowExtensions)
            : this(webSocketUrl, subprotocols, allowExtensions, 65536)
        {
        }

        public WebSocketServerHandshakerFactory(string webSocketUrl, string subprotocols, bool allowExtensions,
            int maxFramePayloadLength)
            : this(webSocketUrl, subprotocols, allowExtensions, maxFramePayloadLength, false)
        {
        }

        public WebSocketServerHandshakerFactory(string webSocketUrl, string subprotocols, bool allowExtensions,
            int maxFramePayloadLength, bool allowMaskMismatch)
        {
            this.webSocketUrl = webSocketUrl;
            this.subprotocols = subprotocols;
            this.allowExtensions = allowExtensions;
            this.maxFramePayloadLength = maxFramePayloadLength;
            this.allowMaskMismatch = allowMaskMismatch;
        }

        public WebSocketServerHandshaker NewHandshaker(IHttpRequest req)
        {
            if (req.Headers.TryGet(HttpHeaderNames.SecWebsocketVersion, out ICharSequence version)
                && version != null)
            {
                if (version.Equals(WebSocketVersion.V13.ToHttpHeaderValue()))
                {
                    // Version 13 of the wire protocol - RFC 6455 (version 17 of the draft hybi specification).
                    return new WebSocketServerHandshaker13(
                        this.webSocketUrl,
                        this.subprotocols,
                        this.allowExtensions,
                        this.maxFramePayloadLength,
                        this.allowMaskMismatch);
                }
                else if (version.Equals(WebSocketVersion.V08.ToHttpHeaderValue()))
                {
                    // Version 8 of the wire protocol - version 10 of the draft hybi specification.
                    return new WebSocketServerHandshaker08(
                        this.webSocketUrl,
                        this.subprotocols,
                        this.allowExtensions,
                        this.maxFramePayloadLength,
                        this.allowMaskMismatch);
                }
                else if (version.Equals(WebSocketVersion.V07.ToHttpHeaderValue()))
                {
                    // Version 8 of the wire protocol - version 07 of the draft hybi specification.
                    return new WebSocketServerHandshaker07(
                        this.webSocketUrl,
                        this.subprotocols,
                        this.allowExtensions,
                        this.maxFramePayloadLength,
                        this.allowMaskMismatch);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                // Assume version 00 where version header was not specified
                return new WebSocketServerHandshaker00(this.webSocketUrl, this.subprotocols, this.maxFramePayloadLength);
            }
        }

        public static Task SendUnsupportedVersionResponse(IChannel channel)
        {
            var res = new DefaultFullHttpResponse(
                HttpVersion.Http11,
                HttpResponseStatus.UpgradeRequired);
            res.Headers.Set(HttpHeaderNames.SecWebsocketVersion, WebSocketVersion.V13.ToHttpHeaderValue());
            HttpUtil.SetContentLength(res, 0);
            return channel.WriteAndFlushAsync(res);
        }
    }
}
