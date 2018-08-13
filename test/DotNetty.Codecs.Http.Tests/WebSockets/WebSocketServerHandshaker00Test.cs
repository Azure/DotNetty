// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    using static HttpVersion;

    public class WebSocketServerHandshaker00Test
    {
        [Fact]
        public void PerformOpeningHandshake() => PerformOpeningHandshake0(true);

        [Fact]
        public void PerformOpeningHandshakeSubProtocolNotSupported() => PerformOpeningHandshake0(false);

        static void PerformOpeningHandshake0(bool subProtocol)
        {
            var ch = new EmbeddedChannel(
                new HttpObjectAggregator(42), new HttpRequestDecoder(), new HttpResponseEncoder());

            var req = new DefaultFullHttpRequest(Http11, HttpMethod.Get, "/chat", 
                Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("^n:ds[4U")));

            req.Headers.Set(HttpHeaderNames.Host, "server.example.com");
            req.Headers.Set(HttpHeaderNames.Upgrade, HttpHeaderValues.Websocket);
            req.Headers.Set(HttpHeaderNames.Connection, "Upgrade");
            req.Headers.Set(HttpHeaderNames.Origin, "http://example.com");
            req.Headers.Set(HttpHeaderNames.SecWebsocketKey1, "4 @1  46546xW%0l 1 5");
            req.Headers.Set(HttpHeaderNames.SecWebsocketKey2, "12998 5 Y3 1  .P00");
            req.Headers.Set(HttpHeaderNames.SecWebsocketProtocol, "chat, superchat");

            WebSocketServerHandshaker00 handshaker;
            if (subProtocol)
            {
                handshaker = new WebSocketServerHandshaker00("ws://example.com/chat", "chat", int.MaxValue);
            }
            else
            {
                handshaker = new WebSocketServerHandshaker00("ws://example.com/chat", null, int.MaxValue);
            }
            Assert.True(handshaker.HandshakeAsync(ch, req).Wait(TimeSpan.FromSeconds(2)));

            var ch2 = new EmbeddedChannel(new HttpResponseDecoder());
            ch2.WriteInbound(ch.ReadOutbound<IByteBuffer>());
            var res = ch2.ReadInbound<IHttpResponse>();

            Assert.True(res.Headers.TryGet(HttpHeaderNames.SecWebsocketLocation, out ICharSequence value));
            Assert.Equal("ws://example.com/chat", value.ToString());

            if (subProtocol)
            {
                Assert.True(res.Headers.TryGet(HttpHeaderNames.SecWebsocketProtocol, out value));
                Assert.Equal("chat", value.ToString());
            }
            else
            {
                Assert.False(res.Headers.TryGet(HttpHeaderNames.SecWebsocketProtocol, out value));
            }
            var content = ch2.ReadInbound<ILastHttpContent>();

            Assert.Equal("8jKS'y:G*Co,Wxa-", content.Content.ToString(Encoding.ASCII));
            content.Release();
            req.Release();
        }
    }
}
