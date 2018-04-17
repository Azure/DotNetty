// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets.Extensions.Compression
{
    using System.Collections.Generic;
    using DotNetty.Codecs.Compression;
    using DotNetty.Codecs.Http.WebSockets.Extensions;
    using DotNetty.Codecs.Http.WebSockets.Extensions.Compression;
    using Xunit;

    using static Http.WebSockets.Extensions.Compression.PerMessageDeflateServerExtensionHandshaker;

    public sealed class PerMessageDeflateClientExtensionHandshakerTest
    {
        [Fact]
        public void NormalData()
        {
            var handshaker = new PerMessageDeflateClientExtensionHandshaker();
            WebSocketExtensionData data = handshaker.NewRequestData();

            Assert.Equal(PerMessageDeflateExtension, data.Name);
            Assert.Equal(ZlibCodecFactory.IsSupportingWindowSizeAndMemLevel ? 1 : 0, data.Parameters.Count);
        }

        [Fact]
        public void CustomData()
        {
            var handshaker = new PerMessageDeflateClientExtensionHandshaker(6, true, 10, true, true);
            WebSocketExtensionData data = handshaker.NewRequestData();

            Assert.Equal(PerMessageDeflateExtension, data.Name);
            Assert.Contains(ClientMaxWindow, data.Parameters.Keys);
            Assert.Contains(ServerMaxWindow, data.Parameters.Keys);
            Assert.Equal("10", data.Parameters[ServerMaxWindow]);
        }

        [Fact]
        public void NormalHandshake()
        {
            var handshaker = new PerMessageDeflateClientExtensionHandshaker();

            IWebSocketClientExtension extension = handshaker.HandshakeExtension(
                new WebSocketExtensionData(PerMessageDeflateExtension, new Dictionary<string, string>()));

            Assert.NotNull(extension);
            Assert.Equal(WebSocketRsv.Rsv1, extension.Rsv);
            Assert.IsType<PerMessageDeflateDecoder>(extension.NewExtensionDecoder());
            Assert.IsType<PerMessageDeflateEncoder>(extension.NewExtensionEncoder());
        }

        [Fact]
        public void CustomHandshake()
        {
            var handshaker = new PerMessageDeflateClientExtensionHandshaker(6, true, 10, true, true);

            var parameters = new Dictionary<string, string>
            {
                { ClientMaxWindow, "12" },
                { ServerMaxWindow, "10" },
                { ClientNoContext, null },
                { ServerNoContext, null }
            };
            IWebSocketClientExtension extension = handshaker.HandshakeExtension(
                new WebSocketExtensionData(PerMessageDeflateExtension, parameters));

            Assert.NotNull(extension);
            Assert.Equal(WebSocketRsv.Rsv1, extension.Rsv);
            Assert.IsType<PerMessageDeflateDecoder>(extension.NewExtensionDecoder());
            Assert.IsType<PerMessageDeflateEncoder>(extension.NewExtensionEncoder());

            parameters = new Dictionary<string, string>
            {
                { ServerMaxWindow, "10" },
                { ServerNoContext, null }
            };
            extension = handshaker.HandshakeExtension(
                new WebSocketExtensionData(PerMessageDeflateExtension, parameters));

            Assert.NotNull(extension);
            Assert.Equal(WebSocketRsv.Rsv1, extension.Rsv);
            Assert.IsType<PerMessageDeflateDecoder>(extension.NewExtensionDecoder());
            Assert.IsType<PerMessageDeflateEncoder>(extension.NewExtensionEncoder());

            parameters = new Dictionary<string, string>();
            extension = handshaker.HandshakeExtension(
                new WebSocketExtensionData(PerMessageDeflateExtension, parameters));

            Assert.Null(extension);
        }
    }
}
