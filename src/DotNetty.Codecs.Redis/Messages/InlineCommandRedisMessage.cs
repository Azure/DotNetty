// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Messages
{
    public sealed class InlineCommandRedisMessage : AbstractStringRedisMessage
    {
        public InlineCommandRedisMessage(string content)
            : base(content)
        {
        }
    }
}
