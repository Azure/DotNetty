// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    using static HttpResponseStatus;
    using static HttpVersion;

    public class WebSocketServerProtocolHandlerTest : IDisposable
    {
        readonly Queue<IFullHttpResponse> responses;

        public WebSocketServerProtocolHandlerTest()
        {
            this.responses = new Queue<IFullHttpResponse>();
        }

        [Fact]
        public void HttpUpgradeRequest()
        {
            EmbeddedChannel ch = this.CreateChannel(new MockOutboundHandler(this));
            IChannelHandlerContext handshakerCtx = ch.Pipeline.Context<WebSocketServerProtocolHandshakeHandler>();
            WriteUpgradeRequest(ch);

            IFullHttpResponse response = this.responses.Dequeue();
            Assert.Equal(SwitchingProtocols, response.Status);
            response.Release();
            Assert.NotNull(WebSocketServerProtocolHandler.GetHandshaker(handshakerCtx.Channel));
        }

        [Fact]
        public void SubsequentHttpRequestsAfterUpgradeShouldReturn403()
        {
            EmbeddedChannel ch = this.CreateChannel();

            WriteUpgradeRequest(ch);

            IFullHttpResponse response = this.responses.Dequeue();
            Assert.Equal(SwitchingProtocols, response.Status);
            response.Release();

            ch.WriteInbound(new DefaultFullHttpRequest(Http11, HttpMethod.Get, "/test"));
            response = this.responses.Dequeue();
            Assert.Equal(Forbidden, response.Status);
            response.Release();
        }

        [Fact]
        public void HttpUpgradeRequestInvalidUpgradeHeader()
        {
            EmbeddedChannel ch = this.CreateChannel();
            IFullHttpRequest httpRequestWithEntity = new WebSocketRequestBuilder()
                .HttpVersion(Http11)
                .Method(HttpMethod.Get)
                .Uri("/test")
                .Connection("Upgrade")
                .Version00()
                .Upgrade("BogusSocket")
                .Build();

            ch.WriteInbound(httpRequestWithEntity);

            IFullHttpResponse response = this.responses.Dequeue();
            Assert.Equal(BadRequest, response.Status);
            Assert.Equal("not a WebSocket handshake request: missing upgrade", GetResponseMessage(response));
            response.Release();
        }

        [Fact]
        public void HttpUpgradeRequestMissingWsKeyHeader()
        {
            EmbeddedChannel ch = this.CreateChannel();
            IHttpRequest httpRequest = new WebSocketRequestBuilder()
                .HttpVersion(Http11)
                .Method(HttpMethod.Get)
                .Uri("/test")
                .Key(null)
                .Connection("Upgrade")
                .Upgrade(HttpHeaderValues.Websocket)
                .Version13()
                .Build();

            ch.WriteInbound(httpRequest);

            IFullHttpResponse response = this.responses.Dequeue();
            Assert.Equal(BadRequest, response.Status);
            Assert.Equal("not a WebSocket request: missing key", GetResponseMessage(response));
            response.Release();
        }

        [Fact]
        public void HandleTextFrame()
        {
            var customTextFrameHandler = new CustomTextFrameHandler();
            EmbeddedChannel ch = this.CreateChannel(customTextFrameHandler);
            WriteUpgradeRequest(ch);

            if (ch.Pipeline.Context<HttpRequestDecoder>() != null)
            {
                // Removing the HttpRequestDecoder because we are writing a TextWebSocketFrame and thus
                // decoding is not necessary.
                ch.Pipeline.Remove<HttpRequestDecoder>();
            }

            ch.WriteInbound(new TextWebSocketFrame("payload"));

            Assert.Equal("processed: payload", customTextFrameHandler.Content);
        }

        EmbeddedChannel CreateChannel() =>  this.CreateChannel(null);

        EmbeddedChannel CreateChannel(IChannelHandler handler) =>
            new EmbeddedChannel(
                new WebSocketServerProtocolHandler("/test", null, false),
                new HttpRequestDecoder(),
                new HttpResponseEncoder(),
                new MockOutboundHandler(this),
                handler);

        static void WriteUpgradeRequest(EmbeddedChannel ch) => ch.WriteInbound(WebSocketRequestBuilder.Successful());

        static string GetResponseMessage(IFullHttpResponse response) => Encoding.ASCII.GetString(response.Content.Array);

        sealed class MockOutboundHandler : ChannelHandlerAdapter
        {
            readonly WebSocketServerProtocolHandlerTest owner;

            public MockOutboundHandler(WebSocketServerProtocolHandlerTest owner)
            {
                this.owner = owner;
            }

            public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                this.owner.responses.Enqueue((IFullHttpResponse)msg);
                return TaskEx.Completed;
            }

            public override void Flush(IChannelHandlerContext ctx)
            {
            }
        }

        sealed class CustomTextFrameHandler : ChannelHandlerAdapter
        {
            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                Assert.Null(this.Content);
                this.Content = "processed: " + ((TextWebSocketFrame)msg).Text();
                ReferenceCountUtil.Release(msg);
            }

            public string Content { get; private set; }
        }

        public void Dispose() => this.responses.Clear();
    }
}
