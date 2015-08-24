// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    abstract class PausableChannelEventExecutor : IPausableEventExecutor
    {
        public abstract void RejectNewTasks();

        public abstract void AcceptNewTasks();

        public abstract bool IsAcceptingNewTasks { get; }

        internal abstract IChannel Channel { get; }

        public abstract IEventExecutor Unwrap();

        public IEventExecutor Executor
        {
            get { return this; }
        }

        public bool InEventLoop
        {
            get { return this.Unwrap().InEventLoop; }
        }

        public bool IsInEventLoop(Thread thread)
        {
            return this.Unwrap().IsInEventLoop(thread);
        }

        public void Execute(IRunnable command)
        {
            if (!this.IsAcceptingNewTasks)
            {
                throw new RejectedExecutionException();
            }
            this.Unwrap().Execute(command);
        }

        public void Execute(Action<object> action, object state)
        {
            if (!this.IsAcceptingNewTasks)
            {
                throw new RejectedExecutionException();
            }
            this.Unwrap().Execute(action, state);
        }

        public void Execute(Action action)
        {
            if (!this.IsAcceptingNewTasks)
            {
                throw new RejectedExecutionException();
            }
            this.Unwrap().Execute(action);
        }

        public void Schedule(Action<object> action, object state, TimeSpan delay, CancellationToken cancellationToken)
        {
            if (!this.IsAcceptingNewTasks)
            {
                throw new RejectedExecutionException();
            }
            this.Unwrap().Schedule(action, state, delay, cancellationToken);
        }

        public void Schedule(Action<object> action, object state, TimeSpan delay)
        {
            if (!this.IsAcceptingNewTasks)
            {
                throw new RejectedExecutionException();
            }
            this.Unwrap().Schedule(action, state, delay);
        }

        public void Schedule(Action<object, object> action, object context, object state, TimeSpan delay, CancellationToken cancellationToken)
        {
            if (!this.IsAcceptingNewTasks)
            {
                throw new RejectedExecutionException();
            }
            this.Unwrap().Schedule(action, context, state, delay, cancellationToken);
        }

        public void Schedule(Action<object, object> action, object context, object state, TimeSpan delay)
        {
            if (!this.IsAcceptingNewTasks)
            {
                throw new RejectedExecutionException();
            }
            this.Unwrap().Schedule(action, context, state, delay);
        }

        public void Schedule(Action action, TimeSpan delay, CancellationToken cancellationToken)
        {
            if (!this.IsAcceptingNewTasks)
            {
                throw new RejectedExecutionException();
            }
            this.Unwrap().Schedule(action, delay, cancellationToken);
        }

        public void Schedule(Action action, TimeSpan delay)
        {
            if (!this.IsAcceptingNewTasks)
            {
                throw new RejectedExecutionException();
            }
            this.Unwrap().Schedule(action, delay);
        }

        public Task SubmitAsync(Func<object, Task> taskFunc, object state)
        {
            if (!this.IsAcceptingNewTasks)
            {
                throw new RejectedExecutionException();
            }
            return this.Unwrap().SubmitAsync(taskFunc, state);
        }

        public Task SubmitAsync(Func<Task> taskFunc)
        {
            if (!this.IsAcceptingNewTasks)
            {
                throw new RejectedExecutionException();
            }
            return this.Unwrap().SubmitAsync(taskFunc);
        }

        public void Execute(Action<object, object> action, object context, object state)
        {
            if (!this.IsAcceptingNewTasks)
            {
                throw new RejectedExecutionException();
            }
            this.Unwrap().Execute(action, context, state);
        }

        public Task SubmitAsync(Func<object, object, Task> func, object context, object state)
        {
            if (!this.IsAcceptingNewTasks)
            {
                throw new RejectedExecutionException();
            }
            return this.Unwrap().SubmitAsync(func, context, state);
        }

        public bool IsShuttingDown
        {
            get { return this.Unwrap().IsShuttingDown; }
        }

        public Task ShutdownGracefullyAsync()
        {
            return this.Unwrap().ShutdownGracefullyAsync();
        }

        public Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        {
            return this.Unwrap().ShutdownGracefullyAsync(quietPeriod, timeout);
        }


        public Task TerminationCompletion
        {
            get { return this.Unwrap().TerminationCompletion; }
        }

        public bool IsShutdown
        {
            get { return this.Unwrap().IsShutdown; }
        }

        public bool IsTerminated
        {
            get { return this.Unwrap().IsTerminated; }
        }
    }
}