// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    using static HttpVersion;

    public class WebSocketServerHandshaker13Test
    {
        [Fact]
        public void PerformOpeningHandshake() => PerformOpeningHandshake0(true);

        [Fact]
        public void PerformOpeningHandshakeSubProtocolNotSupported() => PerformOpeningHandshake0(false);

        static void PerformOpeningHandshake0(bool subProtocol)
        {
            var ch = new EmbeddedChannel(
                new HttpObjectAggregator(42), new HttpRequestDecoder(), new HttpResponseEncoder());

            var req = new DefaultFullHttpRequest(Http11, HttpMethod.Get, "/chat");
            req.Headers.Set(HttpHeaderNames.Host, "server.example.com");
            req.Headers.Set(HttpHeaderNames.Upgrade, HttpHeaderValues.Websocket);
            req.Headers.Set(HttpHeaderNames.Connection, "Upgrade");
            req.Headers.Set(HttpHeaderNames.SecWebsocketKey, "dGhlIHNhbXBsZSBub25jZQ==");
            req.Headers.Set(HttpHeaderNames.SecWebsocketOrigin, "http://example.com");
            req.Headers.Set(HttpHeaderNames.SecWebsocketProtocol, "chat, superchat");
            req.Headers.Set(HttpHeaderNames.SecWebsocketVersion, "13");

            WebSocketServerHandshaker13 handshaker;
            if (subProtocol)
            {
                handshaker = new WebSocketServerHandshaker13(
                    "ws://example.com/chat", "chat", false, int.MaxValue, false);
            }
            else
            {
                handshaker = new WebSocketServerHandshaker13(
                    "ws://example.com/chat", null, false, int.MaxValue, false);
            }

            Assert.True(handshaker.HandshakeAsync(ch, req).Wait(TimeSpan.FromSeconds(2)));

            var resBuf = ch.ReadOutbound<IByteBuffer>();

            var ch2 = new EmbeddedChannel(new HttpResponseDecoder());
            ch2.WriteInbound(resBuf);
            var res = ch2.ReadInbound<IHttpResponse>();

            Assert.True(res.Headers.TryGet(HttpHeaderNames.SecWebsocketAccept, out ICharSequence value));
            Assert.Equal("s3pPLMBiTxaQ9kYGzzhZRbK+xOo=", value.ToString());
            if (subProtocol)
            {
                Assert.True(res.Headers.TryGet(HttpHeaderNames.SecWebsocketProtocol, out value));
                Assert.Equal("chat", value.ToString());
            }
            else
            {
                Assert.False(res.Headers.TryGet(HttpHeaderNames.SecWebsocketProtocol, out value));
            }
            ReferenceCountUtil.Release(res);
            req.Release();
        }
    }
}
