// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Codecs.Redis.Messages;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public sealed class RedisArrayAggregator : MessageToMessageDecoder<IRedisMessage>
    {
        const int DefaultMaxNestedArrayDepth = 1024;
        const int InitialChildrenCapacity = 32;

        readonly Stack<AggregateState> depths = new Stack<AggregateState>(4);
        readonly int maxElements;
        readonly int maxNestedArrayDepth;
        long pendingElements;

        public RedisArrayAggregator()
            : this(RedisConstants.RedisMaxArrayLength, DefaultMaxNestedArrayDepth)
        {
        }

        public RedisArrayAggregator(int maxElements, int maxNestedArrayDepth)
        {
            if (maxElements <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxElements), "maxElements must be a positive integer.");
            }
            if (maxNestedArrayDepth <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxNestedArrayDepth), "maxNestedArrayDepth must be a positive integer.");
            }

            this.maxElements = maxElements;
            this.maxNestedArrayDepth = maxNestedArrayDepth;
        }

        protected override void Decode(IChannelHandlerContext context, IRedisMessage message, List<object> output)
        {
            if (message is ArrayHeaderRedisMessage)
            {
                message = this.DecodeRedisArrayHeader((ArrayHeaderRedisMessage)message);
                if (message == null)
                {
                    return;
                }
            }
            else
            {
                ReferenceCountUtil.Retain(message);
            }

            while (this.depths.Count > 0)
            {
                AggregateState current = this.depths.Peek();
                current.Children.Add(message);

                // if current aggregation completed, go to parent aggregation.
                if (current.Children.Count == current.Length)
                {
                    message = new ArrayRedisMessage(current.Children);
                    this.depths.Pop();
                }
                else
                {
                    // not aggregated yet. try next time.
                    return;
                }
            }

            this.pendingElements = 0;
            output.Add(message);
        }

        CodecException ClearAndCreateException(string message)
        {
            this.ReleaseAndClearDepths();
            return new CodecException(message);
        }

        IRedisMessage DecodeRedisArrayHeader(ArrayHeaderRedisMessage header)
        {
            if (header.IsNull)
            {
                return ArrayRedisMessage.Null;
            }
            else if (header.Length == 0)
            {
                return ArrayRedisMessage.Empty;
            }
            else if (header.Length > 0)
            {
                if (header.Length > this.maxElements)
                {
                    throw this.ClearAndCreateException(
                        $"This codec doesn't support longer length than {this.maxElements}");
                }

                if (this.depths.Count >= this.maxNestedArrayDepth)
                {
                    throw this.ClearAndCreateException(
                        $"Max nested array depth exceeded: {this.maxNestedArrayDepth}");
                }

                long newPendingElements = this.pendingElements + header.Length;
                if (newPendingElements > this.maxElements)
                {
                    throw this.ClearAndCreateException(
                        $"Total outstanding array elements exceeds {this.maxElements}");
                }
                this.pendingElements = newPendingElements;

                this.depths.Push(new AggregateState((int)header.Length));
                return null;
            }

            throw this.ClearAndCreateException($"Bad length: {header.Length}");
        }

        public override void HandlerRemoved(IChannelHandlerContext context)
        {
            try
            {
                base.HandlerRemoved(context);
            }
            finally
            {
                this.ReleaseAndClearDepths();
            }
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            try
            {
                base.ChannelInactive(context);

                if (this.depths.Count > 0)
                {
                    context.FireExceptionCaught(new PrematureChannelClosureException(
                        $"Channel gone inactive with {this.depths.Count} messages still incomplete"));
                }
            }
            finally
            {
                this.ReleaseAndClearDepths();
            }
        }

        void ReleaseAndClearDepths()
        {
            foreach (AggregateState state in this.depths)
            {
                foreach (IRedisMessage message in state.Children)
                {
                    ReferenceCountUtil.SafeRelease(message);
                }
            }
            this.depths.Clear();
            this.pendingElements = 0;
        }

        sealed class AggregateState
        {
            internal int Length { get; }

            internal readonly List<IRedisMessage> Children;

            internal AggregateState(int length)
            {
                this.Length = length;
                this.Children = new List<IRedisMessage>(Math.Min(length, InitialChildrenCapacity));
            }
        }
    }
}