// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tests
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;

    class AsIsWriteStrategy : IWriteStrategy
    {
        public Task WriteToChannelAsync(EmbeddedChannel channel, ArraySegment<byte> input)
        {
            channel.WriteInbound(Unpooled.WrappedBuffer(input.Array, input.Offset, input.Count));
            return TaskEx.Completed;
        }

        public override string ToString() => "as-is";
    }
}