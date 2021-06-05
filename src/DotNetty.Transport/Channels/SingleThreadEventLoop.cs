// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;

    /// <summary>
    /// <see cref="IEventLoop"/> implementation based on <see cref="SingleThreadEventExecutor"/>.
    /// </summary>
    public class SingleThreadEventLoop : SingleThreadEventExecutor, IEventLoop
    {
        static readonly TimeSpan DefaultBreakoutInterval = TimeSpan.FromMilliseconds(100);

        /// <summary>Creates a new instance of <see cref="SingleThreadEventLoop"/>.</summary>
        public SingleThreadEventLoop()
            : this(null, DefaultBreakoutInterval)
        {
        }

        /// <summary>Creates a new instance of <see cref="SingleThreadEventLoop"/>.</summary>
        public SingleThreadEventLoop(string threadName)
            : this(threadName, DefaultBreakoutInterval)
        {
        }

        /// <summary>Creates a new instance of <see cref="SingleThreadEventLoop"/>.</summary>
        public SingleThreadEventLoop(string threadName, TimeSpan breakoutInterval)
            : base(threadName, breakoutInterval)
        {
        }

        /// <summary>Creates a new instance of <see cref="SingleThreadEventLoop"/>.</summary>
        public SingleThreadEventLoop(IEventLoopGroup parent)
            : this(parent, null, DefaultBreakoutInterval)
        {
        }

        /// <summary>Creates a new instance of <see cref="SingleThreadEventLoop"/>.</summary>
        public SingleThreadEventLoop(IEventLoopGroup parent, string threadName)
            : this(parent, threadName, DefaultBreakoutInterval)
        {
        }

        /// <summary>Creates a new instance of <see cref="SingleThreadEventLoop"/>.</summary>
        public SingleThreadEventLoop(IEventLoopGroup parent, string threadName, TimeSpan breakoutInterval)
            : base(parent, threadName, breakoutInterval)
        {
        }

        /// <summary>Creates a new instance of <see cref="SingleThreadEventLoop"/>.</summary>
        protected SingleThreadEventLoop(string threadName, TimeSpan breakoutInterval, IQueue<IRunnable> taskQueue)
            : base(null, threadName, breakoutInterval, taskQueue)
        {
        }

        /// <summary>Creates a new instance of <see cref="SingleThreadEventLoop"/>.</summary>
        protected SingleThreadEventLoop(IEventLoopGroup parent, string threadName, TimeSpan breakoutInterval, IQueue<IRunnable> taskQueue)
            : base(parent, threadName, breakoutInterval, taskQueue)
        {
        }

        public new IEventLoop GetNext() => this;

        /// <inheritdoc />
        public Task RegisterAsync(IChannel channel) => channel.Unsafe.RegisterAsync(this);

        /// <inheritdoc />
        public new IEventLoopGroup Parent => (IEventLoopGroup)base.Parent;

        public new IEnumerable<IEventLoop> Items => new[] { this };
    }
}