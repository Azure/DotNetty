// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable InconsistentNaming
namespace DotNetty.Codecs.Http.WebSockets
{
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class WebSocket00FrameEncoder : MessageToMessageEncoder<WebSocketFrame>, IWebSocketFrameEncoder
    {
        static readonly IByteBuffer _0X00 = Unpooled.UnreleasableBuffer(
            Unpooled.DirectBuffer(1, 1).WriteByte(0x00));

        static readonly IByteBuffer _0XFF = Unpooled.UnreleasableBuffer(
            Unpooled.DirectBuffer(1, 1).WriteByte(0xFF));

        static readonly IByteBuffer _0XFF_0X00 = Unpooled.UnreleasableBuffer(
            Unpooled.DirectBuffer(2, 2).WriteByte(0xFF).WriteByte(0x00));

        public override bool IsSharable => true;

        protected override void Encode(IChannelHandlerContext context, WebSocketFrame message, List<object> output)
        {
            if (message is TextWebSocketFrame)
            {
                // Text frame
                IByteBuffer data = message.Content;

                output.Add(_0X00.Duplicate());
                output.Add(data.Retain());
                output.Add(_0XFF.Duplicate());
            }
            else if (message is CloseWebSocketFrame)
            {
                // Close frame, needs to call duplicate to allow multiple writes.
                // See https://github.com/netty/netty/issues/2768
                output.Add(_0XFF_0X00.Duplicate());
            }
            else
            {
                // Binary frame
                IByteBuffer data = message.Content;
                int dataLen = data.ReadableBytes;

                IByteBuffer buf = context.Allocator.Buffer(5);
                bool release = true;
                try
                {
                    // Encode type.
                    buf.WriteByte(0x80);

                    // Encode length.
                    int b1 = dataLen.RightUShift(28) & 0x7F;
                    int b2 = dataLen.RightUShift(14) & 0x7F;
                    int b3 = dataLen.RightUShift(7) & 0x7F;
                    int b4 = dataLen & 0x7F;
                    if (b1 == 0)
                    {
                        if (b2 == 0)
                        {
                            if (b3 == 0)
                            {
                                buf.WriteByte(b4);
                            }
                            else
                            {
                                buf.WriteByte(b3 | 0x80);
                                buf.WriteByte(b4);
                            }
                        }
                        else
                        {
                            buf.WriteByte(b2 | 0x80);
                            buf.WriteByte(b3 | 0x80);
                            buf.WriteByte(b4);
                        }
                    }
                    else
                    {
                        buf.WriteByte(b1 | 0x80);
                        buf.WriteByte(b2 | 0x80);
                        buf.WriteByte(b3 | 0x80);
                        buf.WriteByte(b4);
                    }

                    // Encode binary data.
                    output.Add(buf);
                    output.Add(data.Retain());
                    release = false;
                }
                finally
                {
                    if (release)
                    {
                        buf.Release();
                    }
                }
            }
        }
    }
}
