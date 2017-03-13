// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Protobuf
{
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    ///
    /// A decoder that splits the received {@link ByteBuf}s dynamically by the
    /// value of the Google Protocol Buffers
    /// http://code.google.com/apis/protocolbuffers/docs/encoding.html#varints
    /// Base 128 Varints integer length field in the message. 
    /// For example:
    /// 
    /// BEFORE DECODE (302 bytes)       AFTER DECODE (300 bytes)
    /// +--------+---------------+      +---------------+
    /// | Length | Protobuf Data |----->| Protobuf Data |
    /// | 0xAC02 |  (300 bytes)  |      |  (300 bytes)  |
    /// +--------+---------------+      +---------------+
    ///
    public sealed class ProtobufVarint32FrameDecoder : ByteToMessageDecoder
    {
        // todo: maxFrameLength + safe skip + fail-fast option (just like LengthFieldBasedFrameDecoder)

        protected internal override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            input.MarkReaderIndex();

            int preIndex = input.ReaderIndex;
            int length = ReadRawVarint32(input);

            if (preIndex == input.ReaderIndex)
            {
                return;
            }

            if (length < 0)
            {
                throw new CorruptedFrameException($"Negative length: {length}");
            }

            if (input.ReadableBytes < length)
            {
                input.ResetReaderIndex();
            }
            else
            {
                IByteBuffer byteBuffer = input.ReadSlice(length);
                output.Add(byteBuffer.Retain());
            }
        }

        static int ReadRawVarint32(IByteBuffer buffer)
        {
            Contract.Requires(buffer != null);

            if (!buffer.IsReadable())
            {
                return 0;
            }

            buffer.MarkReaderIndex();
            byte rawByte = buffer.ReadByte();
            if (rawByte < 128)
            {
                return rawByte;
            }

            int result = rawByte & 127;
            if (!buffer.IsReadable())
            {
                buffer.ResetReaderIndex();
                return 0;
            }

            rawByte = buffer.ReadByte();
            if (rawByte < 128)
            {
                result |= rawByte << 7;
            }
            else
            {
                result |= (rawByte & 127) << 7;
                if (!buffer.IsReadable())
                {
                    buffer.ResetReaderIndex();
                    return 0;
                }

                rawByte = buffer.ReadByte();
                if (rawByte < 128)
                {
                    result |= rawByte << 14;
                }
                else
                {
                    result |= (rawByte & 127) << 14;
                    if (!buffer.IsReadable())
                    {
                        buffer.ResetReaderIndex();
                        return 0;
                    }

                    rawByte = buffer.ReadByte();
                    if (rawByte < 128)
                    {
                        result |= rawByte << 21;
                    }
                    else
                    {
                        result |= (rawByte & 127) << 21;
                        if (!buffer.IsReadable())
                        {
                            buffer.ResetReaderIndex();
                            return 0;
                        }

                        rawByte = buffer.ReadByte();
                        result |= rawByte << 28;

                        if (rawByte >= 128)
                        {
                            throw new CorruptedFrameException("Malformed varint.");
                        }
                    }
                }
            }

            return result;
        }
    }
}
