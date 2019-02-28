// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using DotNetty.Buffers;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class WebSocket08FrameEncoder : MessageToMessageEncoder<WebSocketFrame>, IWebSocketFrameEncoder
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<WebSocket08FrameEncoder>();

        const byte OpcodeCont = 0x0;
        const byte OpcodeText = 0x1;
        const byte OpcodeBinary = 0x2;
        const byte OpcodeClose = 0x8;
        const byte OpcodePing = 0x9;
        const byte OpcodePong = 0xA;

        ///
        //  The size threshold for gathering writes. Non-Masked messages bigger than this size will be be sent fragmented as
        //  a header and a content ByteBuf whereas messages smaller than the size will be merged into a single buffer and
        //  sent at once.
        //  Masked messages will always be sent at once.
        // 
        const int GatheringWriteThreshold = 1024;

        readonly bool maskPayload;
        readonly Random random;

        public WebSocket08FrameEncoder(bool maskPayload)
        {
            this.maskPayload = maskPayload;
            this.random = new Random();
        }

        protected override unsafe void Encode(IChannelHandlerContext ctx, WebSocketFrame msg, List<object> output)
        {
            IByteBuffer data = msg.Content;
            var mask = stackalloc byte[4];

            byte opcode = 0;
            if (msg is TextWebSocketFrame)
            {
                opcode = OpcodeText;
            }
            else if (msg is PingWebSocketFrame)
            {
                opcode = OpcodePing;
            }
            else if (msg is PongWebSocketFrame)
            {
                opcode = OpcodePong;
            }
            else if (msg is CloseWebSocketFrame)
            {
                opcode = OpcodeClose;
            }
            else if (msg is BinaryWebSocketFrame)
            {
                opcode = OpcodeBinary;
            }
            else if (msg is ContinuationWebSocketFrame)
            {
                opcode = OpcodeCont;
            }
            else
            {
                ThrowNotSupportedException(msg);
            }

            int length = data.ReadableBytes;

            if (Logger.DebugEnabled)
            {
                Logger.Debug($"Encoding WebSocket Frame opCode={opcode} length={length}");
            }

            int b0 = 0;
            if (msg.IsFinalFragment)
            {
                b0 |= 1 << 7;
            }
            b0 |= msg.Rsv % 8 << 4;
            b0 |= opcode % 128;

            if (opcode == OpcodePing && length > 125)
            {
                ThrowTooLongFrameException(length);
            }

            bool release = true;
            IByteBuffer buf = null;

            try
            {
                int maskLength = this.maskPayload ? 4 : 0;
                if (length <= 125)
                {
                    int size = 2 + maskLength;
                    if (this.maskPayload || length <= GatheringWriteThreshold)
                    {
                        size += length;
                    }
                    buf = ctx.Allocator.Buffer(size);
                    buf.WriteByte(b0);
                    byte b = (byte)(this.maskPayload ? 0x80 | (byte)length : (byte)length);
                    buf.WriteByte(b);
                }
                else if (length <= 0xFFFF)
                {
                    int size = 4 + maskLength;
                    if (this.maskPayload || length <= GatheringWriteThreshold)
                    {
                        size += length;
                    }
                    buf = ctx.Allocator.Buffer(size);
                    buf.WriteByte(b0);
                    buf.WriteByte(this.maskPayload ? 0xFE : 126);
                    buf.WriteByte(length.RightUShift(8) & 0xFF);
                    buf.WriteByte(length & 0xFF);
                }
                else
                {
                    int size = 10 + maskLength;
                    if (this.maskPayload || length <= GatheringWriteThreshold)
                    {
                        size += length;
                    }
                    buf = ctx.Allocator.Buffer(size);
                    buf.WriteByte(b0);
                    buf.WriteByte(this.maskPayload ? 0xFF : 127);
                    buf.WriteLong(length);
                }

                // Write payload
                if (this.maskPayload)
                {
                    int intMask = (this.random.Next() * int.MaxValue);

                    // Mask bytes in BE
                    uint unsignedValue = (uint)intMask;
                    *mask = (byte)(unsignedValue >> 24);
                    *(mask + 1) = (byte)(unsignedValue >> 16);
                    *(mask + 2) = (byte)(unsignedValue >> 8);
                    *(mask + 3) = (byte)unsignedValue;

                    // Mask in BE
                    buf.WriteInt(intMask);

                    int counter = 0;
                    int i = data.ReaderIndex;
                    int end = data.WriterIndex;

                    for (; i + 3 < end; i += 4)
                    {
                        int intData = data.GetInt(i);
                        buf.WriteInt(intData ^ intMask);
                    }
                    for (; i < end; i++)
                    {
                        byte byteData = data.GetByte(i);
                        buf.WriteByte(byteData ^ mask[counter++ % 4]);
                    }
                    output.Add(buf);
                }
                else
                {
                    if (buf.WritableBytes >= data.ReadableBytes)
                    {
                        // merge buffers as this is cheaper then a gathering write if the payload is small enough
                        buf.WriteBytes(data);
                        output.Add(buf);
                    }
                    else
                    {
                        output.Add(buf);
                        output.Add(data.Retain());
                    }
                }
                release = false;
            }
            finally 
            {
                if (release)
                {
                    buf?.Release();
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowTooLongFrameException(int length)
        {
            throw GetTooLongFrameException();

            TooLongFrameException GetTooLongFrameException()
            {
                return new TooLongFrameException(string.Format("invalid payload for PING (payload length must be <= 125, was {0}", length));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowNotSupportedException(WebSocketFrame msg)
        {
            throw GetNotSupportedException();

            NotSupportedException GetNotSupportedException()
            {
                return new NotSupportedException(string.Format("Cannot encode frame of type: {0}", msg.GetType().Name));
            }
        }
    }
}
