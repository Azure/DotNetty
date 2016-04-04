// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Runtime.CompilerServices;
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
            this.VerifyAcceptingNewTasks();
            this.Unwrap().Execute(command);
        }

        public void Execute(Action<object> action, object state)
        {
            this.VerifyAcceptingNewTasks();
            this.Unwrap().Execute(action, state);
        }

        public void Execute(Action action)
        {
            this.VerifyAcceptingNewTasks();
            this.Unwrap().Execute(action);
        }

        public IScheduledTask Schedule(Action<object, object> action, object context, object state, TimeSpan delay)
        {
            this.VerifyAcceptingNewTasks();
            return this.Unwrap().Schedule(action, context, state, delay);
        }

        public IScheduledTask Schedule(Action<object> action, object state, TimeSpan delay)
        {
            this.VerifyAcceptingNewTasks();
            return this.Unwrap().Schedule(action, state, delay);
        }

        public IScheduledTask Schedule(Action action, TimeSpan delay)
        {
            this.VerifyAcceptingNewTasks();
            return this.Unwrap().Schedule(action, delay);
        }

        public Task ScheduleAsync(Action<object> action, object state, TimeSpan delay, CancellationToken cancellationToken)
        {
            this.VerifyAcceptingNewTasks();
            return this.Unwrap().ScheduleAsync(action, state, delay, cancellationToken);
        }

        public Task ScheduleAsync(Action<object> action, object state, TimeSpan delay)
        {
            this.VerifyAcceptingNewTasks();
            return this.Unwrap().ScheduleAsync(action, state, delay);
        }

        public Task ScheduleAsync(Action<object, object> action, object context, object state, TimeSpan delay, CancellationToken cancellationToken)
        {
            this.VerifyAcceptingNewTasks();
            return this.Unwrap().ScheduleAsync(action, context, state, delay, cancellationToken);
        }

        public Task ScheduleAsync(Action<object, object> action, object context, object state, TimeSpan delay)
        {
            this.VerifyAcceptingNewTasks();
            return this.Unwrap().ScheduleAsync(action, context, state, delay);
        }

        public Task ScheduleAsync(Action action, TimeSpan delay, CancellationToken cancellationToken)
        {
            this.VerifyAcceptingNewTasks();
            return this.Unwrap().ScheduleAsync(action, delay, cancellationToken);
        }

        public Task ScheduleAsync(Action action, TimeSpan delay)
        {
            this.VerifyAcceptingNewTasks();
            return this.Unwrap().ScheduleAsync(action, delay);
        }

        public Task<T> SubmitAsync<T>(Func<T> func)
        {
            this.VerifyAcceptingNewTasks();
            return this.Unwrap().SubmitAsync(func);
        }

        public Task<T> SubmitAsync<T>(Func<T> func, CancellationToken cancellationToken)
        {
            this.VerifyAcceptingNewTasks();
            return this.Unwrap().SubmitAsync(func, cancellationToken);
        }

        public Task<T> SubmitAsync<T>(Func<object, T> func, object state)
        {
            this.VerifyAcceptingNewTasks();
            return this.Unwrap().SubmitAsync(func, state);
        }

        public Task<T> SubmitAsync<T>(Func<object, T> func, object state, CancellationToken cancellationToken)
        {
            this.VerifyAcceptingNewTasks();
            return this.Unwrap().SubmitAsync(func, state, cancellationToken);
        }

        public Task<T> SubmitAsync<T>(Func<object, object, T> func, object context, object state)
        {
            this.VerifyAcceptingNewTasks();
            return this.Unwrap().SubmitAsync(func, context, state);
        }

        public Task<T> SubmitAsync<T>(Func<object, object, T> func, object context, object state, CancellationToken cancellationToken)
        {
            this.VerifyAcceptingNewTasks();
            return this.Unwrap().SubmitAsync(func, context, state, cancellationToken);
        }

        public void Execute(Action<object, object> action, object context, object state)
        {
            this.VerifyAcceptingNewTasks();
            this.Unwrap().Execute(action, context, state);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void VerifyAcceptingNewTasks()
        {
            if (!this.IsAcceptingNewTasks)
            {
                throw new RejectedExecutionException();
            }
        }
    }
}