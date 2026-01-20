// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Messages
{
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    public sealed class FullBulkStringRedisMessage : DefaultByteBufferHolder, IFullBulkStringRedisMessage
    {
        public static readonly FullBulkStringRedisMessage Null = new FullBulkStringRedisMessage(true);
        public static readonly FullBulkStringRedisMessage Empty = new FullBulkStringRedisMessage(false);

        public FullBulkStringRedisMessage(IByteBuffer content)
            : base(content)
        {
        }
  

        public FullBulkStringRedisMessage(bool isNull)
        : base(Unpooled.Empty)
        {
            this.IsNull = isNull;
        }
        public bool IsNull { get; private set; }

        public override string ToString() => 
            new StringBuilder(StringUtil.SimpleClassName(this))
            .Append('[')
            .Append("content=")
            .Append(this.Content)
            .Append(']')
            .ToString();

        public override IByteBufferHolder Replace(IByteBuffer content) => new FullBulkStringRedisMessage(content);
    }
}