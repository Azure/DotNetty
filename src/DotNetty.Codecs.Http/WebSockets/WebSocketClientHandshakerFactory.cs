// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;

    using static WebSocketVersion;

    public static class WebSocketClientHandshakerFactory
    {
        public static WebSocketClientHandshaker NewHandshaker(Uri webSocketUrl, WebSocketVersion version, string subprotocol, bool allowExtensions, HttpHeaders customHeaders) => 
            NewHandshaker(webSocketUrl, version, subprotocol, allowExtensions, customHeaders, 65536);

        public static WebSocketClientHandshaker NewHandshaker(Uri webSocketUrl, WebSocketVersion version, string subprotocol, bool allowExtensions, HttpHeaders customHeaders, int maxFramePayloadLength) => 
                NewHandshaker(webSocketUrl, version, subprotocol, allowExtensions, customHeaders, maxFramePayloadLength, true, false);

        public static WebSocketClientHandshaker NewHandshaker(
            Uri webSocketUrl, WebSocketVersion version, string subprotocol,
            bool allowExtensions, HttpHeaders customHeaders, int maxFramePayloadLength,
            bool performMasking, bool allowMaskMismatch)
        {
            if (version == V13)
            {
                return new WebSocketClientHandshaker13(
                    webSocketUrl, V13, subprotocol, allowExtensions, customHeaders,
                    maxFramePayloadLength, performMasking, allowMaskMismatch);
            }
            if (version == V08)
            {
                return new WebSocketClientHandshaker08(
                    webSocketUrl, V08, subprotocol, allowExtensions, customHeaders,
                    maxFramePayloadLength, performMasking, allowMaskMismatch);
            }
            if (version == V07)
            {
                return new WebSocketClientHandshaker07(
                    webSocketUrl, V07, subprotocol, allowExtensions, customHeaders,
                    maxFramePayloadLength, performMasking, allowMaskMismatch);
            }
            if (version == V00)
            {
                return new WebSocketClientHandshaker00(
                    webSocketUrl, V00, subprotocol, customHeaders, maxFramePayloadLength);
            }

            throw new WebSocketHandshakeException($"Protocol version {version}not supported.");
        }
    }
}
