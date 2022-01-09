// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Handshaker
{
    using System;
    using DotNetty.Common.Utilities;

    public class WebsocketHandshakerV07Selector: WebsocketHandshakerVersionSelector
    {
        public WebsocketHandshakerV07Selector(string webSocketUrl, string subprotocols, bool allowExtensions, int maxFramePayloadLength, bool allowMaskMismatch)
            : base(webSocketUrl, subprotocols, allowExtensions, maxFramePayloadLength, allowMaskMismatch){ }

        protected override Func<WebSocketServerHandshaker> InstanceFactory(string webSocketUrl, string subprotocols, bool allowExtensions, int maxFramePayloadLength, bool allowMaskMismatch)
        {
            return () => new WebSocketServerHandshaker07(webSocketUrl, subprotocols, allowExtensions, maxFramePayloadLength);
        }

        protected override bool Selected(ICharSequence version)
        {
            return version.Equals(WebSocketVersion.V07.ToHttpHeaderValue());
        }
    }
}