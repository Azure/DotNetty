// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace DotNetty.Codecs.Redis
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Redis.Messages;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public sealed class RedisDecoder : ByteToMessageDecoder
    {
        readonly ToPositiveLongProcessor toPositiveLongProcessor = new ToPositiveLongProcessor();

        readonly bool decodeInlineCommands;
        readonly int maxInlineMessageLength;
        readonly IRedisMessagePool messagePool;

        // current decoding states
        State state = State.DecodeType;
        RedisMessageType type;
        int remainingBulkLength;

        enum State
        {
            DecodeType,
            DecodeInline, // SIMPLE_STRING, ERROR, INTEGER
            DecodeLength, // BULK_STRING, ARRAY_HEADER
            DecodeBulkStringEol,
            DecodeBulkStringContent,
        }

        public RedisDecoder() : this(false)
        {
        }

        public RedisDecoder(bool decodeInlineCommands)
            : this(RedisConstants.RedisInlineMessageMaxLength, FixedRedisMessagePool.Instance, decodeInlineCommands)
        {
        }

        public RedisDecoder(int maxInlineMessageLength, IRedisMessagePool messagePool, bool decodeInlineCommands)
        {
            if (maxInlineMessageLength <= 0 || maxInlineMessageLength > RedisConstants.RedisMessageMaxLength)
            {
                throw new RedisCodecException($"maxInlineMessageLength: {maxInlineMessageLength} (expected: <= {RedisConstants.RedisMessageMaxLength})");
            }

            this.maxInlineMessageLength = maxInlineMessageLength;
            this.messagePool = messagePool;
            this.decodeInlineCommands = decodeInlineCommands;
        }

        protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            try
            {
                for (;;)
                {
                    switch (this.state)
                    {
                        case State.DecodeType:
                            if (!this.DecodeType(input))
                            {
                                return;
                            }

                            break;
                        case State.DecodeInline:
                            if (!this.DecodeInline(input, output))
                            {
                                return;
                            }

                            break;
                        case State.DecodeLength:
                            if (!this.DecodeLength(input, output))
                            {
                                return;
                            }

                            break;
                        case State.DecodeBulkStringEol:
                            if (!this.DecodeBulkStringEndOfLine(input, output))
                            {
                                return;
                            }

                            break;
                        case State.DecodeBulkStringContent:
                            if (!this.DecodeBulkStringContent(input, output))
                            {
                                return;
                            }

                            break;
                        default:
                            throw new RedisCodecException($"Unknown state: {this.state}");
                    }
                }
            }
            catch (RedisCodecException)
            {
                this.ResetDecoder();
                throw;
            }
            catch (Exception exception)
            {
                this.ResetDecoder();
                throw new RedisCodecException(exception);
            }
        }

        void ResetDecoder()
        {
            this.state = State.DecodeType;
            this.remainingBulkLength = 0;
        }

        bool DecodeType(IByteBuffer input)
        {
            if (!input.IsReadable())
            {
                return false;
            }

            this.type = RedisMessageType.ReadFrom(input, this.decodeInlineCommands);
            this.state = this.type.Inline ? State.DecodeInline : State.DecodeLength;
            return true;
        }

        bool DecodeInline(IByteBuffer input, List<object> output)
        {
            IByteBuffer lineBytes = ReadLine(input);
            if (lineBytes == null)
            {
                if (input.ReadableBytes > this.maxInlineMessageLength)
                {
                    throw new RedisCodecException($"length: {input.ReadableBytes} (expected: <= {this.maxInlineMessageLength})");
                }

                return false;
            }

            output.Add(this.NewInlineRedisMessage(this.type, lineBytes));
            this.ResetDecoder();
            return true;
        }

        bool DecodeLength(IByteBuffer input, List<object> output)
        {
            IByteBuffer lineByteBuf = ReadLine(input);
            if (lineByteBuf == null)
            {
                return false;
            }

            long length = this.ParseRedisNumber(lineByteBuf);
            if (length < RedisConstants.NullValue)
            {
                throw new RedisCodecException($"length: {length} (expected: >= {RedisConstants.NullValue})");
            }

            if (this.type == RedisMessageType.ArrayHeader)
            {
                output.Add(new ArrayHeaderRedisMessage(length));
                this.ResetDecoder();
                return true;
            }

            if (this.type == RedisMessageType.BulkString)
            {
                if (length > RedisConstants.RedisMessageMaxLength)
                {
                    throw new RedisCodecException($"length: {length} (expected: <= {RedisConstants.RedisMessageMaxLength})");
                }
                this.remainingBulkLength = (int)length; // range(int) is already checked.
                return this.DecodeBulkString(input, output);
            }

            throw new RedisCodecException($"bad type: {this.type}");
        }

        bool DecodeBulkString(IByteBuffer input, List<object> output)
        {
            if (this.remainingBulkLength == RedisConstants.NullValue) // $-1\r\n
            {
                output.Add(FullBulkStringRedisMessage.Null);
                this.ResetDecoder();
                return true;
            }
            else if (this.remainingBulkLength == 0)
            {
                this.state = State.DecodeBulkStringEol;
                return this.DecodeBulkStringEndOfLine(input, output);

            }

            // expectedBulkLength is always positive.
            output.Add(new BulkStringHeaderRedisMessage(this.remainingBulkLength));
            this.state = State.DecodeBulkStringContent;
            return this.DecodeBulkStringContent(input, output);
        }

        // $0\r\n <here> \r\n
        bool DecodeBulkStringEndOfLine(IByteBuffer input, List<object> output)
        {
            if (input.ReadableBytes < RedisConstants.EndOfLineLength)
            {
                return false;
            }

            ReadEndOfLine(input);
            output.Add(FullBulkStringRedisMessage.Empty);
            this.ResetDecoder();
            return true;
        }

        // ${expectedBulkLength}\r\n <here> {data...}\r\n
        bool DecodeBulkStringContent(IByteBuffer input, List<object> output)
        {
            int readableBytes = input.ReadableBytes;
            if (readableBytes == 0 || this.remainingBulkLength == 0 && readableBytes < RedisConstants.EndOfLineLength)
            {
                return false;
            }

            // if this is last frame.
            if (readableBytes >= this.remainingBulkLength + RedisConstants.EndOfLineLength)
            {
                IByteBuffer content = input.ReadSlice(this.remainingBulkLength);
                ReadEndOfLine(input);
                // Only call retain after readEndOfLine(...) as the method may throw an exception.
                output.Add(new DefaultLastBulkStringRedisContent((IByteBuffer)content.Retain()));
                this.ResetDecoder();
                return true;
            }

            // chunked write.
            int toRead = Math.Min(this.remainingBulkLength, readableBytes);
            this.remainingBulkLength -= toRead;
            output.Add(new DefaultBulkStringRedisContent((IByteBuffer)input.ReadSlice(toRead).Retain()));
            return true;
        }

        static void ReadEndOfLine(IByteBuffer input)
        {
            short delim = input.ReadShort();
            if (RedisConstants.EndOfLineShort == delim)
            {
                return;
            }
            byte[] bytes = RedisCodecUtil.ShortToBytes(delim);
            throw new RedisCodecException($"delimiter: [{bytes[0]},{bytes[1]}] (expected: \\r\\n)");
        }

        IRedisMessage NewInlineRedisMessage(RedisMessageType messageType, IByteBuffer content)
        {
            if (messageType == RedisMessageType.InlineCommand)
            {
                return new InlineCommandRedisMessage(content.ToString(Encoding.UTF8));
            }
            else if (messageType == RedisMessageType.SimpleString)
            {
                if (this.messagePool.TryGetSimpleString(content, out SimpleStringRedisMessage cached))
                {
                    return cached;
                }
                return new SimpleStringRedisMessage(content.ToString(Encoding.UTF8));
            }
            else if (messageType == RedisMessageType.Error)
            {
                if (this.messagePool.TryGetError(content, out ErrorRedisMessage cached))
                {
                    return cached;
                }
                return new ErrorRedisMessage(content.ToString(Encoding.UTF8));
            }
            else if (messageType == RedisMessageType.Integer)
            {
                if (this.messagePool.TryGetInteger(content, out IntegerRedisMessage cached))
                {
                    return cached;
                }
                return new IntegerRedisMessage(this.ParseRedisNumber(content));
            }

            throw new RedisCodecException($"bad type: {messageType}");
        }

        static IByteBuffer ReadLine(IByteBuffer input)
        {
            if (!input.IsReadable(RedisConstants.EndOfLineLength))
            {
                return null;
            }

            int lfIndex = input.ForEachByte(ByteProcessor.FindLF);
            if (lfIndex < 0)
            {
                return null;
            }

            IByteBuffer data = input.ReadSlice(lfIndex - input.ReaderIndex - 1); // `-1` is for CR
            ReadEndOfLine(input); // validate CR LF
            return data;
        }

        long ParseRedisNumber(IByteBuffer byteBuf)
        {
            int readableBytes = byteBuf.ReadableBytes;
            bool negative = readableBytes > 0 && byteBuf.GetByte(byteBuf.ReaderIndex) == '-';
            int extraOneByteForNegative = negative ? 1 : 0;
            if (readableBytes <= extraOneByteForNegative)
            {
                throw new RedisCodecException($"no number to parse: {byteBuf.ToString(Encoding.ASCII)}");
            }
            if (readableBytes > RedisConstants.PositiveLongMaxLength + extraOneByteForNegative)
            {
                throw new RedisCodecException($"too many characters to be a valid RESP Integer: {byteBuf.ToString(Encoding.ASCII)}");
            }
            if (negative)
            {
                return -this.ParsePositiveNumber(byteBuf.SkipBytes(extraOneByteForNegative));
            }
            return this.ParsePositiveNumber(byteBuf);
        }

        long ParsePositiveNumber(IByteBuffer byteBuffer)
        {
            this.toPositiveLongProcessor.Reset();
            byteBuffer.ForEachByte(this.toPositiveLongProcessor);
            return this.toPositiveLongProcessor.Content;
        }

        sealed class ToPositiveLongProcessor : IByteProcessor
        {
            long result;

            public bool Process(byte value)
            {
                if (!char.IsDigit((char)value))
                {
                    throw new RedisCodecException($"Bad byte in number: {value}, expecting digits from 0 to 9");
                }

                this.result = this.result * 10 + (value - '0');
                return true;
            }

            public long Content => this.result;

            public void Reset() => this.result = 0;
        }
    }
}