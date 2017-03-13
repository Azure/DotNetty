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
    using DotNetty.Transport.Channels;

    public sealed class RedisEncoder : MessageToMessageEncoder<IRedisMessage>
    {
        readonly IRedisMessagePool messagePool;

        public RedisEncoder()
            : this(FixedRedisMessagePool.Default)
        {
        }

        public RedisEncoder(IRedisMessagePool messagePool)
        {
            Contract.Requires(messagePool != null);

            this.messagePool = messagePool;
        }

        protected override void Encode(IChannelHandlerContext context, IRedisMessage message, List<object> output)
        {
            Contract.Requires(context != null);
            Contract.Requires(message != null);
            Contract.Requires(output != null);

            try
            {
                this.Write(context.Allocator, message, output);
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

        void Write(IByteBufferAllocator allocator, IRedisMessage message, ICollection<object> output)
        {
            Contract.Requires(allocator != null);
            Contract.Requires(message != null);
            Contract.Requires(output != null);

            if (message is SimpleStringRedisMessage)
            {
                Write(allocator, (SimpleStringRedisMessage)message, output);
                return;
            }

            if (message is ErrorRedisMessage)
            {
                Write(allocator, (ErrorRedisMessage)message, output);
                return;
            }

            if (message is IntegerRedisMessage)
            {
                this.Write(allocator, (IntegerRedisMessage)message, output);
                return;
            }

            if (message is FullBulkStringRedisMessage)
            {
                this.Write(allocator, (FullBulkStringRedisMessage)message, output);
                return;
            }

            if (message is IBulkStringRedisContent)
            {
                Write(allocator, (IBulkStringRedisContent)message, output);
                return;
            }

            if (message is BulkStringHeaderRedisMessage)
            {
                this.Write(allocator, (BulkStringHeaderRedisMessage)message, output);
                return;
            }

            if (message is ArrayHeaderRedisMessage)
            {
                this.Write(allocator, (ArrayHeaderRedisMessage)message, output);
                return;
            }

            if (message is ArrayRedisMessage)
            {
                this.Write(allocator, (ArrayRedisMessage)message, output);
                return;
            }

            throw new CodecException($"Unknown message type: {message}");
        }

        void Write(IByteBufferAllocator allocator, FullBulkStringRedisMessage message, ICollection<object> output)
        {
            Contract.Requires(allocator != null);
            Contract.Requires(message != null);
            Contract.Requires(output != null);

            IByteBuffer buffer = allocator.Buffer(
                RedisConstants.TypeLength
                + (message.IsNull ? RedisConstants.NullLength : RedisConstants.LongValueMaximumLength)
                + RedisConstants.EndOfLineLength);

            buffer.WriteByte((char)RedisMessageType.BulkString);
            if (message.IsNull)
            {
                buffer.WriteShort(RedisConstants.Null);
                buffer.WriteShort(RedisConstants.EndOfLine);

                output.Add(buffer);
            }
            else
            {
                int readableBytes = message.Content.ReadableBytes;
                byte[] bytes = this.NumberToBytes(readableBytes);
                buffer.WriteBytes(bytes);
                buffer.WriteShort(RedisConstants.EndOfLine);

                output.Add(buffer);
                output.Add(message.Content.Retain());
                output.Add(allocator
                    .Buffer(RedisConstants.EndOfLineLength)
                    .WriteShort(RedisConstants.EndOfLine));
            }
        }

        static void Write(IByteBufferAllocator allocator, IBulkStringRedisContent message, ICollection<object> output)
        {
            Contract.Requires(allocator != null);
            Contract.Requires(message != null);
            Contract.Requires(output != null);

            output.Add(message.Content.Retain());
            if (message is ILastBulkStringRedisContent)
            {
                output.Add(allocator
                    .Buffer(RedisConstants.EndOfLineLength)
                    .WriteShort(RedisConstants.EndOfLine));
            }
        }

        void Write(IByteBufferAllocator allocator, BulkStringHeaderRedisMessage message, ICollection<object> output)
        {
            Contract.Requires(allocator != null);
            Contract.Requires(message != null);
            Contract.Requires(output != null);

            IByteBuffer buffer = allocator.Buffer(
                RedisConstants.TypeLength
                + (message.IsNull ? RedisConstants.NullLength : RedisConstants.LongValueMaximumLength)
                + RedisConstants.EndOfLineLength);

            buffer.WriteByte((char)RedisMessageType.BulkString);

            if (!message.IsNull)
            {
                byte[] bytes = this.NumberToBytes(message.BulkStringLength);
                buffer.WriteBytes(bytes);
            }

            buffer.WriteShort(RedisConstants.EndOfLine);
            output.Add(buffer);
        }

        static void Write(IByteBufferAllocator allocator, SimpleStringRedisMessage message, ICollection<object> output)
        {
            Contract.Requires(allocator != null);
            Contract.Requires(message != null);
            Contract.Requires(output != null);

            IByteBuffer buffer = WriteString(allocator, RedisMessageType.SimpleString, message.Content);
            output.Add(buffer);
        }

        static void Write(IByteBufferAllocator allocator, ErrorRedisMessage message, ICollection<object> output)
        {
            Contract.Requires(allocator != null);
            Contract.Requires(message != null);
            Contract.Requires(output != null);

            IByteBuffer buffer = WriteString(allocator, RedisMessageType.Error, message.Content);
            output.Add(buffer);
        }

        void Write(IByteBufferAllocator allocator, IntegerRedisMessage message, ICollection<object> output)
        {
            Contract.Requires(allocator != null);
            Contract.Requires(message != null);
            Contract.Requires(output != null);

            IByteBuffer buffer = allocator.Buffer(
                RedisConstants.TypeLength
                + RedisConstants.LongValueMaximumLength
                + RedisConstants.EndOfLineLength);

            // Header
            buffer.WriteByte((char)RedisMessageType.Integer);

            // Content
            byte[] bytes = this.NumberToBytes(message.Value);
            buffer.WriteBytes(bytes);

            // EOL
            buffer.WriteShort(RedisConstants.EndOfLine);

            output.Add(buffer);
        }

        void Write(IByteBufferAllocator allocator, ArrayHeaderRedisMessage message, ICollection<object> output)
        {
            Contract.Requires(allocator != null);
            Contract.Requires(message != null);
            Contract.Requires(output != null);

            this.WriteArrayHeader(allocator, message.IsNull ? default(long?) : message.Length, output);
        }

        void Write(IByteBufferAllocator allocator, ArrayRedisMessage message, ICollection<object> output)
        {
            Contract.Requires(allocator != null);
            Contract.Requires(message != null);
            Contract.Requires(output != null);

            this.WriteArrayHeader(allocator, message.IsNull ? default(long?) : message.Children.Count, output);
            if (message.IsNull)
            {
                return;
            }

            foreach (IRedisMessage childMessage in message.Children)
            {
                this.Write(allocator, childMessage, output);
            }
        }

        void WriteArrayHeader(IByteBufferAllocator allocator, long? length, ICollection<object> output)
        {
            Contract.Requires(allocator != null);
            Contract.Requires(output != null);

            IByteBuffer buffer = allocator.Buffer(
                RedisConstants.TypeLength
                + (!length.HasValue ? RedisConstants.NullLength : RedisConstants.LongValueMaximumLength)
                + RedisConstants.EndOfLineLength);

            buffer.WriteByte((char)RedisMessageType.ArrayHeader);

            if (!length.HasValue)
            {
                buffer.WriteShort(RedisConstants.Null);
            }
            else
            {
                byte[] bytes = this.NumberToBytes(length.Value);
                buffer.WriteBytes(bytes);
            }

            buffer.WriteShort(RedisConstants.EndOfLine);
            output.Add(buffer);
        }

        static IByteBuffer WriteString(IByteBufferAllocator allocator, RedisMessageType messageType, string content)
        {
            Contract.Requires(allocator != null);

            IByteBuffer buffer = allocator.Buffer(
                RedisConstants.TypeLength
                + Encoding.UTF8.GetMaxByteCount(content.Length)
                + RedisConstants.EndOfLineLength);

            // Header
            buffer.WriteByte((char)messageType);

            // Content
            buffer.WriteBytes(Encoding.UTF8.GetBytes(content));

            // EOL
            buffer.WriteShort(RedisConstants.EndOfLine);

            return buffer;
        }

        byte[] NumberToBytes(long value)
        {
            byte[] bytes;
            if (!this.messagePool.TryGetBytes(value, out bytes))
            {
                bytes = RedisCodecUtil.LongToAsciiBytes(value);
            }

            return bytes;
        }
    }
}