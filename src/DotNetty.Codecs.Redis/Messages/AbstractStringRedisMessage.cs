// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Messages
{
    using System.Diagnostics.Contracts;

    public abstract class AbstractStringRedisMessage : IRedisMessage
    {
        protected AbstractStringRedisMessage(string content)
        {
            Contract.Requires(content != null);

            this.Content = content;
        }

        public string Content { get; }
    }
}