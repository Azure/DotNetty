// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Messages
{
    using System.Diagnostics.Contracts;

    public sealed class BulkStringHeaderRedisMessage : IRedisMessage
    {
        public BulkStringHeaderRedisMessage(int bulkStringLength)
        {
            Contract.Requires(bulkStringLength > 0);

            this.BulkStringLength = bulkStringLength;
        }

        public int BulkStringLength { get; }

        public bool IsNull => false;
    }
}