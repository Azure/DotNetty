﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions.Compression
{
    public class WebSocketServerCompressionHandler : WebSocketServerExtensionHandler
    {
        public WebSocketServerCompressionHandler()
            : base(new PerMessageDeflateServerExtensionHandshaker(), new DeflateFrameServerExtensionHandshaker())
        {
        }
    }
}
