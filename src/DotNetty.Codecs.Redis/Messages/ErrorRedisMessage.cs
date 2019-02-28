// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Messages
{
    public sealed class ErrorRedisMessage : AbstractStringRedisMessage
    {
        public ErrorRedisMessage(string content)
            : base(content)
        {
        }
    }
}