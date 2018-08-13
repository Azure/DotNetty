// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets.Extensions.Compression
{
    using System.Collections.Generic;
    using DotNetty.Codecs.Http.WebSockets.Extensions;
    using DotNetty.Codecs.Http.WebSockets.Extensions.Compression;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    using static Http.WebSockets.Extensions.Compression.PerMessageDeflateServerExtensionHandshaker;
    using static WebSocketExtensionTestUtil;

    public sealed class WebSocketServerCompressionHandlerTest
    {
        [Fact]
        public void NormalSuccess()
        {
            var ch = new EmbeddedChannel(new WebSocketServerCompressionHandler());

            IHttpRequest req = NewUpgradeRequest(PerMessageDeflateExtension);
            ch.WriteInbound(req);

            IHttpResponse res = NewUpgradeResponse(null);
            ch.WriteOutbound(res);

            var res2 = ch.ReadOutbound<IHttpResponse>();
            Assert.True(res2.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value));
            List<WebSocketExtensionData> exts = WebSocketExtensionUtil.ExtractExtensions(value.ToString());

            Assert.Equal(PerMessageDeflateExtension, exts[0].Name);
            Assert.Empty(exts[0].Parameters);
            Assert.NotNull(ch.Pipeline.Get<PerMessageDeflateDecoder>());
            Assert.NotNull(ch.Pipeline.Get<PerMessageDeflateEncoder>());
        }

        [Fact]
        public void ClientWindowSizeSuccess()
        {
            var ch = new EmbeddedChannel(
                new WebSocketServerExtensionHandler(
                    new PerMessageDeflateServerExtensionHandshaker(6, false, 10, false, false)));

            IHttpRequest req = NewUpgradeRequest(PerMessageDeflateExtension + "; " + ClientMaxWindow);
            ch.WriteInbound(req);

            IHttpResponse res = NewUpgradeResponse(null);
            ch.WriteOutbound(res);

            var res2 = ch.ReadOutbound<IHttpResponse>();
            Assert.True(res2.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value));
            List<WebSocketExtensionData> exts = WebSocketExtensionUtil.ExtractExtensions(value.ToString());

            Assert.Equal(PerMessageDeflateExtension, exts[0].Name);
            Assert.Equal("10", exts[0].Parameters[ClientMaxWindow]);
            Assert.NotNull(ch.Pipeline.Get<PerMessageDeflateDecoder>());
            Assert.NotNull(ch.Pipeline.Get<PerMessageDeflateEncoder>());
        }

        [Fact]
        public void ClientWindowSizeUnavailable()
        {
            var ch = new EmbeddedChannel(
                new WebSocketServerExtensionHandler(
                    new PerMessageDeflateServerExtensionHandshaker(6, false, 10, false, false)));

            IHttpRequest req = NewUpgradeRequest(PerMessageDeflateExtension);
            ch.WriteInbound(req);

            IHttpResponse res = NewUpgradeResponse(null);
            ch.WriteOutbound(res);

            var res2 = ch.ReadOutbound<IHttpResponse>();
            Assert.True(res2.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value));
            List<WebSocketExtensionData> exts = WebSocketExtensionUtil.ExtractExtensions(value.ToString());

            Assert.Equal(PerMessageDeflateExtension, exts[0].Name);
            Assert.Empty(exts[0].Parameters);
            Assert.NotNull(ch.Pipeline.Get<PerMessageDeflateDecoder>());
            Assert.NotNull(ch.Pipeline.Get<PerMessageDeflateEncoder>());
        }

        [Fact]
        public void ServerWindowSizeSuccess()
        {
            var ch = new EmbeddedChannel(
                new WebSocketServerExtensionHandler(
                    new PerMessageDeflateServerExtensionHandshaker(6, true, 15, false, false)));

            IHttpRequest req = NewUpgradeRequest(PerMessageDeflateExtension + "; " + ServerMaxWindow + "=10");
            ch.WriteInbound(req);

            IHttpResponse res = NewUpgradeResponse(null);
            ch.WriteOutbound(res);

            var res2 = ch.ReadOutbound<IHttpResponse>();
            Assert.True(res2.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value));
            List<WebSocketExtensionData> exts = WebSocketExtensionUtil.ExtractExtensions(value.ToString());

            Assert.Equal(PerMessageDeflateExtension, exts[0].Name);
            Assert.Equal("10", exts[0].Parameters[ServerMaxWindow]);
            Assert.NotNull(ch.Pipeline.Get<PerMessageDeflateDecoder>());
            Assert.NotNull(ch.Pipeline.Get<PerMessageDeflateEncoder>());
        }

        [Fact]
        public void ServerWindowSizeDisable()
        {
            var ch = new EmbeddedChannel(
                new WebSocketServerExtensionHandler(
                    new PerMessageDeflateServerExtensionHandshaker(6, false, 15, false, false)));

            IHttpRequest req = NewUpgradeRequest(PerMessageDeflateExtension + "; " + ServerMaxWindow + "=10");
            ch.WriteInbound(req);

            IHttpResponse res = NewUpgradeResponse(null);
            ch.WriteOutbound(res);

            var res2 = ch.ReadOutbound<IHttpResponse>();

            Assert.False(res2.Headers.Contains(HttpHeaderNames.SecWebsocketExtensions));
            Assert.Null(ch.Pipeline.Get<PerMessageDeflateDecoder>());
            Assert.Null(ch.Pipeline.Get<PerMessageDeflateEncoder>());
        }

        [Fact]
        public void ServerNoContext()
        {
            var ch = new EmbeddedChannel(new WebSocketServerCompressionHandler());

            IHttpRequest req = NewUpgradeRequest(
                PerMessageDeflateExtension + "; "
                + PerMessageDeflateServerExtensionHandshaker.ServerNoContext);
            ch.WriteInbound(req);

            IHttpResponse res = NewUpgradeResponse(null);
            ch.WriteOutbound(res);

            var res2 = ch.ReadOutbound<IHttpResponse>();

            Assert.False(res2.Headers.Contains(HttpHeaderNames.SecWebsocketExtensions));
            Assert.Null(ch.Pipeline.Get<PerMessageDeflateDecoder>());
            Assert.Null(ch.Pipeline.Get<PerMessageDeflateEncoder>());
        }

        [Fact]
        public void ClientNoContext()
        {
            var ch = new EmbeddedChannel(new WebSocketServerCompressionHandler());

            IHttpRequest req = NewUpgradeRequest(
                PerMessageDeflateExtension + "; "
                + PerMessageDeflateServerExtensionHandshaker.ClientNoContext);
            ch.WriteInbound(req);

            IHttpResponse res = NewUpgradeResponse(null);
            ch.WriteOutbound(res);

            var res2 = ch.ReadOutbound<IHttpResponse>();
            Assert.True(res2.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value));
            List<WebSocketExtensionData> exts = WebSocketExtensionUtil.ExtractExtensions(value.ToString());

            Assert.Equal(PerMessageDeflateExtension, exts[0].Name);
            Assert.Empty(exts[0].Parameters);
            Assert.NotNull(ch.Pipeline.Get<PerMessageDeflateDecoder>());
            Assert.NotNull(ch.Pipeline.Get<PerMessageDeflateEncoder>());
        }

        [Fact]
        public void ServerWindowSizeDisableThenFallback()
        {
            var ch = new EmbeddedChannel(new WebSocketServerExtensionHandler(
                    new PerMessageDeflateServerExtensionHandshaker(6, false, 15, false, false)));

            IHttpRequest req = NewUpgradeRequest(
                PerMessageDeflateExtension + "; " + ServerMaxWindow + "=10, " +
                PerMessageDeflateExtension);
            ch.WriteInbound(req);

            IHttpResponse res = NewUpgradeResponse(null);
            ch.WriteOutbound(res);

            var res2 = ch.ReadOutbound<IHttpResponse>();
            Assert.True(res2.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value));
            List<WebSocketExtensionData> exts = WebSocketExtensionUtil.ExtractExtensions(value.ToString());

            Assert.Equal(PerMessageDeflateExtension, exts[0].Name);
            Assert.Empty(exts[0].Parameters);
            Assert.NotNull(ch.Pipeline.Get<PerMessageDeflateDecoder>());
            Assert.NotNull(ch.Pipeline.Get<PerMessageDeflateEncoder>());
        }
    }
}
