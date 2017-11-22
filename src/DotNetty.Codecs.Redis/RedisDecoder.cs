// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Redis.Messages;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public sealed class RedisDecoder : ByteToMessageDecoder
    {
        readonly IRedisMessagePool messagePool;
        readonly int maximumInlineMessageLength;
        readonly ToPositiveLongProcessor toPositiveLongProcessor = new ToPositiveLongProcessor();

        enum State
        {
            DecodeType,
            DecodeInline, // SIMPLE_STRING, ERROR, INTEGER
            DecodeLength, // BULK_STRING, ARRAY_HEADER
            DecodeBulkStringEndOfLine,
            DecodeBulkStringContent,
        }

        // current decoding states
        State state = State.DecodeType;
        RedisMessageType messageType;
        int remainingBulkLength;

        public RedisDecoder()
            : this(RedisConstants.MaximumInlineMessageLength, FixedRedisMessagePool.Default)
        {
        }

        public RedisDecoder(int maximumInlineMessageLength, IRedisMessagePool messagePool)
        {
            Contract.Requires(maximumInlineMessageLength > 0
                && maximumInlineMessageLength <= RedisConstants.MaximumMessageLength);
            Contract.Requires(messagePool != null);

            this.maximumInlineMessageLength = maximumInlineMessageLength;
            this.messagePool = messagePool;
        }

        protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            Contract.Requires(context != null);
            Contract.Requires(input != null);
            Contract.Requires(output != null);

            try
            {
                while (true)
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
                        case State.DecodeBulkStringEndOfLine:
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

        IRedisMessage ReadInlineMessage(RedisMessageType redisMessageType, IByteBuffer byteBuffer)
        {
            Contract.Requires(byteBuffer != null);

            switch (redisMessageType)
            {
                case RedisMessageType.SimpleString:
                    return this.GetSimpleStringMessage(byteBuffer);
                case RedisMessageType.Error:
                    return this.GetErrorMessage(byteBuffer);
                case RedisMessageType.Integer:
                    return this.GetIntegerMessage(byteBuffer);
                default:
                    throw new RedisCodecException(
                        $"Message type {redisMessageType} must be inline messageType of SimpleString, Error or Integer");
            }
        }

        SimpleStringRedisMessage GetSimpleStringMessage(IByteBuffer byteBuffer)
        {
            Contract.Requires(byteBuffer != null);

            SimpleStringRedisMessage message;
            if (!this.messagePool.TryGetMessage(byteBuffer, out message))
            {
                message = new SimpleStringRedisMessage(byteBuffer.ToString(Encoding.UTF8));
            }

            return message;
        }

        ErrorRedisMessage GetErrorMessage(IByteBuffer byteBuffer)
        {
            Contract.Requires(byteBuffer != null);

            ErrorRedisMessage message;
            if (!this.messagePool.TryGetMessage(byteBuffer, out message))
            {
                message = new ErrorRedisMessage(byteBuffer.ToString(Encoding.UTF8));
            }

            return message;
        }

        IntegerRedisMessage GetIntegerMessage(IByteBuffer byteBuffer)
        {
            IntegerRedisMessage message;
            if (!this.messagePool.TryGetMessage(byteBuffer, out message))
            {
                message = new IntegerRedisMessage(this.ParseNumber(byteBuffer));
            }

            return message;
        }

        bool DecodeType(IByteBuffer byteBuffer)
        {
            Contract.Requires(byteBuffer != null);

            if (!byteBuffer.IsReadable())
            {
                return false;
            }

            RedisMessageType redisMessageType = RedisCodecUtil.ParseMessageType(byteBuffer.ReadByte());
            this.state = IsInline(redisMessageType)
                ? State.DecodeInline
                : State.DecodeLength;
            this.messageType = redisMessageType;

            return true;
        }

        static bool IsInline(RedisMessageType messageType) =>
            messageType == RedisMessageType.SimpleString
            || messageType == RedisMessageType.Error
            || messageType == RedisMessageType.Integer;

        bool DecodeInline(IByteBuffer byteBuffer, ICollection<object> output)
        {
            Contract.Requires(byteBuffer != null);
            Contract.Requires(output != null);

            IByteBuffer buffer = ReadLine(byteBuffer);
            if (buffer == null)
            {
                if (byteBuffer.ReadableBytes > this.maximumInlineMessageLength)
                {
                    throw new RedisCodecException(
                        $"Length: {byteBuffer.ReadableBytes} (expected: <= {this.maximumInlineMessageLength})");
                }

                return false;
            }

            IRedisMessage message = this.ReadInlineMessage(this.messageType, buffer);
            output.Add(message);
            this.ResetDecoder();

            return true;
        }

        bool DecodeLength(IByteBuffer byteBuffer, ICollection<object> output)
        {
            Contract.Requires(byteBuffer != null);
            Contract.Requires(output != null);

            IByteBuffer lineByteBuffer = ReadLine(byteBuffer);
            if (lineByteBuffer == null)
            {
                return false;
            }

            long length = this.ParseNumber(lineByteBuffer);
            if (length < RedisConstants.NullValue)
            {
                throw new RedisCodecException(
                    $"Length: {length} (expected: >= {RedisConstants.NullValue})");
            }

            switch (this.messageType)
            {
                case RedisMessageType.ArrayHeader:
                    output.Add(new ArrayHeaderRedisMessage(length));
                    this.ResetDecoder();

                    return true;
                case RedisMessageType.BulkString:
                    if (length > RedisConstants.MaximumMessageLength)
                    {
                        throw new RedisCodecException(
                            $"Length: {length} (expected: <= {RedisConstants.MaximumMessageLength})");
                    }
                    this.remainingBulkLength = (int)length; // range(int) is already checked.

                    return this.DecodeBulkString(byteBuffer, output);
                default:
                    throw new RedisCodecException(
                        $"Bad messageType: {this.messageType}, expecting ArrayHeader or BulkString.");
            }
        }

        bool DecodeBulkString(IByteBuffer byteBuffer, ICollection<object> output)
        {
            Contract.Requires(byteBuffer != null);
            Contract.Requires(output != null);

            if (this.remainingBulkLength == RedisConstants.NullValue) // $-1\r\n
            {
                output.Add(FullBulkStringRedisMessage.Null);
                this.ResetDecoder();
                return true;
            }

            if (this.remainingBulkLength == 0)
            {
                this.state = State.DecodeBulkStringEndOfLine;
                return this.DecodeBulkStringEndOfLine(byteBuffer, output);
            }

            // expectedBulkLength is always positive.
            output.Add(new BulkStringHeaderRedisMessage(this.remainingBulkLength));
            this.state = State.DecodeBulkStringContent;

            return this.DecodeBulkStringContent(byteBuffer, output);
        }

        bool DecodeBulkStringContent(IByteBuffer byteBuffer, ICollection<object> output)
        {
            Contract.Requires(byteBuffer != null);
            Contract.Requires(output != null);

            int readableBytes = byteBuffer.ReadableBytes;
            if (readableBytes == 0)
            {
                return false;
            }

            // if this is last frame.
            if (readableBytes >= this.remainingBulkLength + RedisConstants.EndOfLineLength)
            {
                IByteBuffer content = byteBuffer.ReadSlice(this.remainingBulkLength);
                ReadEndOfLine(byteBuffer);

                // Only call retain after readEndOfLine(...) as the method may throw an exception.
                output.Add(new LastBulkStringRedisContent((IByteBuffer)content.Retain()));
                this.ResetDecoder();

                return true;
            }

            // chunked write.
            int toRead = Math.Min(this.remainingBulkLength, readableBytes);
            this.remainingBulkLength -= toRead;
            IByteBuffer buffer = byteBuffer.ReadSlice(toRead);
            output.Add(new BulkStringRedisContent((IByteBuffer)buffer.Retain()));

            return true;
        }

        // $0\r\n <here> \r\n
        bool DecodeBulkStringEndOfLine(IByteBuffer byteBuffer, ICollection<object> output)
        {
            Contract.Requires(byteBuffer != null);
            Contract.Requires(output != null);

            if (byteBuffer.ReadableBytes < RedisConstants.EndOfLineLength)
            {
                return false;
            }

            ReadEndOfLine(byteBuffer);
            output.Add(FullBulkStringRedisMessage.Empty);
            this.ResetDecoder();

            return true;
        }

        static IByteBuffer ReadLine(IByteBuffer byteBuffer)
        {
            Contract.Requires(byteBuffer != null);

            if (!byteBuffer.IsReadable(RedisConstants.EndOfLineLength))
            {
                return null;
            }

            int lfIndex = byteBuffer.ForEachByte(ByteProcessor.FindLF);
            if (lfIndex < 0)
            {
                return null;
            }

            IByteBuffer buffer = byteBuffer.ReadSlice(lfIndex - byteBuffer.ReaderIndex - 1);
            ReadEndOfLine(byteBuffer);

            return buffer;
        }

        static void ReadEndOfLine(IByteBuffer byteBuffer)
        {
            Contract.Requires(byteBuffer != null);

            short delim = byteBuffer.ReadShort();
            if (RedisConstants.EndOfLine == delim)
            {
                return;
            }

            byte[] bytes = RedisCodecUtil.GetBytes(delim);
            throw new RedisCodecException($"delimiter: [{bytes[0]},{bytes[1]}] (expected: \\r\\n)");
        }

        void ResetDecoder()
        {
            this.state = State.DecodeType;
            this.remainingBulkLength = 0;
        }

        long ParseNumber(IByteBuffer byteBuffer)
        {
            int readableBytes = byteBuffer.ReadableBytes;
            bool negative = readableBytes > 0
                && byteBuffer.GetByte(byteBuffer.ReaderIndex) == '-';

            int extraOneByteForNegative = negative ? 1 : 0;
            if (readableBytes <= extraOneByteForNegative)
            {
                throw new RedisCodecException(
                    $"No number to parse: {byteBuffer.ToString(Encoding.ASCII)}");
            }

            if (readableBytes > RedisConstants.PositiveLongValueMaximumLength + extraOneByteForNegative)
            {
                throw new RedisCodecException(
                    $"Too many characters to be a valid RESP Integer: {byteBuffer.ToString(Encoding.ASCII)}");
            }

            if (negative)
            {
                return -this.ParsePositiveNumber(byteBuffer.SkipBytes(extraOneByteForNegative));
            }

            return this.ParsePositiveNumber(byteBuffer);
        }

        long ParsePositiveNumber(IByteBuffer byteBuffer)
        {
            this.toPositiveLongProcessor.Reset();
            byteBuffer.ForEachByte(this.toPositiveLongProcessor);

            return this.toPositiveLongProcessor.Content;
        }

        class ToPositiveLongProcessor : IByteProcessor
        {
            public bool Process(byte value)
            {
                if (!char.IsDigit((char)value))
                {
                    throw new RedisCodecException($"Bad byte in number: {value}, expecting digits from 0 to 9");
                }

                this.Content = this.Content * 10 + (value - '0');
                return true;
            }

            public long Content { get; private set; }

            public void Reset() => this.Content = 0;
        }
    }
}