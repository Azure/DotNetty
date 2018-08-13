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

    public sealed class WebSocketServerExtensionHandlerTest
    {
        readonly Mock<IWebSocketServerExtensionHandshaker> mainHandshaker;
        readonly Mock<IWebSocketServerExtensionHandshaker> fallbackHandshaker;
        readonly Mock<IWebSocketServerExtension> mainExtension;
        readonly Mock<IWebSocketServerExtension> fallbackExtension;

        public WebSocketServerExtensionHandlerTest()
        {
            this.mainHandshaker = new Mock<IWebSocketServerExtensionHandshaker>();
            this.fallbackHandshaker = new Mock<IWebSocketServerExtensionHandshaker>();
            this.mainExtension = new Mock<IWebSocketServerExtension>();
            this.fallbackExtension = new Mock<IWebSocketServerExtension>();
        }

        [Fact]
        public void MainSuccess()
        {
            this.mainHandshaker.Setup(
                    x => x.HandshakeExtension(
                        It.Is<WebSocketExtensionData>(v => v.Name.Equals("main"))))
                .Returns(this.mainExtension.Object);
            this.mainHandshaker.Setup(
                    x => x.HandshakeExtension(
                        It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))))
                .Returns(default(IWebSocketServerExtension));

            this.fallbackHandshaker.Setup(
                    x => x.HandshakeExtension(
                        It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))))
                .Returns(this.fallbackExtension.Object);
            this.fallbackHandshaker.Setup(
                    x => x.HandshakeExtension(
                        It.Is<WebSocketExtensionData>(v => v.Name.Equals("main"))))
                .Returns(default(IWebSocketServerExtension));

            this.mainExtension.Setup(x => x.Rsv).Returns(WebSocketRsv.Rsv1);
            this.mainExtension.Setup(x => x.NewReponseData()).Returns(
                new WebSocketExtensionData("main", new Dictionary<string, string>()));
            this.mainExtension.Setup(x => x.NewExtensionEncoder()).Returns(new DummyEncoder());
            this.mainExtension.Setup(x => x.NewExtensionDecoder()).Returns(new DummyDecoder());

            this.fallbackExtension.Setup(x => x.Rsv).Returns(WebSocketRsv.Rsv1);

            var ch = new EmbeddedChannel(
                new WebSocketServerExtensionHandler(
                    this.mainHandshaker.Object,
                    this.fallbackHandshaker.Object));

            IHttpRequest req = NewUpgradeRequest("main, fallback");
            ch.WriteInbound(req);

            IHttpResponse res = NewUpgradeResponse(null);
            ch.WriteOutbound(res);

            var res2 = ch.ReadOutbound<IHttpResponse>();
            Assert.True(res2.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value));
            List<WebSocketExtensionData> resExts = WebSocketExtensionUtil.ExtractExtensions(value.ToString());

            Assert.Single(resExts);
            Assert.Equal("main", resExts[0].Name);
            Assert.Empty(resExts[0].Parameters);
            Assert.NotNull(ch.Pipeline.Get<DummyDecoder>());
            Assert.NotNull(ch.Pipeline.Get<DummyEncoder>());

            this.mainHandshaker.Verify(
                x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("main"))),
                Times.AtLeastOnce);
            this.mainHandshaker.Verify(
                x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))),
                Times.AtLeastOnce);
            this.fallbackHandshaker.Verify(
                x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))),
                Times.AtLeastOnce);

            this.mainExtension.Verify(x => x.Rsv, Times.AtLeastOnce);
            this.fallbackExtension.Verify(x => x.Rsv, Times.AtLeastOnce);
        }

        [Fact]
        public void CompatibleExtensionTogetherSuccess()
        {
            this.mainHandshaker.Setup(x => x.HandshakeExtension(
                        It.Is<WebSocketExtensionData>(v => v.Name.Equals("main"))))
                .Returns(this.mainExtension.Object);
            this.mainHandshaker.Setup(x => x.HandshakeExtension(
                        It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))))
                .Returns(default(IWebSocketServerExtension));

            this.fallbackHandshaker.Setup(x => x.HandshakeExtension(
                        It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))))
                .Returns(this.fallbackExtension.Object);
            this.fallbackHandshaker.Setup(x => x.HandshakeExtension(
                        It.Is<WebSocketExtensionData>(v => v.Name.Equals("main"))))
                .Returns(default(IWebSocketServerExtension));

            this.mainExtension.Setup(x => x.Rsv).Returns(WebSocketRsv.Rsv1);
            this.mainExtension.Setup(x => x.NewReponseData()).Returns(
                new WebSocketExtensionData("main", new Dictionary<string, string>()));
            this.mainExtension.Setup(x => x.NewExtensionEncoder()).Returns(new DummyEncoder());
            this.mainExtension.Setup(x => x.NewExtensionDecoder()).Returns(new DummyDecoder());

            this.fallbackExtension.Setup(x => x.Rsv).Returns(WebSocketRsv.Rsv2);
            this.fallbackExtension.Setup(x => x.NewReponseData()).Returns(
                new WebSocketExtensionData("fallback", new Dictionary<string, string>()));
            this.fallbackExtension.Setup(x => x.NewExtensionEncoder()).Returns(new Dummy2Encoder());
            this.fallbackExtension.Setup(x => x.NewExtensionDecoder()).Returns(new Dummy2Decoder());

            var ch = new EmbeddedChannel(new WebSocketServerExtensionHandler(
                this.mainHandshaker.Object, this.fallbackHandshaker.Object));

            IHttpRequest req = NewUpgradeRequest("main, fallback");
            ch.WriteInbound(req);

            IHttpResponse res = NewUpgradeResponse(null);
            ch.WriteOutbound(res);

            var res2 = ch.ReadOutbound<IHttpResponse>();
            Assert.True(res2.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value));
            List<WebSocketExtensionData> resExts = WebSocketExtensionUtil.ExtractExtensions(value.ToString());

            Assert.Equal(2, resExts.Count);
            Assert.Equal("main", resExts[0].Name);
            Assert.Equal("fallback", resExts[1].Name);
            Assert.NotNull(ch.Pipeline.Get<DummyDecoder>());
            Assert.NotNull(ch.Pipeline.Get<DummyEncoder>());
            Assert.NotNull(ch.Pipeline.Get<Dummy2Decoder>());
            Assert.NotNull(ch.Pipeline.Get<Dummy2Encoder>());

            this.mainHandshaker.Verify(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("main"))),
                Times.AtLeastOnce);
            this.mainHandshaker.Verify(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))),
                Times.AtLeastOnce);
            this.fallbackHandshaker.Verify(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("fallback"))),
                Times.AtLeastOnce);

            this.mainExtension.Verify(x => x.Rsv, Times.Exactly(2));
            this.fallbackExtension.Verify(x => x.Rsv, Times.Exactly(2));
        }

        [Fact]
        public void NoneExtensionMatchingSuccess()
        {
            this.mainHandshaker.Setup(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("unknown")))).
                Returns(default(IWebSocketServerExtension));
            this.mainHandshaker.Setup(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("unknown2")))).
                Returns(default(IWebSocketServerExtension));

            this.fallbackHandshaker.Setup(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("unknown")))).
                Returns(default(IWebSocketServerExtension));
            this.fallbackHandshaker.Setup(x => x.HandshakeExtension(
                    It.Is<WebSocketExtensionData>(v => v.Name.Equals("unknown2")))).
                Returns(default(IWebSocketServerExtension));

            var ch = new EmbeddedChannel(new WebSocketServerExtensionHandler(
                this.mainHandshaker.Object, this.fallbackHandshaker.Object));

            IHttpRequest req = NewUpgradeRequest("unknown, unknown2");
            ch.WriteInbound(req);

            IHttpResponse res = NewUpgradeResponse(null);
            ch.WriteOutbound(res);

            var res2 = ch.ReadOutbound<IHttpResponse>();

            Assert.False(res2.Headers.Contains(HttpHeaderNames.SecWebsocketExtensions));
        }
    }
}
