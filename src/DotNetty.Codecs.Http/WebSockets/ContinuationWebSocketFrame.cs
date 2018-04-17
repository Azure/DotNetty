// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System.Text;
    using DotNetty.Buffers;

    public class ContinuationWebSocketFrame : WebSocketFrame
    {
        public ContinuationWebSocketFrame()
            : this(Unpooled.Buffer(0))
        {
        }

        public ContinuationWebSocketFrame(IByteBuffer binaryData)
            : base(binaryData)
        {
        }

        public ContinuationWebSocketFrame(bool finalFragment, int rsv, IByteBuffer binaryData)
            : base(finalFragment, rsv, binaryData)
        {
        }

        public ContinuationWebSocketFrame(bool finalFragment, int rsv, string text)
            : this(finalFragment, rsv, FromText(text))
        {
        }

        public string Text() => this.Content.ToString(Encoding.UTF8);

        static IByteBuffer FromText(string text) => string.IsNullOrEmpty(text) 
            ? Unpooled.Empty : Unpooled.CopiedBuffer(text, Encoding.UTF8);

        public override IByteBufferHolder Replace(IByteBuffer content) => new ContinuationWebSocketFrame(this.IsFinalFragment, this.Rsv, content);
    }
}
