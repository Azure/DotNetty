// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Handshaker
{
    using System;
    using DotNetty.Common.Utilities;

    public class WebsocketHandshaker00Selector: WebsocketHandshakerVersionSelector
    {
        public WebsocketHandshaker00Selector(string webSocketUrl, string subprotocols, bool allowExtensions, int maxFramePayloadLength, bool allowMaskMismatch)
            : base(webSocketUrl, subprotocols, allowExtensions, maxFramePayloadLength, allowMaskMismatch){ }

        protected override Func<WebSocketServerHandshaker> InstanceFactory(string webSocketUrl, string subprotocols, bool allowExtensions, int maxFramePayloadLength, bool allowMaskMismatch)
        {
            return () => new WebSocketServerHandshaker00(webSocketUrl, subprotocols, maxFramePayloadLength);
        }

        protected override bool Selected(ICharSequence version)
        {
            return version == null;
        }
    }
}