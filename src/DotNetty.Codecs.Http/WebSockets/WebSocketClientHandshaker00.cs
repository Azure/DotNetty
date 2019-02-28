// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Runtime.CompilerServices;
    using DotNetty.Buffers;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;

    public class WebSocketClientHandshaker00 : WebSocketClientHandshaker
    {
        static readonly AsciiString Websocket = AsciiString.Cached("WebSocket");

        IByteBuffer expectedChallengeResponseBytes;

        public WebSocketClientHandshaker00(Uri webSocketUrl, WebSocketVersion version, string subprotocol,
            HttpHeaders customHeaders, int maxFramePayloadLength)
            : base(webSocketUrl, version, subprotocol, customHeaders, maxFramePayloadLength)
        {
        }

        protected internal override unsafe IFullHttpRequest NewHandshakeRequest()
        {
            // Make keys
            int spaces1 = WebSocketUtil.RandomNumber(1, 12);
            int spaces2 = WebSocketUtil.RandomNumber(1, 12);

            int max1 = int.MaxValue / spaces1;
            int max2 = int.MaxValue / spaces2;

            int number1 = WebSocketUtil.RandomNumber(0, max1);
            int number2 = WebSocketUtil.RandomNumber(0, max2);

            int product1 = number1 * spaces1;
            int product2 = number2 * spaces2;

            string key1 = Convert.ToString(product1);
            string key2 = Convert.ToString(product2);

            key1 = InsertRandomCharacters(key1);
            key2 = InsertRandomCharacters(key2);

            key1 = InsertSpaces(key1, spaces1);
            key2 = InsertSpaces(key2, spaces2);

            byte[] key3 = WebSocketUtil.RandomBytes(8);
            var challenge = new byte[16];
            fixed (byte* bytes = challenge)
            {
                Unsafe.WriteUnaligned(bytes, number1);
                Unsafe.WriteUnaligned(bytes + 4, number2);
                PlatformDependent.CopyMemory(key3, 0, bytes + 8, 8);
            }

            this.expectedChallengeResponseBytes = Unpooled.WrappedBuffer(WebSocketUtil.Md5(challenge));

            // Get path
            Uri wsUrl = this.Uri;
            string path = RawPath(wsUrl);

            // Format request
            var request = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, path);
            HttpHeaders headers = request.Headers;
            headers.Add(HttpHeaderNames.Upgrade, Websocket)
                .Add(HttpHeaderNames.Connection, HttpHeaderValues.Upgrade)
                .Add(HttpHeaderNames.Host, WebsocketHostValue(wsUrl))
                .Add(HttpHeaderNames.Origin, WebsocketOriginValue(wsUrl))
                .Add(HttpHeaderNames.SecWebsocketKey1, key1)
                .Add(HttpHeaderNames.SecWebsocketKey2, key2);

            string expectedSubprotocol = this.ExpectedSubprotocol;
            if (!string.IsNullOrEmpty(expectedSubprotocol))
            {
                headers.Add(HttpHeaderNames.SecWebsocketProtocol, expectedSubprotocol);
            }

            if (this.CustomHeaders != null)
            {
                headers.Add(this.CustomHeaders);
            }

            // Set Content-Length to workaround some known defect.
            // See also: http://www.ietf.org/mail-archive/web/hybi/current/msg02149.html
            headers.Set(HttpHeaderNames.ContentLength, key3.Length);
            request.Content.WriteBytes(key3);
            return request;
        }

        protected override void Verify(IFullHttpResponse response)
        {
            if (!response.Status.Equals(HttpResponseStatus.SwitchingProtocols))
            {
                throw new WebSocketHandshakeException($"Invalid handshake response getStatus: {response.Status}");
            }

            HttpHeaders headers = response.Headers;

            if (!headers.TryGet(HttpHeaderNames.Upgrade, out ICharSequence upgrade) 
                ||!Websocket.ContentEqualsIgnoreCase(upgrade))
            {
                throw new WebSocketHandshakeException($"Invalid handshake response upgrade: {upgrade}");
            }

            if (!headers.ContainsValue(HttpHeaderNames.Connection, HttpHeaderValues.Upgrade, true))
            {
                headers.TryGet(HttpHeaderNames.Connection, out upgrade);
                throw new WebSocketHandshakeException($"Invalid handshake response connection: {upgrade}");
            }

            IByteBuffer challenge = response.Content;
            if (!challenge.Equals(this.expectedChallengeResponseBytes))
            {
                throw new WebSocketHandshakeException("Invalid challenge");
            }
        }

        static string InsertRandomCharacters(string key)
        {
            int count = WebSocketUtil.RandomNumber(1, 12);

            var randomChars = new char[count];
            int randCount = 0;
            while (randCount < count)
            {
                int rand = unchecked((int)(WebSocketUtil.RandomNext() * 0x7e + 0x21));
                if (0x21 < rand && rand < 0x2f || 0x3a < rand && rand < 0x7e)
                {
                    randomChars[randCount] = (char)rand;
                    randCount += 1;
                }
            }

            for (int i = 0; i < count; i++)
            {
                int split = WebSocketUtil.RandomNumber(0, key.Length);
                string part1 = key.Substring(0, split);
                string part2 = key.Substring(split);
                key = part1 + randomChars[i] + part2;
            }

            return key;
        }

        static string InsertSpaces(string key, int spaces)
        {
            for (int i = 0; i < spaces; i++)
            {
                int split = WebSocketUtil.RandomNumber(1, key.Length - 1);
                string part1 = key.Substring(0, split);
                string part2 = key.Substring(split);
                key = part1 + ' ' + part2;
            }

            return key;
        }

        protected internal override IWebSocketFrameDecoder NewWebSocketDecoder() => new WebSocket00FrameDecoder(this.MaxFramePayloadLength);

        protected internal override IWebSocketFrameEncoder NewWebSocketEncoder() => new WebSocket00FrameEncoder();
    }
}
