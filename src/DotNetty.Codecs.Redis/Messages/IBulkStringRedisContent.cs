// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Messages
{
    using DotNetty.Buffers;

    public interface IBulkStringRedisContent : IRedisMessage, IByteBufferHolder
    {
    }
}