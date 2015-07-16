// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    public interface IEventLoop : IEventExecutor
    {
        IChannelHandlerInvoker Invoker { get; }

        Task RegisterAsync(IChannel channel);

        IEventLoop Unwrap();
    }
}