// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Messages
{
    /// <summary>
    /// Type of RESP (REdis Serialization Protocol)
    /// see http://redis.io/topics/protocol
    /// </summary>
    public enum RedisMessageType
    {
        SimpleString = RedisCodecUtil.RedisSimpleString,
        Error = RedisCodecUtil.RedisError,
        Integer = RedisCodecUtil.RedisInteger,
        BulkString = RedisCodecUtil.RedisBulkString,
        ArrayHeader = RedisCodecUtil.RedisArray,
        Array = RedisCodecUtil.RedisArray
    }
}