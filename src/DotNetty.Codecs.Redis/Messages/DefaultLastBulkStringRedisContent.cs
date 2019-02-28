// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Messages
{
    using DotNetty.Buffers;

    public sealed class DefaultLastBulkStringRedisContent : DefaultBulkStringRedisContent, ILastBulkStringRedisContent
    {
        public DefaultLastBulkStringRedisContent(IByteBuffer content)
            : base(content)
        {
        }

        public override IByteBufferHolder Replace(IByteBuffer buffer) => new DefaultLastBulkStringRedisContent(buffer);
    }
}