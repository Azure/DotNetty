// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Messages
{
    using DotNetty.Buffers;

    public sealed class BulkStringRedisContent : DefaultByteBufferHolder, IBulkStringRedisContent
    {
        public BulkStringRedisContent(IByteBuffer buffer)
            : base(buffer)
        {
        }

        public override IByteBufferHolder Replace(IByteBuffer content) => new BulkStringRedisContent(content);

        public override string ToString() => $"{nameof(BulkStringRedisContent)}[content={this.Content}]";
    }
}