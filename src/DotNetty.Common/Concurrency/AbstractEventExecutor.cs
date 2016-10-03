﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Abstract base class for <see cref="IEventExecutor" /> implementations
    /// </summary>
    public abstract class AbstractEventExecutor : IEventExecutor
    {
        protected static readonly TimeSpan DefaultShutdownQuietPeriod = TimeSpan.FromSeconds(2);
        protected static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(15);

        //TODO: support for EventExecutorGroup

        public bool InEventLoop => this.IsInEventLoop(Thread.CurrentThread);

        public abstract bool IsShuttingDown { get; }

        public abstract Task TerminationCompletion { get; }

        public abstract bool IsShutdown { get; }

        public abstract bool IsTerminated { get; }

        public abstract bool IsInEventLoop(Thread thread);

        public abstract void Execute(IRunnable task);

        public void Execute(Action<object> action, object state) => this.Execute(new StateActionTaskQueueNode(action, state));

        public void Execute(Action<object, object> action, object context, object state) => this.Execute(new StateActionWithContextTaskQueueNode(action, context, state));

        public void Execute(Action action) => this.Execute(new ActionTaskQueueNode(action));

        public virtual IScheduledTask Schedule(IRunnable action, TimeSpan delay)
        {
            throw new NotSupportedException();
        }

        public virtual IScheduledTask Schedule(Action action, TimeSpan delay)
        {
            throw new NotSupportedException();
        }

        public virtual IScheduledTask Schedule(Action<object> action, object state, TimeSpan delay)
        {
            throw new NotSupportedException();
        }

        public virtual IScheduledTask Schedule(Action<object, object> action, object context, object state, TimeSpan delay)
        {
            throw new NotSupportedException();
        }

        public virtual Task ScheduleAsync(Action action, TimeSpan delay) => this.ScheduleAsync(action, delay, CancellationToken.None);

        public virtual Task ScheduleAsync(Action<object> action, object state, TimeSpan delay, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public virtual Task ScheduleAsync(Action<object> action, object state, TimeSpan delay) => this.ScheduleAsync(action, state, delay, CancellationToken.None);

        public virtual Task ScheduleAsync(Action action, TimeSpan delay, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public virtual Task ScheduleAsync(Action<object, object> action, object context, object state, TimeSpan delay) => this.ScheduleAsync(action, context, state, delay, CancellationToken.None);

        public virtual Task ScheduleAsync(Action<object, object> action, object context, object state, TimeSpan delay, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<T> SubmitAsync<T>(Func<T> func) => this.SubmitAsync(func, CancellationToken.None);

        public Task<T> SubmitAsync<T>(Func<T> func, CancellationToken cancellationToken)
        {
            var node = new FuncSubmitQueueNode<T>(func, cancellationToken);
            this.Execute(node);
            return node.Completion;
        }

        public Task<T> SubmitAsync<T>(Func<object, T> func, object state) => this.SubmitAsync(func, state, CancellationToken.None);

        public Task<T> SubmitAsync<T>(Func<object, T> func, object state, CancellationToken cancellationToken)
        {
            var node = new StateFuncSubmitQueueNode<T>(func, state, cancellationToken);
            this.Execute(node);
            return node.Completion;
        }

        public Task<T> SubmitAsync<T>(Func<object, object, T> func, object context, object state) => this.SubmitAsync(func, context, state, CancellationToken.None);

        public Task<T> SubmitAsync<T>(Func<object, object, T> func, object context, object state, CancellationToken cancellationToken)
        {
            var node = new StateFuncWithContextSubmitQueueNode<T>(func, context, state, cancellationToken);
            this.Execute(node);
            return node.Completion;
        }

        public Task ShutdownGracefullyAsync() => this.ShutdownGracefullyAsync(DefaultShutdownQuietPeriod, DefaultShutdownTimeout);

        public abstract Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout);

        protected void SetCurrentExecutor(IEventExecutor executor) => ExecutionEnvironment.SetCurrentExecutor(executor);

        #region Queuing data structures

        sealed class ActionTaskQueueNode : IRunnable
        {
            readonly Action action;

            public ActionTaskQueueNode(Action action)
            {
                this.action = action;
            }

            public void Run() => this.action();
        }

        sealed class StateActionTaskQueueNode : IRunnable
        {
            readonly Action<object> action;
            readonly object state;

            public StateActionTaskQueueNode(Action<object> action, object state)
            {
                this.action = action;
                this.state = state;
            }

            public void Run() => this.action(this.state);
        }

        sealed class StateActionWithContextTaskQueueNode : IRunnable
        {
            readonly Action<object, object> action;
            readonly object context;
            readonly object state;

            public StateActionWithContextTaskQueueNode(Action<object, object> action, object context, object state)
            {
                this.action = action;
                this.context = context;
                this.state = state;
            }

            public void Run() => this.action(this.context, this.state);
        }

        abstract class FuncQueueNodeBase<T> : IRunnable
        {
            readonly TaskCompletionSource<T> promise;
            readonly CancellationToken cancellationToken;

            protected FuncQueueNodeBase(TaskCompletionSource<T> promise, CancellationToken cancellationToken)
            {
                this.promise = promise;
                this.cancellationToken = cancellationToken;
            }

            public Task<T> Completion => this.promise.Task;

            public void Run()
            {
                if (this.cancellationToken.IsCancellationRequested)
                {
                    this.promise.TrySetCanceled();
                    return;
                }

                try
                {
                    T result = this.Call();
                    this.promise.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    // todo: handle fatal
                    this.promise.TrySetException(ex);
                }
            }

            protected abstract T Call();
        }

        sealed class FuncSubmitQueueNode<T> : FuncQueueNodeBase<T>
        {
            readonly Func<T> func;

            public FuncSubmitQueueNode(Func<T> func, CancellationToken cancellationToken)
                : base(new TaskCompletionSource<T>(), cancellationToken)
            {
                this.func = func;
            }

            protected override T Call() => this.func();
        }

        sealed class StateFuncSubmitQueueNode<T> : FuncQueueNodeBase<T>
        {
            readonly Func<object, T> func;

            public StateFuncSubmitQueueNode(Func<object, T> func, object state, CancellationToken cancellationToken)
                : base(new TaskCompletionSource<T>(state), cancellationToken)
            {
                this.func = func;
            }

            protected override T Call() => this.func(this.Completion.AsyncState);
        }

        sealed class StateFuncWithContextSubmitQueueNode<T> : FuncQueueNodeBase<T>
        {
            readonly Func<object, object, T> func;
            readonly object context;

            public StateFuncWithContextSubmitQueueNode(Func<object, object, T> func, object context, object state, CancellationToken cancellationToken)
                : base(new TaskCompletionSource<T>(state), cancellationToken)
            {
                this.func = func;
                this.context = context;
            }

            protected override T Call() => this.func(this.context, this.Completion.AsyncState);
        }

        #endregion
    }
}