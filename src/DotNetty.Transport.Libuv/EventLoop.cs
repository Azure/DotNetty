// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;

    sealed class EventLoop : LoopExecutor, IEventLoop
    {
        public EventLoop(IEventLoopGroup parent, string threadName)
            : base(parent, threadName)
        {
            this.Start();
        }

        public Task RegisterAsync(IChannel channel) => channel.Unsafe.RegisterAsync(this);

        public new IEventLoopGroup Parent => (IEventLoopGroup)base.Parent;
    }
}
