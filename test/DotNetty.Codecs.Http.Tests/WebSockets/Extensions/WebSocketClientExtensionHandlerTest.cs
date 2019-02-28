// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets.Extensions
{
    using System.Collections.Generic;
    using DotNetty.Codecs.Http.WebSockets.Extensions;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Moq;
    using Xunit;

    using static WebSocketExtensionTestUtil;

    public sealed class WebSocketClientExtensionHandlerTest
    {
        readonly Mock<IWebSocketClientExtensionHandshaker> mainHandshaker;
        readonly Mock<IWebSocketClientExtensionHandshaker> fallbackHandshaker;
        readonly Mock<IWebSocketClientExtension> mainExtension;
        readonly Mock<IWebSocketClientExtension> fallbackExtension;

        public WebSocketClientExtensionHandlerTest()
        {
            this.mainHandshaker = new Mock<IWebSocketClientExtensionHandshaker>(MockBehavior.Strict);
            this.fallbackHandshaker = new Mock<IWebSocketClientExtensionHandshaker>(MockBehavior.Strict);
            this.mainExtension = new Mock<IWebSocketClientExtension>(MockBehavior.Strict);
            this.fallbackExtension = new Mock<IWebSocketClientExtension>(MockBehavior.Strict);
        }

        [Fact]
        public void MainSuccess()
        {
            this.mainHandshaker.Setup(x => x.NewRequestData())
                .Returns(new WebSocketExtensionData("main", new Dictionary<string, string>()));
            this.mainHandshaker.Setup(x => x.HandshakeExtension(It.IsAny<WebSocketExtensionData>()))
                .Returns(this.mainExtension.Object);

            this.fallbackHandshaker.Setup(x => x.NewRequestData())
                .Returns(new WebSocketExtensionData("fallback", new Dictionary<string, string>()));

            this.mainExtension.Setup(x => x.Rsv).Returns(WebSocketRsv.Rsv1);
            this.mainExtension.Setup(x => x.NewExtensionEncoder()).Returns(new DummyEncoder());
            this.mainExtension.Setup(x => x.NewExtensionDecoder()).Returns(new DummyDecoder());

            var ch = new EmbeddedChannel(
                new WebSocketClientExtensionHandler(
                    this.mainHandshaker.Object,
                    this.fallbackHandshaker.Object));

            IHttpRequest req = NewUpgradeRequest(null);
            ch.WriteOutbound(req);

            var req2 = ch.ReadOutbound<IHttpRequest>();
            Assert.True(req2.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value));
            List<WebSocketExtensionData> reqExts = WebSocketExtensionUtil.ExtractExtensions(value.ToString());

            IHttpResponse res = NewUpgradeResponse("main");
            ch.WriteInbound(res);

            var res2 = ch.ReadInbound<IHttpResponse>();
            Assert.True(res2.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out value));
            List<WebSocketExtensionData> resExts = WebSocketExtensionUtil.ExtractExtensions(value.ToString());

            Assert.Equal(2, reqExts.Count);
            Assert.Equal("main", reqExts[0].Name);
            Assert.Equal("fallback", reqExts[1].Name);

            Assert.Single(resExts);
            Assert.Equal("main", resExts[0].Name);
            Assert.Empty(resExts[0].Parameters);
            Assert.NotNull(ch.Pipeline.Get<DummyDecoder>());
            Assert.NotNull(ch.Pipeline.Get<DummyEncoder>());

            this.mainExtension.Verify(x => x.Rsv, Times.AtLeastOnce);
        }

        [Fact]
        public void FallbackSuccess()
        {
            this.mainHandshaker.Setup(x => x.NewRequestData())
                .Returns(new WebSocketExtensionData("main", new Dictionary<string, string>()));
            this.mainHandshaker.Setup(x => x.HandshakeExtension(It.IsAny<WebSocketExtensionData>()))
                .Returns(default(IWebSocketClientExtension));

            this.fallbackHandshaker.Setup(x => x.NewRequestData())
                .Returns(new WebSocketExtensionData("fallback", new Dictionary<string, string>()));
            this.fallbackHandshaker.Setup(x => x.HandshakeExtension(It.IsAny<WebSocketExtensionData>()))
                .Returns(this.fallbackExtension.Object);

            this.fallbackExtension.Setup(x => x.Rsv).Returns(WebSocketRsv.Rsv1);
            this.fallbackExtension.Setup(x => x.NewExtensionEncoder()).Returns(new DummyEncoder());
            this.fallbackExtension.Setup(x => x.NewExtensionDecoder()).Returns(new DummyDecoder());

            var ch = new EmbeddedChannel(
                new WebSocketClientExtensionHandler(
                    this.mainHandshaker.Object,
                    this.fallbackHandshaker.Object));

            IHttpRequest req = NewUpgradeRequest(null);
            ch.WriteOutbound(req);

            var req2 = ch.ReadOutbound<IHttpRequest>();
            Assert.True(req2.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value));
            List<WebSocketExtensionData> reqExts = WebSocketExtensionUtil.ExtractExtensions(value.ToString());

            IHttpResponse res = NewUpgradeResponse("fallback");
            ch.WriteInbound(res);

            var res2 = ch.ReadInbound<IHttpResponse>();
            Assert.True(res2.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out value));
            List<WebSocketExtensionData> resExts = WebSocketExtensionUtil.ExtractExtensions(value.ToString());

            Assert.Equal(2, reqExts.Count);
            Assert.Equal("main", reqExts[0].Name);
            Assert.Equal("fallback", reqExts[1].Name);

            Assert.Single(resExts);
            Assert.Equal("fallback", resExts[0].Name);
            Assert.Empty(resExts[0].Parameters);
            Assert.NotNull(ch.Pipeline.Get<DummyDecoder>());
            Assert.NotNull(ch.Pipeline.Get<DummyEncoder>());

            this.fallbackExtension.Verify(x => x.Rsv, Times.AtLeastOnce);
        }

        [Fact]
        public void AllSuccess()
        {
            this.mainHandshaker.Setup(x => x.NewRequestData())
                .Returns(new WebSocketExtensionData("main", new Dictionary<string, string>()));
            this.mainHandshaker.Setup(x => x.HandshakeExtension(
                        It.Is<WebSocketExtensionData>(v => v.Name.Equals("main"))))
                .Returns(this.mainExtension.Object);
            this.mainHandshaker.Setup(x => x.HandshakeExtension(
                        It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))))
                .Returns(default(IWebSocketClientExtension));
            this.fallbackHandshaker.Setup(x => x.NewRequestData())
                .Returns(new WebSocketExtensionData("fallback", new Dictionary<string, string>()));
            this.fallbackHandshaker.Setup(x => x.HandshakeExtension(
                        It.Is<WebSocketExtensionData>(v => v.Name.Equals("main"))))
                .Returns(default(IWebSocketClientExtension));
            this.fallbackHandshaker.Setup(x => x.HandshakeExtension(
                        It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))))
                .Returns(this.fallbackExtension.Object);

            var mainEncoder = new DummyEncoder();
            var mainDecoder = new DummyDecoder();
            this.mainExtension.Setup(x => x.Rsv).Returns(WebSocketRsv.Rsv1);
            this.mainExtension.Setup(x => x.NewExtensionEncoder()).Returns(mainEncoder);
            this.mainExtension.Setup(x => x.NewExtensionDecoder()).Returns(mainDecoder);

            var fallbackEncoder = new Dummy2Encoder();
            var fallbackDecoder = new Dummy2Decoder();
            this.fallbackExtension.Setup(x => x.Rsv).Returns(WebSocketRsv.Rsv2);
            this.fallbackExtension.Setup(x => x.NewExtensionEncoder()).Returns(fallbackEncoder);
            this.fallbackExtension.Setup(x => x.NewExtensionDecoder()).Returns(fallbackDecoder);

            var ch = new EmbeddedChannel(new WebSocketClientExtensionHandler(
                this.mainHandshaker.Object, this.fallbackHandshaker.Object));

            IHttpRequest req = NewUpgradeRequest(null);
            ch.WriteOutbound(req);

            var req2 = ch.ReadOutbound<IHttpRequest>();
            Assert.True(req2.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value));
            List<WebSocketExtensionData> reqExts = WebSocketExtensionUtil.ExtractExtensions(value.ToString());

            IHttpResponse res = NewUpgradeResponse("main, fallback");
            ch.WriteInbound(res);

            var res2 = ch.ReadInbound<IHttpResponse>();
            Assert.True(res2.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out value));
            List<WebSocketExtensionData> resExts = WebSocketExtensionUtil.ExtractExtensions(value.ToString());

            Assert.Equal(2, reqExts.Count);
            Assert.Equal("main", reqExts[0].Name);
            Assert.Equal("fallback", reqExts[1].Name);

            Assert.Equal(2, resExts.Count);
            Assert.Equal("main", resExts[0].Name);
            Assert.Equal("fallback", resExts[1].Name);
            Assert.NotNull(ch.Pipeline.Context(mainEncoder));
            Assert.NotNull(ch.Pipeline.Context(mainDecoder));
            Assert.NotNull(ch.Pipeline.Context(fallbackEncoder));
            Assert.NotNull(ch.Pipeline.Context(fallbackDecoder));

            this.mainExtension.Verify(x => x.Rsv, Times.AtLeastOnce);
            this.fallbackExtension.Verify(x => x.Rsv, Times.AtLeastOnce);
        }

        [Fact]
        public void MainAndFallbackUseRsv1WillFail()
        {
            this.mainHandshaker.Setup(x => x.NewRequestData())
                .Returns(new WebSocketExtensionData("main", new Dictionary<string, string>()));
            this.mainHandshaker.Setup(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("main"))))
                .Returns(this.mainExtension.Object);
            this.mainHandshaker.Setup(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))))
                .Returns(default(IWebSocketClientExtension));
            this.fallbackHandshaker.Setup(x => x.NewRequestData())
                .Returns(new WebSocketExtensionData("fallback", new Dictionary<string, string>()));
            this.fallbackHandshaker.Setup(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))))
                .Returns(this.fallbackExtension.Object);
            this.mainExtension.Setup(x => x.Rsv).Returns(WebSocketRsv.Rsv1);
            this.fallbackExtension.Setup(x => x.Rsv).Returns(WebSocketRsv.Rsv1);

            var ch = new EmbeddedChannel(new WebSocketClientExtensionHandler(
                this.mainHandshaker.Object, this.fallbackHandshaker.Object));

            IHttpRequest req = NewUpgradeRequest(null);
            ch.WriteOutbound(req);

            var req2 = ch.ReadOutbound<IHttpRequest>();
            Assert.True(req2.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value));
            List<WebSocketExtensionData> reqExts = WebSocketExtensionUtil.ExtractExtensions(value.ToString());

            IHttpResponse res = NewUpgradeResponse("main, fallback");
            Assert.Throws<CodecException>(() => ch.WriteInbound(res));

            Assert.Equal(2, reqExts.Count);
            Assert.Equal("main", reqExts[0].Name);
            Assert.Equal("fallback", reqExts[1].Name);

            this.mainHandshaker.Verify(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("main"))), Times.AtLeastOnce);
            this.mainHandshaker.Verify(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))), Times.AtLeastOnce);

            this.fallbackHandshaker.Verify(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))), Times.AtLeastOnce);

            this.mainExtension.Verify(x => x.Rsv, Times.AtLeastOnce);
            this.fallbackExtension.Verify(x => x.Rsv, Times.AtLeastOnce);
        }
    }
}
