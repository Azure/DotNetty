// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using DotNetty.Buffers;

    public class PingWebSocketFrame : WebSocketFrame
    {
        public PingWebSocketFrame()
            : base(true, 0, Unpooled.Buffer(0))
        {
        }

        public PingWebSocketFrame(IByteBuffer binaryData)
            : base(binaryData)
        {
        }

        public PingWebSocketFrame(bool finalFragment, int rsv, IByteBuffer binaryData)
            : base(finalFragment, rsv, binaryData)
        {
        }

        public override IByteBufferHolder Replace(IByteBuffer content) => new PingWebSocketFrame(this.IsFinalFragment, this.Rsv, content);
    }
}
