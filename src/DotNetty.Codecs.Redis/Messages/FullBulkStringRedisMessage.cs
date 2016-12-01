// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Messages
{
    using DotNetty.Buffers;
    using DotNetty.Common;

    public sealed class FullBulkStringRedisMessage : DefaultByteBufferHolder, IFullBulkStringRedisMessage
    {
        public static readonly IFullBulkStringRedisMessage Null = new NullOrEmptyFullBulkStringRedisMessage(true);

        public static readonly IFullBulkStringRedisMessage Empty = new NullOrEmptyFullBulkStringRedisMessage(false);

        public FullBulkStringRedisMessage(IByteBuffer buffer)
            : base(buffer)
        {
        }

        public bool IsNull => false;

        public override IByteBufferHolder Copy() => new FullBulkStringRedisMessage(this.Content.Copy());

        public override IByteBufferHolder Duplicate() => new FullBulkStringRedisMessage(this.Content.Duplicate());

        public override string ToString() => $"{nameof(FullBulkStringRedisMessage)}[content={this.Content}]";

        sealed class NullOrEmptyFullBulkStringRedisMessage : IFullBulkStringRedisMessage
        {
            internal NullOrEmptyFullBulkStringRedisMessage(bool isNull)
                : this(Unpooled.Empty, isNull)
            {
            }

            NullOrEmptyFullBulkStringRedisMessage(IByteBuffer content, bool isNull)
            {
                this.Content = content;
                this.IsNull = isNull;
            }

            public bool IsNull { get; }

            public int ReferenceCount => 1;

            public IByteBuffer Content { get; }

            public IByteBufferHolder Copy() => this;

            public IByteBufferHolder Duplicate() => this;

            public IReferenceCounted Touch() => this;

            public IReferenceCounted Touch(object hint) => this;

            public IReferenceCounted Retain() => this;

            public IReferenceCounted Retain(int increment) => this;

            public bool Release() => false;

            public bool Release(int decrement) => false;
        }
    }
}