// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Messages
{
    public sealed class IntegerRedisMessage : IRedisMessage
    {
        public IntegerRedisMessage(long value)
        {
            this.Value = value;
        }

        public long Value { get; }

        public override string ToString() => $"{nameof(IntegerRedisMessage)}[value={this.Value}]";
    }
}