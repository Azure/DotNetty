// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets.Extensions.Compression
{
    using System.Collections.Generic;
    using DotNetty.Codecs.Http.WebSockets.Extensions;
    using DotNetty.Codecs.Http.WebSockets.Extensions.Compression;
    using Xunit;

    using static Http.WebSockets.Extensions.Compression.DeflateFrameServerExtensionHandshaker;

    public sealed class DeflateFrameClientExtensionHandshakerTest
    {
        [Fact]
        public void WebkitDeflateFrameData()
        {
            var handshaker = new DeflateFrameClientExtensionHandshaker(true);

            WebSocketExtensionData data = handshaker.NewRequestData();

            Assert.Equal(XWebkitDeflateFrameExtension, data.Name);
            Assert.Empty(data.Parameters);
        }

        [Fact]
        public void DeflateFrameData()
        {
            var handshaker = new DeflateFrameClientExtensionHandshaker(false);

            WebSocketExtensionData data = handshaker.NewRequestData();

            Assert.Equal(DeflateFrameExtension, data.Name);
            Assert.Empty(data.Parameters);
        }

        [Fact]
        public void NormalHandshake()
        {
            var handshaker = new DeflateFrameClientExtensionHandshaker(false);

            IWebSocketClientExtension extension = handshaker.HandshakeExtension(
                new WebSocketExtensionData(DeflateFrameExtension, new Dictionary<string, string>()));

            Assert.NotNull(extension);
            Assert.Equal(WebSocketRsv.Rsv1, extension.Rsv);
            Assert.IsType<PerFrameDeflateDecoder>(extension.NewExtensionDecoder());
            Assert.IsType<PerFrameDeflateEncoder>(extension.NewExtensionEncoder());
        }

        [Fact]
        public void FailedHandshake()
        {
            var handshaker = new DeflateFrameClientExtensionHandshaker(false);

            var parameters = new Dictionary<string, string>
            {
                { "invalid", "12" }
            };
            IWebSocketClientExtension extension = handshaker.HandshakeExtension(
                new WebSocketExtensionData(DeflateFrameExtension, parameters));

            Assert.Null(extension);
        }
    }
}
