// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;

    sealed class EventLoop : LoopExecutor, IEventLoop
    {
        public EventLoop(IEventLoopGroup parent, string threadName)
            : base(parent, threadName)
        {
            this.Start();
        }

        public new IEventLoop GetNext() => (IEventLoop)base.GetNext();

        public Task RegisterAsync(IChannel channel) => channel.Unsafe.RegisterAsync(this);

        public new IEventLoopGroup Parent => (IEventLoopGroup)base.Parent;

        public new IEnumerable<IEventLoop> Items => new[] { this };
    }
}