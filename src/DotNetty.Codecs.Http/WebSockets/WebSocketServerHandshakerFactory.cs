// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System.Threading.Tasks;
    using DotNetty.Codecs.Http.WebSockets.Handshaker;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class WebSocketServerHandshakerFactory
    {
        readonly string webSocketUrl;

        readonly string subprotocols;

        readonly bool allowExtensions;

        readonly int maxFramePayloadLength;

        readonly bool allowMaskMismatch;

        readonly WebsocketHandshakerVersionSelector[] selectors;

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

            this.selectors = new WebsocketHandshakerVersionSelector[]
            {
                new WebsocketHandshaker00Selector(
                    this.subprotocols,
                    this.subprotocols,
                    this.allowExtensions,
                    this.maxFramePayloadLength,
                    this.allowMaskMismatch),
                new WebsocketHandshaker07Selector(
                    this.subprotocols,
                    this.subprotocols,
                    this.allowExtensions,
                    this.maxFramePayloadLength,
                    this.allowMaskMismatch),
                new WebsocketHandshaker08Selector(
                    this.subprotocols,
                    this.subprotocols,
                    this.allowExtensions,
                    this.maxFramePayloadLength,
                    this.allowMaskMismatch),
                new WebsocketHandshaker13Selector(
                    this.subprotocols,
                    this.subprotocols,
                    this.allowExtensions,
                    this.maxFramePayloadLength,
                    this.allowMaskMismatch)
            };
        }

        public WebSocketServerHandshaker NewHandshaker(IHttpRequest req)
        {
            req.Headers.TryGet(HttpHeaderNames.SecWebsocketVersion, out ICharSequence version);

            WebSocketServerHandshaker result = null;
            foreach (WebsocketHandshakerVersionSelector selector in this.selectors)
            {
                if (selector.Selector(version, out result))
                {
                    break;
                }
            }

            return result;
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
