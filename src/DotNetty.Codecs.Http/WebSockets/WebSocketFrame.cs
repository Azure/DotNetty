// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http.WebSockets
{
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    public abstract class WebSocketFrame : DefaultByteBufferHolder
    {
        // Flag to indicate if this frame is the final fragment in a message. The first fragment (frame) may also be the
        // final fragment.
        readonly bool finalFragment;

        // RSV1, RSV2, RSV3 used for extensions
        readonly int rsv;

        protected WebSocketFrame(IByteBuffer binaryData) 
            : this(true, 0, binaryData)
        {
        }

        protected WebSocketFrame(bool finalFragment, int rsv, IByteBuffer binaryData)
            : base(binaryData)
        {
            this.finalFragment = finalFragment;
            this.rsv = rsv;
        }

        /// <summary>
        /// Flag to indicate if this frame is the final fragment in a message. The first fragment (frame)
        /// may also be the final fragment.
        /// </summary>
        public bool IsFinalFragment => this.finalFragment;

        /// <summary>
        /// RSV1, RSV2, RSV3 used for extensions
        /// </summary>
        public int Rsv => this.rsv;

        public override string ToString() => StringUtil.SimpleClassName(this) + "(data: " + this.ContentToString() + ')';
    }
}
