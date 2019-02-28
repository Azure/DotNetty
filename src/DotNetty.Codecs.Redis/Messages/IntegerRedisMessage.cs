// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Messages
{
    using System.Text;
    using DotNetty.Common.Utilities;

    public sealed class IntegerRedisMessage : IRedisMessage
    {
        public IntegerRedisMessage(long value)
        {
            this.Value = value;
        }

        public long Value { get; }

        public override string ToString() =>
            new StringBuilder(StringUtil.SimpleClassName(this))
                .Append('[')
                .Append("value=")
                .Append(this.Value)
                .Append(']')
                .ToString();
    }
}