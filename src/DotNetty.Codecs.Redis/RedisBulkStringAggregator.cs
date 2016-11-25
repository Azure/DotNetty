// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Redis.Messages;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public sealed class RedisBulkStringAggregator : MessageToMessageDecoder<IRedisMessage>
    {
        const int DefaultMaximumCumulationBufferComponents = 1024;

        readonly int maximumContentLength;

        int maximumCumulationBufferComponents;

        IByteBufferHolder currentMessage;

        public RedisBulkStringAggregator()
            : this(RedisConstants.MaximumMessageLength)
        {
        }

        RedisBulkStringAggregator(int maximumContentLength)
        {
            Contract.Requires(maximumContentLength > 0);

            this.maximumContentLength = maximumContentLength;
            this.maximumCumulationBufferComponents = DefaultMaximumCumulationBufferComponents;
            this.currentMessage = null;
        }

        public int MaximumCumulationBufferComponents
        {
            get { return this.maximumCumulationBufferComponents; }
            set
            {
                Contract.Requires(value >= 2);

                if (this.currentMessage != null)
                {
                    throw new InvalidOperationException(
                        "Decoder properties cannot be changed once the decoder is added to a pipeline.");
                }

                this.maximumCumulationBufferComponents = value;
            }
        }

        public override bool AcceptInboundMessage(object message)
        {
            // No need to match last and full types because they are subset of first and middle types.
            if (!base.AcceptInboundMessage(message))
            {
                return false;
            }

            // Already checked above, this cast is safe
            var redisMessage = (IRedisMessage)message;

            return (IsContentMessage(redisMessage)
                    || IsStartMessage(redisMessage))
                && !IsAggregated(redisMessage);
        }

        protected override void Decode(IChannelHandlerContext context, IRedisMessage message, List<object> output)
        {
            Contract.Requires(context != null);
            Contract.Requires(message != null);
            Contract.Requires(output != null);

            if (IsStartMessage(message)) // Start header
            {
                if (this.currentMessage != null)
                {
                    this.currentMessage.Release();
                    this.currentMessage = null;

                    throw new MessageAggregationException(
                        $"Start message {message} should have current buffer to be null.");
                }

                var startMessage = (BulkStringHeaderRedisMessage)message;
                if (IsContentLengthInvalid(startMessage, this.maximumContentLength))
                {
                    this.InvokeHandleOversizedMessage(context, startMessage);
                }

                // A streamed message -initialize the cumulative buffer, and wait for incoming chunks.
                CompositeByteBuffer buffer = context.Allocator.CompositeBuffer(this.maximumCumulationBufferComponents);
                this.currentMessage = BeginAggregation(buffer);
            }
            else if (IsContentMessage(message)) // Content
            {
                if (this.currentMessage == null)
                {
                    // it is possible that a TooLongFrameException was already thrown but we can still discard data
                    // until the begging of the next request/response.
                    return;
                }
                // Merge the received chunk into the content of the current message.
                var content = (CompositeByteBuffer)this.currentMessage.Content;

                var bufferHolder = (IByteBufferHolder)message;

                // Handle oversized message.
                if (content.ReadableBytes > this.maximumContentLength - bufferHolder.Content.ReadableBytes)
                {
                    // By convention, full message type extends first message type.

                    // ReSharper disable once PossibleInvalidCastException
                    var startMessage = (BulkStringHeaderRedisMessage)message;
                    this.InvokeHandleOversizedMessage(context, startMessage);

                    return;
                }

                // Append the content of the chunk.
                AppendPartialContent(content, bufferHolder.Content);

                bool isLast = IsLastContentMessage(message);
                if (isLast)
                {
                    // All done
                    output.Add(this.currentMessage);
                    this.currentMessage = null;
                }
            }
            else
            {
                throw new MessageAggregationException($"Unexpected message {message}");
            }
        }

        static void AppendPartialContent(CompositeByteBuffer content, IByteBuffer partialContent)
        {
            Contract.Requires(content != null);
            Contract.Requires(partialContent != null);

            if (!partialContent.IsReadable())
            {
                return;
            }

            var buffer = (IByteBuffer)partialContent.Retain();
            content.AddComponent(buffer);

            // Note that WriterIndex must be manually increased
            content.SetWriterIndex(content.WriterIndex + buffer.ReadableBytes);
        }

        void InvokeHandleOversizedMessage(IChannelHandlerContext context, BulkStringHeaderRedisMessage startMessage)
        {
            Contract.Requires(context != null);
            Contract.Requires(startMessage != null);

            this.currentMessage = null;

            try
            {
                context.FireExceptionCaught(
                    new TooLongFrameException($"Content length exceeded {this.maximumContentLength} bytes."));
            }
            finally
            {
                // Release the message in case it is a full one.
                ReferenceCountUtil.Release(startMessage);
            }
        }

        static bool IsStartMessage(IRedisMessage message)
        {
            Contract.Requires(message != null);

            return message is BulkStringHeaderRedisMessage
                && !IsAggregated(message);
        }

        static bool IsContentMessage(IRedisMessage message)
        {
            Contract.Requires(message != null);

            return message is IBulkStringRedisContent;
        }

        static FullBulkStringRedisMessage BeginAggregation(IByteBuffer byteBuffer)
        {
            Contract.Requires(byteBuffer != null);

            return new FullBulkStringRedisMessage(byteBuffer);
        }

        static bool IsLastContentMessage(IRedisMessage message)
        {
            Contract.Requires(message != null);

            return message is LastBulkStringRedisContent;
        }

        static bool IsContentLengthInvalid(BulkStringHeaderRedisMessage start, int expectedMaximumContentLength)
        {
            Contract.Requires(start != null);

            return start.BulkStringLength > expectedMaximumContentLength;
        }

        static bool IsAggregated(IRedisMessage message)
        {
            Contract.Requires(message != null);

            return message is IFullBulkStringRedisMessage;
        }
    }
}