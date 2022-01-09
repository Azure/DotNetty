// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Handshaker
{
    using System;
    using DotNetty.Common.Utilities;

    public abstract class WebsocketHandshakerVersionSelector
    {
        protected Func<WebSocketServerHandshaker> Factory;

        protected WebsocketHandshakerVersionSelector(
            string webSocketUrl,
            string subprotocols,
            bool allowExtensions,
            int maxFramePayloadLength,
            bool allowMaskMismatch)
        {
            this.Factory = this.InstanceFactory(webSocketUrl, subprotocols, allowExtensions, maxFramePayloadLength, allowExtensions);
        }

        protected abstract Func<WebSocketServerHandshaker> InstanceFactory(
            string webSocketUrl,
            string subprotocols,
            bool allowExtensions,
            int maxFramePayloadLength,
            bool allowMaskMismatch);

        protected abstract bool Selected(ICharSequence version);


        public bool Selector(ICharSequence version, out WebSocketServerHandshaker handshaker)
        {
            handshaker = null;
            if (!this.Selected(version))
            {
                return false;
            }

            handshaker = this.Factory();
            return true;
        }
    }
}