﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public abstract class AbstractExecutorService : IExecutorService
    {
        /// <inheritdoc cref="IExecutorService"/>
        public abstract bool IsShutdown { get; }

        /// <inheritdoc cref="IExecutorService"/>
        public abstract bool IsTerminated { get; }

        /// <inheritdoc cref="IExecutorService"/>
        public Task<T> SubmitAsync<T>(Func<T> func) => this.SubmitAsync(func, CancellationToken.None);

        /// <inheritdoc cref="IExecutorService"/>
        public Task<T> SubmitAsync<T>(Func<T> func, CancellationToken cancellationToken)
        {
            var node = new FuncSubmitQueueNode<T>(func, cancellationToken);
            this.Execute(node);
            return node.Completion;
        }

        /// <inheritdoc cref="IExecutorService"/>
        public Task<T> SubmitAsync<T>(Func<object, T> func, object state) => this.SubmitAsync(func, state, CancellationToken.None);

        /// <inheritdoc cref="IExecutorService"/>
        public Task<T> SubmitAsync<T>(Func<object, T> func, object state, CancellationToken cancellationToken)
        {
            var node = new StateFuncSubmitQueueNode<T>(func, state, cancellationToken);
            this.Execute(node);
            return node.Completion;
        }

        /// <inheritdoc cref="IExecutorService"/>
        public Task<T> SubmitAsync<T>(Func<object, object, T> func, object context, object state) =>
            this.SubmitAsync(func, context, state, CancellationToken.None);

        /// <inheritdoc cref="IExecutorService"/>
        public Task<T> SubmitAsync<T>(
            Func<object, object, T> func,
            object context,
            object state,
            CancellationToken cancellationToken)
        {
            var node = new StateFuncWithContextSubmitQueueNode<T>(func, context, state, cancellationToken);
            this.Execute(node);
            return node.Completion;
        }

        /// <inheritdoc cref="IExecutor"/>
        public abstract void Execute(IRunnable task);

        /// <inheritdoc cref="IExecutor"/>
        public void Execute(Action<object> action, object state) => this.Execute(new StateActionTaskQueueNode(action, state));

        /// <inheritdoc cref="IExecutor"/>
        public void Execute(Action<object, object> action, object context, object state) => this.Execute(new StateActionWithContextTaskQueueNode(action, context, state));

        /// <inheritdoc cref="IExecutor"/>
        public void Execute(Action action) => this.Execute(new ActionTaskQueueNode(action));

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

            public StateFuncWithContextSubmitQueueNode(
                Func<object, object, T> func,
                object context,
                object state,
                CancellationToken cancellationToken)
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