// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis
{
    using DotNetty.Buffers;
    using DotNetty.Codecs.Redis.Messages;

    public interface IRedisMessagePool
    {
        bool TryGetMessage(string content, out SimpleStringRedisMessage message);

        bool TryGetMessage(IByteBuffer content, out SimpleStringRedisMessage message);

        bool TryGetMessage(string content, out ErrorRedisMessage message);

        bool TryGetMessage(IByteBuffer content, out ErrorRedisMessage message);

        bool TryGetMessage(IByteBuffer content, out IntegerRedisMessage message);

        bool TryGetMessage(long value, out IntegerRedisMessage message);

        bool TryGetBytes(long value, out byte[] bytes);
    }
}