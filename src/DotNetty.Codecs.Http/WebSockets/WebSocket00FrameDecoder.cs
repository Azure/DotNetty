// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    using static Buffers.ByteBufferUtil;

    public class WebSocket00FrameDecoder : ReplayingDecoder<WebSocket00FrameDecoder.Void>, IWebSocketFrameDecoder
    {
        public enum Void
        {
            // Empty state
        }

        const int DefaultMaxFrameSize = 16384;

        readonly long maxFrameSize;
        bool receivedClosingHandshake;

        public WebSocket00FrameDecoder() : this(DefaultMaxFrameSize)
        {
        }

        public WebSocket00FrameDecoder(int maxFrameSize) : base(default(Void))
        {
            this.maxFrameSize = maxFrameSize;
        }

        protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            // Discard all data received if closing handshake was received before.
            if (this.receivedClosingHandshake)
            {
                input.SkipBytes(this.ActualReadableBytes);
                return;
            }

            // Decode a frame otherwise.
            byte type = input.ReadByte();
            WebSocketFrame frame;
            if ((type & 0x80) == 0x80)
            {
                // If the MSB on type is set, decode the frame length
                frame = this.DecodeBinaryFrame(context, type, input);
            }
            else
            {
                // Decode a 0xff terminated UTF-8 string
                frame = this.DecodeTextFrame(context, input);
            }

            if (frame != null)
            {
                output.Add(frame);
            }
        }

        WebSocketFrame DecodeBinaryFrame(IChannelHandlerContext ctx, byte type, IByteBuffer buffer)
        {
            long frameSize = 0;
            int lengthFieldSize = 0;
            byte b;
            do
            {
                b = buffer.ReadByte();
                frameSize <<= 7;
                frameSize |= (uint)(b & 0x7f);
                if (frameSize > this.maxFrameSize)
                {
                    throw new TooLongFrameException(nameof(WebSocket00FrameDecoder));
                }
                lengthFieldSize++;
                if (lengthFieldSize > 8)
                {
                    // Perhaps a malicious peer?
                    throw new TooLongFrameException(nameof(WebSocket00FrameDecoder));
                }
            } while ((b & 0x80) == 0x80);

            if (type == 0xFF && frameSize == 0)
            {
                this.receivedClosingHandshake = true;
                return new CloseWebSocketFrame();
            }
            IByteBuffer payload = ReadBytes(ctx.Allocator, buffer, (int)frameSize);
            return new BinaryWebSocketFrame(payload);
        }

        WebSocketFrame DecodeTextFrame(IChannelHandlerContext ctx, IByteBuffer buffer)
        {
            int ridx = buffer.ReaderIndex;
            int rbytes = this.ActualReadableBytes;
            int delimPos = buffer.IndexOf(ridx, ridx + rbytes, 0xFF);
            if (delimPos == -1)
            {
                // Frame delimiter (0xFF) not found
                if (rbytes > this.maxFrameSize)
                {
                    // Frame length exceeded the maximum
                    throw new TooLongFrameException(nameof(WebSocket00FrameDecoder));
                }
                else
                {
                    // Wait until more data is received
                    return null;
                }
            }

            int frameSize = delimPos - ridx;
            if (frameSize > this.maxFrameSize)
            {
                throw new TooLongFrameException(nameof(WebSocket00FrameDecoder));
            }

            IByteBuffer binaryData = ReadBytes(ctx.Allocator, buffer, frameSize);
            buffer.SkipBytes(1);

            int ffDelimPos = binaryData.IndexOf(binaryData.ReaderIndex, binaryData.WriterIndex, 0xFF);
            if (ffDelimPos >= 0)
            {
                binaryData.Release();
                throw new ArgumentException("a text frame should not contain 0xFF.");
            }

            return new TextWebSocketFrame(binaryData);
        }
    }
}
