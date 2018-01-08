// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.HaProxy
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /**
     * Decodes an HAProxy proxy protocol header
     *
     * @see <a href="http://haproxy.1wt.eu/download/1.5/doc/proxy-protocol.txt">Proxy Protocol Specification</a>
     */
    public class HAProxyMessageDecoder : ByteToMessageDecoder
    {
        /**
         * Maximum possible length of a v1 proxy protocol header per spec
         */
        const int V1_MAX_LENGTH = 108;

        /**
         * Maximum possible length of a v2 proxy protocol header (fixed 16 bytes + max unsigned short)
         */
        const int V2_MAX_LENGTH = 16 + 65535;

        /**
         * Minimum possible length of a fully functioning v2 proxy protocol header (fixed 16 bytes + v2 address info space)
         */
        const int V2_MIN_LENGTH = 16 + 216;

        /**
         * Maximum possible length for v2 additional TLV data (max unsigned short - max v2 address info space)
         */
        const int V2_MAX_TLV = 65535 - 216;

        /**
         * Version 1 header delimiter is always '\r\n' per spec
         */
        const int DELIMITER_LENGTH = 2;

        /**
         * Binary header prefix
         */
        static readonly byte[] BINARY_PREFIX = {
            (byte) 0x0D,
            (byte) 0x0A,
            (byte) 0x0D,
            (byte) 0x0A,
            (byte) 0x00,
            (byte) 0x0D,
            (byte) 0x0A,
            (byte) 0x51,
            (byte) 0x55,
            (byte) 0x49,
            (byte) 0x54,
            (byte) 0x0A
        };

        static readonly byte[] TEXT_PREFIX = {
            (byte) 'P',
            (byte) 'R',
            (byte) 'O',
            (byte) 'X',
            (byte) 'Y',
        };

        /**
         * Binary header prefix length
         */
        static readonly int BINARY_PREFIX_LENGTH = BINARY_PREFIX.Length;

        /**
         * {@link ProtocolDetectionResult} for {@link HAProxyProtocolVersion#V1}.
         */
        static readonly ProtocolDetectionResult<HAProxyProtocolVersion> DETECTION_RESULT_V1 = ProtocolDetectionResult<HAProxyProtocolVersion>.Detected(HAProxyProtocolVersion.V1);

        /**
         * {@link ProtocolDetectionResult} for {@link HAProxyProtocolVersion#V2}.
         */
        static readonly ProtocolDetectionResult<HAProxyProtocolVersion> DETECTION_RESULT_V2 = ProtocolDetectionResult<HAProxyProtocolVersion>.Detected(HAProxyProtocolVersion.V2);

        /**
         * {@code true} if we're discarding input because we're already over maxLength
         */
        bool discarding;

        /**
         * Number of discarded bytes
         */
        int discardedBytes;

        /**
         * {@code true} if we're finished decoding the proxy protocol header
         */
        bool finished;

        /**
         * Protocol specification version
         */
        int version = -1;

        /**
         * The latest v2 spec (2014/05/18) allows for additional data to be sent in the proxy protocol header beyond the
         * address information block so now we need a configurable max header size
         */
        readonly int v2MaxHeaderSize;

        /**
         * Creates a new decoder with no additional data (TLV) restrictions
         */
        public HAProxyMessageDecoder()
        {
            v2MaxHeaderSize = V2_MAX_LENGTH;
            SingleDecode = true;
        }

        /**
         * Creates a new decoder with restricted additional data (TLV) size
         * <p>
         * <b>Note:</b> limiting TLV size only affects processing of v2, binary headers. Also, as allowed by the 1.5 spec
         * TLV data is currently ignored. For maximum performance it would be best to configure your upstream proxy host to
         * <b>NOT</b> send TLV data and instantiate with a max TLV size of {@code 0}.
         * </p>
         *
         * @param maxTlvSize maximum number of bytes allowed for additional data (Type-Length-Value vectors) in a v2 header
         */
        public HAProxyMessageDecoder(int maxTlvSize)
        {
            SingleDecode = true;
            if (maxTlvSize < 1)
            {
                v2MaxHeaderSize = V2_MIN_LENGTH;
            }
            else if (maxTlvSize > V2_MAX_TLV)
            {
                v2MaxHeaderSize = V2_MAX_LENGTH;
            }
            else
            {
                int calcMax = maxTlvSize + V2_MIN_LENGTH;
                if (calcMax > V2_MAX_LENGTH)
                {
                    v2MaxHeaderSize = V2_MAX_LENGTH;
                }
                else
                {
                    v2MaxHeaderSize = calcMax;
                }
            }
        }

        /**
         * Returns the proxy protocol specification version in the buffer if the version is found.
         * Returns -1 if no version was found in the buffer.
         */
        private static int FindVersion(IByteBuffer buffer)
        {
            int n = buffer.ReadableBytes;
            // per spec, the version number is found in the 13th byte
            if (n < 13)
            {
                return -1;
            }

            int idx = buffer.ReaderIndex;
            return Match(BINARY_PREFIX, buffer, idx) ? buffer.GetByte(idx + BINARY_PREFIX_LENGTH) : 1;
        }

        /**
         * Returns the index in the buffer of the end of header if found.
         * Returns -1 if no end of header was found in the buffer.
         */
        private static int FindEndOfHeader(IByteBuffer buffer)
        {
            int n = buffer.ReadableBytes;

            // per spec, the 15th and 16th bytes contain the address length in bytes
            if (n < 16)
            {
                return -1;
            }

            int offset = buffer.ReaderIndex + 14;

            // the total header length will be a fixed 16 byte sequence + the dynamic address information block
            int totalHeaderBytes = 16 + buffer.GetUnsignedShort(offset);

            // ensure we actually have the full header available
            if (n >= totalHeaderBytes)
            {
                return totalHeaderBytes;
            }
            else
            {
                return -1;
            }
        }

        /**
         * Returns the index in the buffer of the end of line found.
         * Returns -1 if no end of line was found in the buffer.
         */
        private static int FindEndOfLine(IByteBuffer buffer)
        {
            int n = buffer.WriterIndex;
            for (int i = buffer.ReaderIndex; i < n; i++)
            {
                byte b = buffer.GetByte(i);
                if (b == '\r' && i < n - 1 && buffer.GetByte(i + 1) == '\n')
                {
                    return i;  // \r\n
                }
            }
            return -1;  // Not found.
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            base.ChannelRead(context, message);
            if (finished) {
                context.Channel.Pipeline.Remove(this);
            }
        }

        protected override sealed void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            // determine the specification version
            if (version == -1 && (version = FindVersion(input)) == -1)
            {
                return;
            }

            IByteBuffer decoded;

            if (version == 1)
            {
                decoded = DecodeLine(context, input);
            }
            else
            {
                decoded = DecodeStruct(context, input);
            }

            if (decoded != null)
            {
                finished = true;
                try
                {
                    if (version == 1)
                    {
                            output.Add(HAProxyMessage.DecodeHeader(decoded.ToString(Encoding.ASCII)));
                    }
                    else
                    {
                            output.Add(HAProxyMessage.DecodeHeader(decoded));
                    }
                }
                catch (HAProxyProtocolException e)
                {
                    Fail(context, null, e);
                }
            }
        }

        /**
         * Create a frame out of the {@link ByteBuf} and return it.
         * Based on code from {@link LineBasedFrameDecoder#decode(ChannelHandlerContext, ByteBuf)}.
         *
         * @param ctx     the {@link ChannelHandlerContext} which this {@link HAProxyMessageDecoder} belongs to
         * @param buffer  the {@link ByteBuf} from which to read data
         * @return frame  the {@link ByteBuf} which represent the frame or {@code null} if no frame could
         *                be created
         */
        private IByteBuffer DecodeStruct(IChannelHandlerContext ctx, IByteBuffer buffer)
        {
            int eoh = FindEndOfHeader(buffer);
            if (!discarding)
            {
                if (eoh >= 0)
                {
                    int length = eoh - buffer.ReaderIndex;
                    if (length > v2MaxHeaderSize)
                    {
                        buffer.SetReaderIndex(eoh);
                        FailOverLimit(ctx, length);
                        return null;
                    }
                    return buffer.ReadSlice(length);
                }
                else
                {
                    int length = buffer.ReadableBytes;
                    if (length > v2MaxHeaderSize)
                    {
                        discardedBytes = length;
                        buffer.SkipBytes(length);
                        discarding = true;
                        FailOverLimit(ctx, "over " + discardedBytes);
                    }
                    return null;
                }
            }
            else
            {
                if (eoh >= 0)
                {
                    buffer.SetReaderIndex(eoh);
                    discardedBytes = 0;
                    discarding = false;
                }
                else
                {
                    discardedBytes = buffer.ReadableBytes;
                    buffer.SkipBytes(discardedBytes);
                }
                return null;
            }
        }

        /**
         * Create a frame out of the {@link ByteBuf} and return it.
         * Based on code from {@link LineBasedFrameDecoder#decode(ChannelHandlerContext, ByteBuf)}.
         *
         * @param ctx     the {@link ChannelHandlerContext} which this {@link HAProxyMessageDecoder} belongs to
         * @param buffer  the {@link ByteBuf} from which to read data
         * @return frame  the {@link ByteBuf} which represent the frame or {@code null} if no frame could
         *                be created
         */
        private IByteBuffer DecodeLine(IChannelHandlerContext ctx, IByteBuffer buffer)
        {
            int eol = FindEndOfLine(buffer);
            if (!discarding)
            {
                if (eol >= 0)
                {
                    int length = eol - buffer.ReaderIndex;
                    if (length > V1_MAX_LENGTH)
                    {
                        buffer.SetReaderIndex(eol + DELIMITER_LENGTH);
                        FailOverLimit(ctx, length);
                        return null;
                    }
                    IByteBuffer frame = buffer.ReadSlice(length);
                    buffer.SkipBytes(DELIMITER_LENGTH);
                    return frame;
                }
                else
                {
                    int length = buffer.ReadableBytes;
                    if (length > V1_MAX_LENGTH)
                    {
                        discardedBytes = length;
                        buffer.SkipBytes(length);
                        discarding = true;
                        FailOverLimit(ctx, "over " + discardedBytes);
                    }
                    return null;
                }
            }
            else
            {
                if (eol >= 0)
                {
                    int delimLength = buffer.GetByte(eol) == '\r' ? 2 : 1;
                    buffer.SetReaderIndex(eol + delimLength);
                    discardedBytes = 0;
                    discarding = false;
                }
                else
                {
                    discardedBytes = buffer.ReadableBytes;
                    buffer.SkipBytes(discardedBytes);
                }
                return null;
            }
        }

        private void FailOverLimit(IChannelHandlerContext ctx, int length)
        {
            FailOverLimit(ctx, length.ToString());
        }

        private void FailOverLimit(IChannelHandlerContext ctx, string length)
        {
            int maxLength = version == 1 ? V1_MAX_LENGTH : v2MaxHeaderSize;
            Fail(ctx, "header length (" + length + ") exceeds the allowed maximum (" + maxLength + ')', null);
        }

        private void Fail(IChannelHandlerContext ctx, string errMsg, Exception e)
        {
            finished = true;
            ctx.CloseAsync().RunSynchronously(); // drop connection immediately per spec
            HAProxyProtocolException ppex;
            if (errMsg != null && e != null)
            {
                ppex = new HAProxyProtocolException(errMsg, e);
            }
            else if (errMsg != null)
            {
                ppex = new HAProxyProtocolException(errMsg);
            }
            else if (e != null)
            {
                ppex = new HAProxyProtocolException(e);
            }
            else
            {
                ppex = new HAProxyProtocolException();
            }
            throw ppex;
        }

        /**
         * Returns the {@link ProtocolDetectionResult} for the given {@link ByteBuf}.
         */
        public static ProtocolDetectionResult<HAProxyProtocolVersion> DetectProtocol(IByteBuffer buffer)
        {
            if (buffer.ReadableBytes < 12)
            {
                return ProtocolDetectionResult<HAProxyProtocolVersion>.NeedsMoreData();
            }

            int idx = buffer.ReaderIndex;

            if (Match(BINARY_PREFIX, buffer, idx))
            {
                return DETECTION_RESULT_V2;
            }
            if (Match(TEXT_PREFIX, buffer, idx))
            {
                return DETECTION_RESULT_V1;
            }
            return ProtocolDetectionResult<HAProxyProtocolVersion>.Invalid();
        }

        private static bool Match(byte[] prefix, IByteBuffer buffer, int idx)
        {
            for (int i = 0; i < prefix.Length; i++)
            {
                byte b = buffer.GetByte(idx + i);
                if (b != prefix[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
