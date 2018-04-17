// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets.Extensions.Compression
{
    using System.Collections.Generic;
    using DotNetty.Codecs.Http.WebSockets.Extensions;
    using DotNetty.Codecs.Http.WebSockets.Extensions.Compression;
    using Xunit;

    using static Http.WebSockets.Extensions.Compression.PerMessageDeflateServerExtensionHandshaker;

    public sealed class PerMessageDeflateServerExtensionHandshakerTest
    {
        [Fact]
        public void NormalHandshake()
        {
            var handshaker = new PerMessageDeflateServerExtensionHandshaker();
            IWebSocketServerExtension extension = handshaker.HandshakeExtension(
                    new WebSocketExtensionData(PerMessageDeflateExtension, new Dictionary<string, string>()));

            Assert.NotNull(extension);
            Assert.Equal(WebSocketRsv.Rsv1, extension.Rsv);
            Assert.IsType<PerMessageDeflateDecoder>(extension.NewExtensionDecoder());
            Assert.IsType<PerMessageDeflateEncoder>(extension.NewExtensionEncoder());

            WebSocketExtensionData data = extension.NewReponseData();

            Assert.Equal(PerMessageDeflateExtension, data.Name);
            Assert.Empty(data.Parameters);

            var parameters = new Dictionary<string, string>
            {
                { ClientMaxWindow, null },
                { ClientNoContext, null }
            };

            extension = handshaker.HandshakeExtension(
                new WebSocketExtensionData(PerMessageDeflateExtension, parameters));

            Assert.NotNull(extension);
            Assert.Equal(WebSocketRsv.Rsv1, extension.Rsv);
            Assert.IsType<PerMessageDeflateDecoder>(extension.NewExtensionDecoder());
            Assert.IsType<PerMessageDeflateEncoder>(extension.NewExtensionEncoder());

            data = extension.NewReponseData();

            Assert.Equal(PerMessageDeflateExtension, data.Name);
            Assert.Empty(data.Parameters);

            parameters = new Dictionary<string, string>
            {
                { ServerMaxWindow, "12" },
                { ServerNoContext, null }
            };

            extension = handshaker.HandshakeExtension(
                    new WebSocketExtensionData(PerMessageDeflateExtension, parameters));
            Assert.Null(extension);
        }

        [Fact]
        public void CustomHandshake()
        {
            var handshaker = new PerMessageDeflateServerExtensionHandshaker(6, true, 10, true, true);

            var parameters = new Dictionary<string, string>
            {
                { ClientMaxWindow, null },
                { ServerMaxWindow, "12" },
                { ClientNoContext, null },
                { ServerNoContext, null }
            };

            IWebSocketServerExtension extension = handshaker.HandshakeExtension(
                    new WebSocketExtensionData(PerMessageDeflateExtension, parameters));

            Assert.NotNull(extension);
            Assert.Equal(WebSocketRsv.Rsv1, extension.Rsv);
            Assert.IsType<PerMessageDeflateDecoder>(extension.NewExtensionDecoder());
            Assert.IsType<PerMessageDeflateEncoder>(extension.NewExtensionEncoder());

            WebSocketExtensionData data = extension.NewReponseData();

            Assert.Equal(PerMessageDeflateExtension, data.Name);
            Assert.Contains(ClientMaxWindow, data.Parameters.Keys);
            Assert.Equal("10", data.Parameters[ClientMaxWindow]);
            Assert.Contains(ServerMaxWindow, data.Parameters.Keys);
            Assert.Equal("12", data.Parameters[ServerMaxWindow]);

            parameters = new Dictionary<string, string>
            {
                { ServerMaxWindow, "12" },
                { ServerNoContext, null }
            };
            extension = handshaker.HandshakeExtension(
                    new WebSocketExtensionData(PerMessageDeflateExtension, parameters));

            Assert.NotNull(extension);
            Assert.Equal(WebSocketRsv.Rsv1, extension.Rsv);
            Assert.IsType<PerMessageDeflateDecoder>(extension.NewExtensionDecoder());
            Assert.IsType<PerMessageDeflateEncoder>(extension.NewExtensionEncoder());

            data = extension.NewReponseData();

            Assert.Equal(PerMessageDeflateExtension, data.Name);
            Assert.Equal(2, data.Parameters.Count);
            Assert.Contains(ServerMaxWindow, data.Parameters.Keys);
            Assert.Equal("12", data.Parameters[ServerMaxWindow]);
            Assert.Contains(ServerNoContext, data.Parameters.Keys);

            parameters = new Dictionary<string, string>();
            extension = handshaker.HandshakeExtension(
                    new WebSocketExtensionData(PerMessageDeflateExtension, parameters));
            Assert.NotNull(extension);

            data = extension.NewReponseData();
            Assert.Equal(PerMessageDeflateExtension, data.Name);
            Assert.Empty(data.Parameters);
        }
    }
}
