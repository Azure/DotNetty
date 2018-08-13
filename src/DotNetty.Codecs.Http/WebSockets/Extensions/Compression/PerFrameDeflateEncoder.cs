// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions.Compression
{
    class PerFrameDeflateEncoder : DeflateEncoder
    {
        public PerFrameDeflateEncoder(int compressionLevel, int windowSize, bool noContext)
            : base(compressionLevel, windowSize, noContext)
        {
        }

        public override bool AcceptOutboundMessage(object msg) =>
            (msg is TextWebSocketFrame || msg is BinaryWebSocketFrame || msg is ContinuationWebSocketFrame)
            && ((WebSocketFrame)msg).Content.ReadableBytes > 0
            && (((WebSocketFrame)msg).Rsv & WebSocketRsv.Rsv1) == 0;

        protected override int Rsv(WebSocketFrame msg) => msg.Rsv | WebSocketRsv.Rsv1;

        protected override bool RemoveFrameTail(WebSocketFrame msg) => true;
    }
}
