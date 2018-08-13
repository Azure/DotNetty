// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;

    class WebSocketClientProtocolHandshakeHandler : ChannelHandlerAdapter
    {
        readonly WebSocketClientHandshaker handshaker;

        internal WebSocketClientProtocolHandshakeHandler(WebSocketClientHandshaker handshaker)
        {
            this.handshaker = handshaker;
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            base.ChannelActive(context);
            this.handshaker.HandshakeAsync(context.Channel)
                .ContinueWith((t, state) =>
                {
                    var ctx = (IChannelHandlerContext)state;
                    if (t.Status == TaskStatus.RanToCompletion)
                    {
                        ctx.FireUserEventTriggered(WebSocketClientProtocolHandler.ClientHandshakeStateEvent.HandshakeIssued);
                    }
                    else
                    {
                        ctx.FireExceptionCaught(t.Exception);
                    }
                }, 
                context, 
                TaskContinuationOptions.ExecuteSynchronously);
        }

        public override void ChannelRead(IChannelHandlerContext ctx, object msg)
        {
            if (!(msg is IFullHttpResponse))
            {
                ctx.FireChannelRead(msg);
                return;
            }

            var response = (IFullHttpResponse)msg;
            try
            {
                if (!this.handshaker.IsHandshakeComplete)
                {
                    this.handshaker.FinishHandshake(ctx.Channel, response);
                    ctx.FireUserEventTriggered(WebSocketClientProtocolHandler.ClientHandshakeStateEvent.HandshakeComplete);
                    ctx.Channel.Pipeline.Remove(this);
                    return;
                }

                throw new InvalidOperationException("WebSocketClientHandshaker should have been finished yet");
            }
            finally
            {
                response.Release();
            }
        }
    }
}
