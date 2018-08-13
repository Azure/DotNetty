// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Utilities;

    public class WebSocketClientHandshaker07Test : WebSocketClientHandshakerTest
    {
        protected override WebSocketClientHandshaker NewHandshaker(Uri uri) => new WebSocketClientHandshaker07(uri, WebSocketVersion.V07, null, false, null, 1024);

        protected override AsciiString GetOriginHeaderName() => HttpHeaderNames.SecWebsocketOrigin;
    }
}
