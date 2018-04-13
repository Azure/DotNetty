// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis
{
    using System.Collections.Generic;
    using DotNetty.Codecs.Redis.Messages;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public sealed class RedisArrayAggregator : MessageToMessageDecoder<IRedisMessage>
    {
        readonly Stack<AggregateState> depths = new Stack<AggregateState>(4);

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

            output.Add(message);
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
                // Currently, this codec doesn't support `long` length for arrays because Java's List.size() is int.
                if (header.Length > int.MaxValue)
                {
                    throw new CodecException($"This codec doesn't support longer length than {int.MaxValue}");
                }

                // start aggregating array
                this.depths.Push(new AggregateState((int)header.Length));
                return null;
            }

            throw new CodecException($"Bad length: {header.Length}");
        }

        sealed class AggregateState
        {
            internal int Length { get; }

            internal readonly List<IRedisMessage> Children;

            internal AggregateState(int length)
            {
                this.Length = length;
                this.Children = new List<IRedisMessage>(length);
            }
        }
    }
}