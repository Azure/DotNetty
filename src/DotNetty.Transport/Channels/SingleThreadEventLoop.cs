// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    public class SingleThreadEventLoop : SingleThreadEventExecutor, IEventLoop
    {
        static readonly TimeSpan DefaultBreakoutInterval = TimeSpan.FromMilliseconds(100);

        public SingleThreadEventLoop()
            : this(null, DefaultBreakoutInterval)
        {
        }

        public SingleThreadEventLoop(string threadName)
            : this(threadName, DefaultBreakoutInterval)
        {
        }

        public SingleThreadEventLoop(string threadName, TimeSpan breakoutInterval)
            : base(threadName, breakoutInterval)
        {
            this.Invoker = new DefaultChannelHandlerInvoker(this);
        }

        public IChannelHandlerInvoker Invoker { get; private set; }

        public Task RegisterAsync(IChannel channel)
        {
            return channel.Unsafe.RegisterAsync(this);
        }

        IEventLoop IEventLoop.Unwrap()
        {
            return this;
        }
    }
}