// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System.Threading.Tasks;

    public interface IEventLoopGroup
    {
        Task TerminationCompletion { get; }

        IEventLoop GetNext();

        Task ShutdownGracefullyAsync();
    }
}