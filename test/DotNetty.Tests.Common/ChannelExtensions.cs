// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Tests.Common
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;

    public static class ChannelExtensions
    {
        public static Task WriteAndFlushManyAsync(this IChannel channel, params object[] messages)
        {
            var list = new List<Task>();
            foreach (object m in messages)
            {
                list.Add(Task.Run(async () => await channel.WriteAsync(m)));
            }
            IEnumerable<Task> tasks = list.ToArray();
            channel.Flush();
            return Task.WhenAll(tasks);
        }
    }
}