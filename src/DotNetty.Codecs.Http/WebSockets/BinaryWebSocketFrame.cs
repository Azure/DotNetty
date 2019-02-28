// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using DotNetty.Buffers;

    public class BinaryWebSocketFrame : WebSocketFrame
    {
        public BinaryWebSocketFrame() 
            : base(Unpooled.Buffer(0))
        {
        }

        public BinaryWebSocketFrame(IByteBuffer binaryData)
            : base(binaryData)
        {
        }

        public BinaryWebSocketFrame(bool finalFragment, int rsv, IByteBuffer binaryData)
            : base(finalFragment, rsv, binaryData)
        {
        }

        public override IByteBufferHolder Replace(IByteBuffer content) => new BinaryWebSocketFrame(this.IsFinalFragment, this.Rsv, content);
    }
}
