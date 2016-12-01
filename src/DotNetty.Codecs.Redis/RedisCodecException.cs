// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis
{
    using System;

    public sealed class RedisCodecException : CodecException
    {
        public RedisCodecException(string message)
            : this(message, null)
        {
        }

        public RedisCodecException(Exception exception)
            : this(null, exception)
        {
        }

        public RedisCodecException(string message, Exception exception)
            : base(message, exception)
        {
        }
    }
}