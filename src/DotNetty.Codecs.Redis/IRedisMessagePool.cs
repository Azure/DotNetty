// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis
{
    using DotNetty.Buffers;
    using DotNetty.Codecs.Redis.Messages;

    public interface IRedisMessagePool
    {
        bool TryGetSimpleString(string content, out SimpleStringRedisMessage message);

        bool TryGetSimpleString(IByteBuffer content, out SimpleStringRedisMessage message);

        bool TryGetError(string content, out ErrorRedisMessage message);

        bool TryGetError(IByteBuffer content, out ErrorRedisMessage message);

        bool TryGetInteger(IByteBuffer content, out IntegerRedisMessage message);

        bool TryGetInteger(long value, out IntegerRedisMessage message);

        bool TryGetByteBufferOfInteger(long value, out byte[] bytes);
    }
}