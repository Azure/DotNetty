// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Messages
{
    using System.Diagnostics.Contracts;

    public sealed class ArrayHeaderRedisMessage : IRedisMessage
    {
        public ArrayHeaderRedisMessage(long length)
        {
            Contract.Requires(length >= RedisConstants.NullValue);

            this.Length = length;
        }

        public long Length { get; }

        public bool IsNull => this.Length == RedisConstants.NullValue;

        public override string ToString() => $"{nameof(ArrayHeaderRedisMessage)}[length={this.Length}]";
    }
}