// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Messages
{
    using System.Text;
    using DotNetty.Common.Utilities;

    public class ArrayHeaderRedisMessage : IRedisMessage
    {
        public ArrayHeaderRedisMessage(long length)
        {
            if (length < RedisConstants.NullValue)
            {
                throw new RedisCodecException($"length: {length} (expected: >= {RedisConstants.NullValue})");
            }
            this.Length = length;
        }

        public long Length { get; }

        public bool IsNull => this.Length == RedisConstants.NullValue;

        public override string ToString() =>
            new StringBuilder(StringUtil.SimpleClassName(this))
                .Append('[')
                .Append("length=")
                .Append(this.Length)
                .Append(']')
                .ToString();
    }
}