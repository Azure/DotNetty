// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets.Extensions
{
    using DotNetty.Codecs.Http.WebSockets.Extensions;
    using Xunit;

    public sealed class WebSocketExtensionUtilTest
    {
        [Fact]
        public void IsWebsocketUpgrade()
        {
            HttpHeaders headers = new DefaultHttpHeaders();
            Assert.False(WebSocketExtensionUtil.IsWebsocketUpgrade(headers));

            headers.Add(HttpHeaderNames.Upgrade, HttpHeaderValues.Websocket);
            Assert.False(WebSocketExtensionUtil.IsWebsocketUpgrade(headers));

            headers.Add(HttpHeaderNames.Connection, "Keep-Alive, Upgrade");
            Assert.True(WebSocketExtensionUtil.IsWebsocketUpgrade(headers));
        }
    }
}
