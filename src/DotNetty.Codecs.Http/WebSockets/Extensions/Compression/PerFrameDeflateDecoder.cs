// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions.Compression
{
    class PerFrameDeflateDecoder : DeflateDecoder
    {
        public PerFrameDeflateDecoder(bool noContext)
            : base(noContext)
        {
        }

        public override bool AcceptInboundMessage(object msg) => 
            (msg is TextWebSocketFrame || msg is BinaryWebSocketFrame || msg is ContinuationWebSocketFrame) 
            && (((WebSocketFrame) msg).Rsv & WebSocketRsv.Rsv1) > 0;

        protected override int NewRsv(WebSocketFrame msg) => msg.Rsv ^ WebSocketRsv.Rsv1;

        protected override bool AppendFrameTail(WebSocketFrame msg) => true;
    }
}
