// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Redis.Messages;
    using DotNetty.Transport.Channels;

    public sealed class RedisEncoder : MessageToMessageEncoder<IRedisMessage>
    {
        readonly IRedisMessagePool messagePool;

        public RedisEncoder() : this(FixedRedisMessagePool.Instance)
        {
        }

        public RedisEncoder(IRedisMessagePool messagePool)
        {
            Contract.Requires(messagePool != null);

            this.messagePool = messagePool;
        }

        protected override void Encode(IChannelHandlerContext context, IRedisMessage message, List<object> output)
        {
            try
            {
                this.WriteRedisMessage(context.Allocator, message, output);
            }
            catch (CodecException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new CodecException(exception);
            }
        }

        void WriteRedisMessage(IByteBufferAllocator allocator, IRedisMessage message, List<object> output)
        {
            if (message is InlineCommandRedisMessage inlineCommandRedisMessage)
            {
                WriteInlineCommandMessage(allocator, inlineCommandRedisMessage, output);
            }
            else if (message is SimpleStringRedisMessage stringRedisMessage)
            {
                WriteSimpleStringMessage(allocator, stringRedisMessage, output);
            }
            else if (message is ErrorRedisMessage errorRedisMessage)
            {
                WriteErrorMessage(allocator, errorRedisMessage, output);
            }
            else if (message is IntegerRedisMessage integerRedisMessage)
            {
                this.WriteIntegerMessage(allocator, integerRedisMessage, output);
            }
            else if (message is FullBulkStringRedisMessage fullBulkStringRedisMessage)
            {
                this.WriteFullBulkStringMessage(allocator, fullBulkStringRedisMessage, output);
            }
            else if (message is IBulkStringRedisContent bulkStringRedisContent)
            {
                WriteBulkStringContent(allocator, bulkStringRedisContent, output);
            }
            else if (message is BulkStringHeaderRedisMessage bulkStringHeaderRedisMessage)
            {
                this.WriteBulkStringHeader(allocator, bulkStringHeaderRedisMessage, output);
            }
            else if (message is ArrayHeaderRedisMessage arrayHeaderRedisMessage)
            {
                this.WriteArrayHeader(allocator, arrayHeaderRedisMessage, output);
            }
            else if (message is IArrayRedisMessage arrayRedisMessage)
            {
                this.WriteArrayMessage(allocator, arrayRedisMessage, output);
            }
            else
            {
                throw new CodecException($"Unknown message type: {message}");
            }
        }

        static void WriteInlineCommandMessage(IByteBufferAllocator allocator, InlineCommandRedisMessage msg, List<object> output) =>
            WriteString(allocator, RedisMessageType.InlineCommand, msg.Content, output);

        static void WriteSimpleStringMessage(IByteBufferAllocator allocator, SimpleStringRedisMessage msg, List<object> output) =>
            WriteString(allocator, RedisMessageType.SimpleString, msg.Content, output);

        static void WriteErrorMessage(IByteBufferAllocator allocator, ErrorRedisMessage msg, List<object> output) =>
            WriteString(allocator, RedisMessageType.Error, msg.Content, output);

        static void WriteString(IByteBufferAllocator allocator, RedisMessageType type, string content, List<object> output)
        {
            IByteBuffer buf = allocator.Buffer(type.Length + ByteBufferUtil.Utf8MaxBytes(content) + 
                RedisConstants.EndOfLineLength);
            type.WriteTo(buf);
            ByteBufferUtil.WriteUtf8(buf, content);
            buf.WriteShort(RedisConstants.EndOfLineShort);
            output.Add(buf);
        }

        void WriteIntegerMessage(IByteBufferAllocator allocator, IntegerRedisMessage msg, List<object> output)
        {
            IByteBuffer buf = allocator.Buffer(RedisConstants.TypeLength + RedisConstants.LongMaxLength + 
                RedisConstants.EndOfLineLength);
            RedisMessageType.Integer.WriteTo(buf);
            buf.WriteBytes(this.NumberToBytes(msg.Value));
            buf.WriteShort(RedisConstants.EndOfLineShort);
            output.Add(buf);
        }

        void WriteBulkStringHeader(IByteBufferAllocator allocator, BulkStringHeaderRedisMessage msg, List<object> output)
        {
            IByteBuffer buf = allocator.Buffer(RedisConstants.TypeLength +
                (msg.IsNull ? RedisConstants.NullLength : RedisConstants.LongMaxLength + RedisConstants.EndOfLineLength));
            RedisMessageType.BulkString.WriteTo(buf);
            if (msg.IsNull)
            {
                buf.WriteShort(RedisConstants.NullShort);
            }
            else
            {
                buf.WriteBytes(this.NumberToBytes(msg.BulkStringLength));
                buf.WriteShort(RedisConstants.EndOfLineShort);
            }
            output.Add(buf);
        }

        static void WriteBulkStringContent(IByteBufferAllocator allocator, IBulkStringRedisContent msg, List<object> output)
        {
            output.Add(msg.Content.Retain());
            if (msg is ILastBulkStringRedisContent)
            {
                output.Add(allocator.Buffer(RedisConstants.EndOfLineLength).WriteShort(RedisConstants.EndOfLineShort));
            }
        }

        void WriteFullBulkStringMessage(IByteBufferAllocator allocator, FullBulkStringRedisMessage msg, List<object> output)
        {
            if (msg.IsNull)
            {
                IByteBuffer buf = allocator.Buffer(RedisConstants.TypeLength + RedisConstants.NullLength +
                    RedisConstants.EndOfLineLength);
                RedisMessageType.BulkString.WriteTo(buf);
                buf.WriteShort(RedisConstants.NullShort);
                buf.WriteShort(RedisConstants.EndOfLineShort);
                output.Add(buf);
            }
            else
            {
                IByteBuffer headerBuf = allocator.Buffer(RedisConstants.TypeLength + RedisConstants.LongMaxLength +
                    RedisConstants.EndOfLineLength);
                RedisMessageType.BulkString.WriteTo(headerBuf);
                headerBuf.WriteBytes(this.NumberToBytes(msg.Content.ReadableBytes));
                headerBuf.WriteShort(RedisConstants.EndOfLineShort);
                output.Add(headerBuf);
                output.Add(msg.Content.Retain());
                output.Add(allocator.Buffer(RedisConstants.EndOfLineLength).WriteShort(RedisConstants.EndOfLineShort));
            }
        }

        void WriteArrayHeader(IByteBufferAllocator allocator, ArrayHeaderRedisMessage msg, List<object> output) =>
            this.WriteArrayHeader(allocator, msg.IsNull, msg.Length, output);

        void WriteArrayMessage(IByteBufferAllocator allocator, IArrayRedisMessage msg, List<object> output)
        {
            if (msg.IsNull)
            {
                this.WriteArrayHeader(allocator, msg.IsNull, RedisConstants.NullValue, output);
            }
            else
            {
                this.WriteArrayHeader(allocator, msg.IsNull, msg.Children.Count, output);
                foreach (IRedisMessage child in msg.Children)
                {
                    this.WriteRedisMessage(allocator, child, output);
                }
            }
        }

        void WriteArrayHeader(IByteBufferAllocator allocator, bool isNull, long length, List<object> output)
        {
            if (isNull)
            {
                IByteBuffer buf = allocator.Buffer(RedisConstants.TypeLength + RedisConstants.NullLength +
                    RedisConstants.EndOfLineLength);
                RedisMessageType.ArrayHeader.WriteTo(buf);
                buf.WriteShort(RedisConstants.NullShort);
                buf.WriteShort(RedisConstants.EndOfLineShort);
                    output.Add(buf);
            }
            else
            {
                IByteBuffer buf = allocator.Buffer(RedisConstants.TypeLength + RedisConstants.LongMaxLength +
                    RedisConstants.EndOfLineLength);
                RedisMessageType.ArrayHeader.WriteTo(buf);
                buf.WriteBytes(this.NumberToBytes(length));
                buf.WriteShort(RedisConstants.EndOfLineShort);
                    output.Add(buf);
            }
        }

        byte[] NumberToBytes(long value) =>
            this.messagePool.TryGetByteBufferOfInteger(value, out byte[] bytes) ? bytes : RedisCodecUtil.LongToAsciiBytes(value);
    }
}