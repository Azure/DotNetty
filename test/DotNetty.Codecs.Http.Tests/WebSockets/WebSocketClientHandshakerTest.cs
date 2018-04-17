// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public abstract class WebSocketClientHandshakerTest
    {
        protected abstract WebSocketClientHandshaker NewHandshaker(Uri uri);

        protected abstract AsciiString GetOriginHeaderName();

        [Fact]
        public void HostHeaderWs()
        {
            foreach (string scheme in new[] { "ws://", "http://" })
            {
                foreach (string host in new[] { /*"localhost", "127.0.0.1", "[::1]",*/ "Netty.io" })
                {
                    string enter = scheme + host;

                    this.HostHeader(enter, host);
                    this.HostHeader(enter + '/', host);
                    this.HostHeader(enter + ":80", host);
                    this.HostHeader(enter + ":443", host + ":443");
                    this.HostHeader(enter + ":9999", host + ":9999");
                    this.HostHeader(enter + "/path", host);
                    this.HostHeader(enter + ":80/path", host);
                    this.HostHeader(enter + ":443/path", host + ":443");
                    this.HostHeader(enter + ":9999/path", host + ":9999");
                }
            }
        }

        [Fact]
        public void HostHeaderWss()
        {
            foreach (string scheme in new[] { "wss://", "https://" })
            {
                foreach (string host in new[] { "localhost", "127.0.0.1", "[::1]", "Netty.io" })
                {
                    string enter = scheme + host;

                    this.HostHeader(enter, host);
                    this.HostHeader(enter + '/', host);
                    this.HostHeader(enter + ":80", host + ":80");
                    this.HostHeader(enter + ":443", host);
                    this.HostHeader(enter + ":9999", host + ":9999");
                    this.HostHeader(enter + "/path", host);
                    this.HostHeader(enter + ":80/path", host + ":80");
                    this.HostHeader(enter + ":443/path", host);
                    this.HostHeader(enter + ":9999/path", host + ":9999");
                }
            }
        }

        [Fact]
        public void HostHeaderWithoutScheme()
        {
            this.HostHeader("//localhost/", "localhost");
            this.HostHeader("//localhost/path", "localhost");
            this.HostHeader("//localhost:80/", "localhost:80");
            this.HostHeader("//localhost:443/", "localhost:443");
            this.HostHeader("//localhost:9999/", "localhost:9999");
        }

        [Fact]
        public void OriginHeaderWs()
        {
            foreach (string scheme in new[] { "ws://", "http://" })
            {
                foreach (string host in new[] { "localhost", "127.0.0.1", "[::1]", "NETTY.IO" })
                {
                    string enter = scheme + host;
                    string expect = "http://" + host.ToLower();

                    this.OriginHeader(enter, expect);
                    this.OriginHeader(enter + '/', expect);
                    this.OriginHeader(enter + ":80", expect);
                    this.OriginHeader(enter + ":443", expect + ":443");
                    this.OriginHeader(enter + ":9999", expect + ":9999");
                    this.OriginHeader(enter + "/path%20with%20ws", expect);
                    this.OriginHeader(enter + ":80/path%20with%20ws", expect);
                    this.OriginHeader(enter + ":443/path%20with%20ws", expect + ":443");
                    this.OriginHeader(enter + ":9999/path%20with%20ws", expect + ":9999");
                }
            }
        }

        [Fact]
        public void OriginHeaderWss()
        {
            foreach (string scheme in new[] { "wss://", "https://" })
            {
                foreach (string host in new[] { "localhost", "127.0.0.1", "[::1]", "NETTY.IO" })
                {
                    string enter = scheme + host;
                    string expect = "https://" + host.ToLower();

                    this.OriginHeader(enter, expect);
                    this.OriginHeader(enter + '/', expect);
                    this.OriginHeader(enter + ":80", expect + ":80");
                    this.OriginHeader(enter + ":443", expect);
                    this.OriginHeader(enter + ":9999", expect + ":9999");
                    this.OriginHeader(enter + "/path%20with%20ws", expect);
                    this.OriginHeader(enter + ":80/path%20with%20ws", expect + ":80");
                    this.OriginHeader(enter + ":443/path%20with%20ws", expect);
                    this.OriginHeader(enter + ":9999/path%20with%20ws", expect + ":9999");
                }
            }
        }

        [Fact]
        public void OriginHeaderWithoutScheme()
        {
            this.OriginHeader("//localhost/", "http://localhost");
            this.OriginHeader("//localhost/path", "http://localhost");

            // http scheme by port
            this.OriginHeader("//localhost:80/", "http://localhost");
            this.OriginHeader("//localhost:80/path", "http://localhost");

            // https scheme by port
            this.OriginHeader("//localhost:443/", "https://localhost");
            this.OriginHeader("//localhost:443/path", "https://localhost");

            // http scheme for non standard port
            this.OriginHeader("//localhost:9999/", "http://localhost:9999");
            this.OriginHeader("//localhost:9999/path", "http://localhost:9999");

            // convert host to lower case
            this.OriginHeader("//LOCALHOST/", "http://localhost");
        }

        void HostHeader(string uri, string expected) =>
            this.HeaderDefaultHttp(uri, HttpHeaderNames.Host, expected);

        void OriginHeader(string uri, string expected) =>
            this.HeaderDefaultHttp(uri, this.GetOriginHeaderName(), expected);

        protected void HeaderDefaultHttp(string uri, AsciiString header, string expectedValue)
        {
            Assert.True(Uri.TryCreate(uri, UriKind.RelativeOrAbsolute, out Uri originalUri));
            WebSocketClientHandshaker handshaker = this.NewHandshaker(originalUri);
            IFullHttpRequest request = handshaker.NewHandshakeRequest();
            try
            {
                Assert.True(request.Headers.TryGet(header, out ICharSequence value));
                Assert.Equal(expectedValue, value.ToString(), true);
            }
            finally
            {
                request.Release();
            }
        }

        [Fact]
        public void RawPath()
        {
            var uri = new Uri("ws://localhost:9999/path%20with%20ws");
            WebSocketClientHandshaker handshaker = this.NewHandshaker(uri);
            IFullHttpRequest request = handshaker.NewHandshakeRequest();
            try
            {
                Assert.Equal("/path%20with%20ws", request.Uri);
            }
            finally
            {
                request.Release();
            }
        }

        [Fact]
        public void RawPathWithQuery()
        {
            var uri = new Uri("ws://localhost:9999/path%20with%20ws?a=b%20c");
            WebSocketClientHandshaker handshaker = this.NewHandshaker(uri);
            IFullHttpRequest request = handshaker.NewHandshakeRequest();
            try
            {
                Assert.Equal("/path%20with%20ws?a=b%20c", request.Uri);
            }
            finally
            {
                request.Release();
            }
        }

        [Fact]
        public void HttpResponseAndFrameInSameBuffer() => this.TestHttpResponseAndFrameInSameBuffer(false);

        [Fact]
        public void HttpResponseAndFrameInSameBufferCodec() => this.TestHttpResponseAndFrameInSameBuffer(true);

        void TestHttpResponseAndFrameInSameBuffer(bool codec)
        {
            string url = "ws://localhost:9999/ws";
            WebSocketClientHandshaker shaker = this.NewHandshaker(new Uri(url));
            var handshaker = new Handshaker(shaker);

            var data = new byte[24];
            PlatformDependent.GetThreadLocalRandom().NextBytes(data);

            // Create a EmbeddedChannel which we will use to encode a BinaryWebsocketFrame to bytes and so use these
            // to test the actual handshaker.
            var factory = new WebSocketServerHandshakerFactory(url, null, false);
            WebSocketServerHandshaker socketServerHandshaker = factory.NewHandshaker(shaker.NewHandshakeRequest());
            var websocketChannel = new EmbeddedChannel(socketServerHandshaker.NewWebSocketEncoder(),
                socketServerHandshaker.NewWebsocketDecoder());
            Assert.True(websocketChannel.WriteOutbound(new BinaryWebSocketFrame(Unpooled.WrappedBuffer(data))));

            byte[] bytes = Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nContent-Length: 0\r\n\r\n");

            CompositeByteBuffer compositeByteBuf = Unpooled.CompositeBuffer();
            compositeByteBuf.AddComponent(true, Unpooled.WrappedBuffer(bytes));
            for (;;)
            {
                var frameBytes = websocketChannel.ReadOutbound<IByteBuffer>();
                if (frameBytes == null)
                {
                    break;
                }
                compositeByteBuf.AddComponent(true, frameBytes);
            }

            var ch = new EmbeddedChannel(new HttpObjectAggregator(int.MaxValue), new Handler(handshaker));
            if (codec)
            {
                ch.Pipeline.AddFirst(new HttpClientCodec());
            }
            else
            {
                ch.Pipeline.AddFirst(new HttpRequestEncoder(), new HttpResponseDecoder());
            }

            // We need to first write the request as HttpClientCodec will fail if we receive a response before a request
            // was written.
            shaker.HandshakeAsync(ch).Wait();
            for (;;)
            {
                // Just consume the bytes, we are not interested in these.
                var buf = ch.ReadOutbound<IByteBuffer>();
                if (buf == null)
                {
                    break;
                }
                buf.Release();
            }
            Assert.True(ch.WriteInbound(compositeByteBuf));
            Assert.True(ch.Finish());

            var frame = ch.ReadInbound<BinaryWebSocketFrame>();
            IByteBuffer expect = Unpooled.WrappedBuffer(data);
            try
            {
                Assert.Equal(expect, frame.Content);
                Assert.True(frame.IsFinalFragment);
                Assert.Equal(0, frame.Rsv);
            }
            finally
            {
                expect.Release();
                frame.Release();
            }
        }

        sealed class Handshaker : WebSocketClientHandshaker
        {
            readonly WebSocketClientHandshaker shaker;

            public Handshaker(WebSocketClientHandshaker shaker)
                : base(shaker.Uri, shaker.Version, null, EmptyHttpHeaders.Default, int.MaxValue)
            {
                this.shaker = shaker;
            }

            protected internal override IFullHttpRequest NewHandshakeRequest() => this.shaker.NewHandshakeRequest();

            protected override void Verify(IFullHttpResponse response)
            {
                // Not do any verification, so we do not need to care sending the correct headers etc in the test,
                // which would just make things more complicated.
            }

            protected internal override IWebSocketFrameDecoder NewWebSocketDecoder() => this.shaker.NewWebSocketDecoder();

            protected internal override IWebSocketFrameEncoder NewWebSocketEncoder() => this.shaker.NewWebSocketEncoder();
        }

        sealed class Handler : SimpleChannelInboundHandler<IFullHttpResponse>
        {
            readonly Handshaker handshaker;

            public Handler(Handshaker handshaker)
            {
                this.handshaker = handshaker;
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, IFullHttpResponse msg)
            {
                this.handshaker.FinishHandshake(ctx.Channel, msg);
                ctx.Channel.Pipeline.Remove(this);
            }
        }
    }
}
