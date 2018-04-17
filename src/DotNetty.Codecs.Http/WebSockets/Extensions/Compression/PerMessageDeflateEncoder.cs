// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions.Compression
{
    using System.Collections.Generic;
    using DotNetty.Transport.Channels;

    class PerMessageDeflateEncoder : DeflateEncoder
    {
        bool compressing;

        public PerMessageDeflateEncoder(int compressionLevel, int windowSize, bool noContext)
            : base(compressionLevel, windowSize, noContext)
        {
        }

        public override bool AcceptOutboundMessage(object msg) =>
            ((msg is TextWebSocketFrame || msg is BinaryWebSocketFrame) 
                && (((WebSocketFrame) msg).Rsv & WebSocketRsv.Rsv1) == 0) 
            || (msg is ContinuationWebSocketFrame && this.compressing);

        protected override int Rsv(WebSocketFrame msg) =>
            msg is TextWebSocketFrame || msg is BinaryWebSocketFrame 
                ? msg.Rsv | WebSocketRsv.Rsv1 
                : msg.Rsv;

        protected override bool RemoveFrameTail(WebSocketFrame msg) => msg.IsFinalFragment;

        protected override void Encode(IChannelHandlerContext ctx, WebSocketFrame msg, List<object> output)
        {
            base.Encode(ctx, msg, output);

            if (msg.IsFinalFragment)
            {
                this.compressing = false;
            }
            else if (msg is TextWebSocketFrame || msg is BinaryWebSocketFrame)
            {
                this.compressing = true;
            }
        }
    }
}
