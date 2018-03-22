// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Embedded
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using Thread = DotNetty.Common.Concurrency.XThread;

    sealed class EmbeddedEventLoop : AbstractScheduledEventExecutor, IEventLoop
    {
        readonly Queue<IRunnable> tasks = new Queue<IRunnable>(2);

        public IEventExecutor Executor => this;

        public Task RegisterAsync(IChannel channel) => channel.Unsafe.RegisterAsync(this);

        public override bool IsShuttingDown => false;

        public override Task TerminationCompletion
        {
            get { throw new NotSupportedException(); }
        }

        public override bool IsShutdown => false;

        public override bool IsTerminated => false;

        public new IEventLoopGroup Parent => (IEventLoopGroup)base.Parent;

        public override bool IsInEventLoop(Thread thread) => true;

        public override void Execute(IRunnable command)
        {
            if (command == null)
            {
                throw new NullReferenceException("command");
            }
            this.tasks.Enqueue(command);
        }

        public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        {
            throw new NotSupportedException();
        }

        internal PreciseTimeSpan NextScheduledTask() => this.NextScheduledTaskNanos();

        internal void RunTasks()
        {
            for (;;)
            {
                // have to perform an additional check since Queue<T> throws upon empty dequeue in .NET
                if (this.tasks.Count == 0)
                {
                    break;
                }
                IRunnable task = this.tasks.Dequeue();
                if (task == null)
                {
                    break;
                }
                task.Run();
            }
        }

        internal PreciseTimeSpan RunScheduledTasks()
        {
            PreciseTimeSpan time = GetNanos();
            for (;;)
            {
                IRunnable task = this.PollScheduledTask(time);
                if (task == null)
                {
                    return this.NextScheduledTaskNanos();
                }
                task.Run();
            }
        }

        internal new void CancelScheduledTasks() => base.CancelScheduledTasks();
    }
}