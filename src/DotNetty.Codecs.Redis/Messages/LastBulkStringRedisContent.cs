// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Messages
{
    using DotNetty.Buffers;

    public sealed class LastBulkStringRedisContent : DefaultByteBufferHolder, ILastBulkStringRedisContent
    {
        public LastBulkStringRedisContent(IByteBuffer buffer)
            : base(buffer)
        {
        }

        public override IByteBufferHolder Replace(IByteBuffer buffer) => new LastBulkStringRedisContent(buffer);

        public override string ToString() => $"{nameof(LastBulkStringRedisContent)}[content={this.Content}]";
    }
}