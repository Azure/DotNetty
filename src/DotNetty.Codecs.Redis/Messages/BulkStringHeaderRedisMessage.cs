// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Messages
{
    public class BulkStringHeaderRedisMessage : IRedisMessage
    {
        public BulkStringHeaderRedisMessage(int bulkStringLength)
        {
            if (bulkStringLength <= 0)
            {
                throw new RedisCodecException($"bulkStringLength: {bulkStringLength} (expected: > 0)");
            }
            this.BulkStringLength = bulkStringLength;
        }

        public int BulkStringLength { get; }

        public bool IsNull => this.BulkStringLength == RedisConstants.NullValue;
    }
}