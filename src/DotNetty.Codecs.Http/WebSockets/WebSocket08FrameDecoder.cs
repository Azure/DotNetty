// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable UseStringInterpolation
namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Channels;

    using static Buffers.ByteBufferUtil;

    public class WebSocket08FrameDecoder : ByteToMessageDecoder, IWebSocketFrameDecoder
    {
        enum State
        {
            ReadingFirst,
            ReadingSecond,
            ReadingSize,
            MaskingKey,
            Payload,
            Corrupt
        }

        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<WebSocket08FrameDecoder>();

        const byte OpcodeCont = 0x0;
        const byte OpcodeText = 0x1;
        const byte OpcodeBinary = 0x2;
        const byte OpcodeClose = 0x8;
        const byte OpcodePing = 0x9;
        const byte OpcodePong = 0xA;

        readonly long maxFramePayloadLength;
        readonly bool allowExtensions;
        readonly bool expectMaskedFrames;
        readonly bool allowMaskMismatch;

        int fragmentedFramesCount;
        bool frameFinalFlag;
        bool frameMasked;
        int frameRsv;
        int frameOpcode;
        long framePayloadLength;
        byte[] maskingKey;
        int framePayloadLen1;
        bool receivedClosingHandshake;
        State state = State.ReadingFirst;

        public WebSocket08FrameDecoder(bool expectMaskedFrames, bool allowExtensions, int maxFramePayloadLength)
            : this(expectMaskedFrames, allowExtensions, maxFramePayloadLength, false)
        {
        }

        public WebSocket08FrameDecoder(bool expectMaskedFrames, bool allowExtensions, int maxFramePayloadLength, bool allowMaskMismatch)
        {
            this.expectMaskedFrames = expectMaskedFrames;
            this.allowMaskMismatch = allowMaskMismatch;
            this.allowExtensions = allowExtensions;
            this.maxFramePayloadLength = maxFramePayloadLength;
        }

        protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            // Discard all data received if closing handshake was received before.
            if (this.receivedClosingHandshake)
            {
                input.SkipBytes(this.ActualReadableBytes);
                return;
            }

            switch (this.state)
            {
                case State.ReadingFirst:
                    if (!input.IsReadable())
                    {
                        return;
                    }

                    this.framePayloadLength = 0;

                    // FIN, RSV, OPCODE
                    byte b = input.ReadByte();
                    this.frameFinalFlag = (b & 0x80) != 0;
                    this.frameRsv = (b & 0x70) >> 4;
                    this.frameOpcode = b & 0x0F;

                    if (Logger.DebugEnabled)
                    {
                        Logger.Debug("Decoding WebSocket Frame opCode={}", this.frameOpcode);
                    }

                    this.state = State.ReadingSecond;
                    goto case State.ReadingSecond;
                case State.ReadingSecond:
                    if (!input.IsReadable())
                    {
                        return;
                    }

                    // MASK, PAYLOAD LEN 1
                    b = input.ReadByte();
                    this.frameMasked = (b & 0x80) != 0;
                    this.framePayloadLen1 = b & 0x7F;

                    if (this.frameRsv != 0 && !this.allowExtensions)
                    {
                        this.ProtocolViolation(context, $"RSV != 0 and no extension negotiated, RSV:{this.frameRsv}");
                        return;
                    }

                    if (!this.allowMaskMismatch && this.expectMaskedFrames != this.frameMasked)
                    {
                        this.ProtocolViolation(context, "received a frame that is not masked as expected");
                        return;
                    }

                    // control frame (have MSB in opcode set)
                    if (this.frameOpcode > 7)
                    {
                        // control frames MUST NOT be fragmented
                        if (!this.frameFinalFlag)
                        {
                            this.ProtocolViolation(context, "fragmented control frame");
                            return;
                        }

                        // control frames MUST have payload 125 octets or less
                        if (this.framePayloadLen1 > 125)
                        {
                            this.ProtocolViolation(context, "control frame with payload length > 125 octets");
                            return;
                        }

                        // check for reserved control frame opcodes
                        if (!(this.frameOpcode == OpcodeClose 
                            || this.frameOpcode == OpcodePing
                            || this.frameOpcode == OpcodePong))
                        {
                            this.ProtocolViolation(context, $"control frame using reserved opcode {this.frameOpcode}");
                            return;
                        }

                        // close frame : if there is a body, the first two bytes of the
                        // body MUST be a 2-byte unsigned integer (in network byte
                        // order) representing a getStatus code
                        if (this.frameOpcode == 8 && this.framePayloadLen1 == 1)
                        {
                            this.ProtocolViolation(context, "received close control frame with payload len 1");
                            return;
                        }
                    }
                    else // data frame
                    {
                        // check for reserved data frame opcodes
                        if (!(this.frameOpcode == OpcodeCont || this.frameOpcode == OpcodeText
                            || this.frameOpcode == OpcodeBinary))
                        {
                            this.ProtocolViolation(context, $"data frame using reserved opcode {this.frameOpcode}");
                            return;
                        }

                        // check opcode vs message fragmentation state 1/2
                        if (this.fragmentedFramesCount == 0 && this.frameOpcode == OpcodeCont)
                        {
                            this.ProtocolViolation(context, "received continuation data frame outside fragmented message");
                            return;
                        }

                        // check opcode vs message fragmentation state 2/2
                        if (this.fragmentedFramesCount != 0 && this.frameOpcode != OpcodeCont && this.frameOpcode != OpcodePing)
                        {
                            this.ProtocolViolation(context, "received non-continuation data frame while inside fragmented message");
                            return;
                        }
                    }

                    this.state = State.ReadingSize;
                    goto case State.ReadingSize;
                case State.ReadingSize:
                    // Read frame payload length
                    if (this.framePayloadLen1 == 126)
                    {
                        if (input.ReadableBytes < 2)
                        {
                            return;
                        }
                        this.framePayloadLength = input.ReadUnsignedShort();
                        if (this.framePayloadLength < 126)
                        {
                            this.ProtocolViolation(context, "invalid data frame length (not using minimal length encoding)");
                            return;
                        }
                    }
                    else if (this.framePayloadLen1 == 127)
                    {
                        if (input.ReadableBytes < 8)
                        {
                            return;
                        }
                        this.framePayloadLength = input.ReadLong();
                        // TODO: check if it's bigger than 0x7FFFFFFFFFFFFFFF, Maybe
                        // just check if it's negative?

                        if (this.framePayloadLength < 65536)
                        {
                            this.ProtocolViolation(context, "invalid data frame length (not using minimal length encoding)");
                            return;
                        }
                    }
                    else
                    {
                        this.framePayloadLength = this.framePayloadLen1;
                    }

                    if (this.framePayloadLength > this.maxFramePayloadLength)
                    {
                        this.ProtocolViolation(context, $"Max frame length of {this.maxFramePayloadLength} has been exceeded.");
                        return;
                    }

                    if (Logger.DebugEnabled)
                    {
                        Logger.Debug("Decoding WebSocket Frame length={}", this.framePayloadLength);
                    }

                    this.state = State.MaskingKey;
                    goto case State.MaskingKey;
                case State.MaskingKey:
                    if (this.frameMasked)
                    {
                        if (input.ReadableBytes < 4)
                        {
                            return;
                        }
                        if (this.maskingKey == null)
                        {
                            this.maskingKey = new byte[4];
                        }
                        input.ReadBytes(this.maskingKey);
                    }
                    this.state = State.Payload;
                    goto case State.Payload;
                case State.Payload:
                    if (input.ReadableBytes < this.framePayloadLength)
                    {
                        return;
                    }

                    IByteBuffer payloadBuffer = null;
                    try
                    {
                        payloadBuffer = ReadBytes(context.Allocator, input, ToFrameLength(this.framePayloadLength));

                        // Now we have all the data, the next checkpoint must be the next
                        // frame
                        this.state = State.ReadingFirst;

                        // Unmask data if needed
                        if (this.frameMasked)
                        {
                            this.Unmask(payloadBuffer);
                        }

                        // Processing ping/pong/close frames because they cannot be
                        // fragmented
                        if (this.frameOpcode == OpcodePing)
                        {
                            output.Add(new PingWebSocketFrame(this.frameFinalFlag, this.frameRsv, payloadBuffer));
                            payloadBuffer = null;
                            return;
                        }
                        if (this.frameOpcode == OpcodePong)
                        {
                            output.Add(new PongWebSocketFrame(this.frameFinalFlag, this.frameRsv, payloadBuffer));
                            payloadBuffer = null;
                            return;
                        }
                        if (this.frameOpcode == OpcodeClose)
                        {
                            this.receivedClosingHandshake = true;
                            this.CheckCloseFrameBody(context, payloadBuffer);
                            output.Add(new CloseWebSocketFrame(this.frameFinalFlag, this.frameRsv, payloadBuffer));
                            payloadBuffer = null;
                            return;
                        }

                        // Processing for possible fragmented messages for text and binary
                        // frames
                        if (this.frameFinalFlag)
                        {
                            // Final frame of the sequence. Apparently ping frames are
                            // allowed in the middle of a fragmented message
                            if (this.frameOpcode != OpcodePing)
                            {
                                this.fragmentedFramesCount = 0;
                            }
                        }
                        else
                        {
                            // Increment counter
                            this.fragmentedFramesCount++;
                        }

                        // Return the frame
                        if (this.frameOpcode == OpcodeText)
                        {
                            output.Add(new TextWebSocketFrame(this.frameFinalFlag, this.frameRsv, payloadBuffer));
                            payloadBuffer = null;
                            return;
                        }
                        else if (this.frameOpcode == OpcodeBinary)
                        {
                            output.Add(new BinaryWebSocketFrame(this.frameFinalFlag, this.frameRsv, payloadBuffer));
                            payloadBuffer = null;
                            return;
                        }
                        else if (this.frameOpcode == OpcodeCont)
                        {
                            output.Add(new ContinuationWebSocketFrame(this.frameFinalFlag, this.frameRsv, payloadBuffer));
                            payloadBuffer = null;
                            return;
                        }
                        else
                        {
                            ThrowNotSupportedException(this.frameOpcode);
                            break;
                        }
                    }
                    finally
                    {
                        payloadBuffer?.Release();
                    }
                case State.Corrupt:
                    if (input.IsReadable())
                    {
                        // If we don't keep reading Netty will throw an exception saying
                        // we can't return null if no bytes read and state not changed.
                        input.ReadByte();
                    }
                    return;
                default:
                    throw new Exception("Shouldn't reach here.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowNotSupportedException(int frameOpcode)
        {
            throw GetNotSupportedException();

            NotSupportedException GetNotSupportedException()
            {
                return new NotSupportedException($"Cannot decode web socket frame with opcode: {frameOpcode}");
            }
        }

        void Unmask(IByteBuffer frame)
        {
            int i = frame.ReaderIndex;
            int end = frame.WriterIndex;

            int intMask = (this.maskingKey[0] << 24)
                  | (this.maskingKey[1] << 16)
                  | (this.maskingKey[2] << 8)
                  | this.maskingKey[3];

            for (; i + 3 < end; i += 4)
            {
                int unmasked = frame.GetInt(i) ^ intMask;
                frame.SetInt(i, unmasked);
            }
            for (; i < end; i++)
            {
                frame.SetByte(i, frame.GetByte(i) ^ this.maskingKey[i % 4]);
            }
        }

        void ProtocolViolation(IChannelHandlerContext ctx, string reason) => this.ProtocolViolation(ctx, new CorruptedFrameException(reason));

        void ProtocolViolation(IChannelHandlerContext ctx, CorruptedFrameException ex)
        {
            this.state = State.Corrupt;
            if (ctx.Channel.Active)
            {
                object closeMessage;
                if (this.receivedClosingHandshake)
                {
                    closeMessage = Unpooled.Empty;
                }
                else
                {
                    closeMessage = new CloseWebSocketFrame(1002, null);
                }
                ctx.WriteAndFlushAsync(closeMessage)
                    .ContinueWith((t, c) => ((IChannel)c).CloseAsync(),
                        ctx.Channel, TaskContinuationOptions.ExecuteSynchronously);
            }
            throw ex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int ToFrameLength(long l)
        {
            if (l > int.MaxValue)
            {
                ThrowTooLongFrameException(l);
            }
            return (int)l;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowTooLongFrameException(long l)
        {
            throw GetTooLongFrameException();

            TooLongFrameException GetTooLongFrameException()
            {
                return new TooLongFrameException(string.Format("Length: {0}", l));
            }
        }

        protected void CheckCloseFrameBody(IChannelHandlerContext ctx, IByteBuffer buffer)
        {
            if (buffer == null || !buffer.IsReadable())
            {
                return;
            }
            if (buffer.ReadableBytes == 1)
            {
                this.ProtocolViolation(ctx, "Invalid close frame body");
            }

            // Save reader index
            int idx = buffer.ReaderIndex;
            buffer.SetReaderIndex(0);

            // Must have 2 byte integer within the valid range
            int statusCode = buffer.ReadShort();
            if (statusCode >= 0 && statusCode <= 999 || statusCode >= 1004 && statusCode <= 1006
                || statusCode >= 1012 && statusCode <= 2999)
            {
                this.ProtocolViolation(ctx, $"Invalid close frame getStatus code: {statusCode}");
            }

            // May have UTF-8 message
            if (buffer.IsReadable())
            {
                try
                {
                    new Utf8Validator().Check(buffer);
                }
                catch (CorruptedFrameException ex)
                {
                    this.ProtocolViolation(ctx, ex);
                }
            }

            // Restore reader index
            buffer.SetReaderIndex(idx);
        }
    }
}
