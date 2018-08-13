// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Transport.Channels;

    public class WebSocketFrameAggregator : MessageAggregator<WebSocketFrame, WebSocketFrame, ContinuationWebSocketFrame, WebSocketFrame>
    {
        public WebSocketFrameAggregator(int maxContentLength)
            : base(maxContentLength)
        {
        }

        protected override bool IsStartMessage(WebSocketFrame msg) => msg is TextWebSocketFrame || msg is BinaryWebSocketFrame;

        protected override bool IsContentMessage(WebSocketFrame msg) => msg is ContinuationWebSocketFrame;

        protected override bool IsLastContentMessage(ContinuationWebSocketFrame msg) => this.IsContentMessage(msg) && msg.IsFinalFragment;

        protected override bool IsAggregated(WebSocketFrame msg)
        {
            if (msg.IsFinalFragment)
            {
                return !this.IsContentMessage(msg);
            }

            return !this.IsStartMessage(msg) && !this.IsContentMessage(msg);
        }

        protected override bool IsContentLengthInvalid(WebSocketFrame start, int maxContentLength) => false;

        protected override object NewContinueResponse(WebSocketFrame start, int maxContentLength, IChannelPipeline pipeline) => null;

        protected override bool CloseAfterContinueResponse(object msg) => throw new NotSupportedException();

        protected override bool IgnoreContentAfterContinueResponse(object msg) => throw new NotSupportedException();

        protected override WebSocketFrame BeginAggregation(WebSocketFrame start, IByteBuffer content)
        {
            if (start is TextWebSocketFrame)
            {
                return new TextWebSocketFrame(true, start.Rsv, content);
            }

            if (start is BinaryWebSocketFrame)
            {
                return new BinaryWebSocketFrame(true, start.Rsv, content);
            }

            // Should not reach here.
            throw new Exception("Unkonw WebSocketFrame type, must be either TextWebSocketFrame or BinaryWebSocketFrame");
        }
    }
}
