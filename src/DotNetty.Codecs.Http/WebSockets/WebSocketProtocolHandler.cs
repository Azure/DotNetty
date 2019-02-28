// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Transport.Channels;

    public abstract class WebSocketProtocolHandler : MessageToMessageDecoder<WebSocketFrame>
    {
        readonly bool dropPongFrames;

        internal WebSocketProtocolHandler() : this(true)
        {
        }

        internal WebSocketProtocolHandler(bool dropPongFrames)
        {
            this.dropPongFrames = dropPongFrames;
        }

        protected override void Decode(IChannelHandlerContext ctx, WebSocketFrame frame, List<object> output)
        {
            if (frame is PingWebSocketFrame)
            {
                frame.Content.Retain();
                ctx.Channel.WriteAndFlushAsync(new PongWebSocketFrame(frame.Content));
                return;
            }

            if (frame is PongWebSocketFrame && this.dropPongFrames)
            {
                // Pong frames need to get ignored
                return;
            }

            output.Add(frame.Retain());
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            ctx.FireExceptionCaught(cause);
            ctx.CloseAsync();
        }
    }
}
