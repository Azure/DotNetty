// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tests
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels.Embedded;

    public interface IWriteStrategy
    {
        Task WriteToChannelAsync(EmbeddedChannel channel, ArraySegment<byte> input);
    }
}