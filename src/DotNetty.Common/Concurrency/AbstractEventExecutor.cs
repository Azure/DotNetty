// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// Abstract base class for <see cref="IEventExecutor"/> implementations
    /// </summary>
    public abstract class AbstractEventExecutor : IEventExecutor
    {
        protected static readonly TimeSpan DefaultShutdownQuietPeriod = TimeSpan.FromSeconds(2);
        protected static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(15);
        protected static readonly Action<object> DelegatingAction = action => ((Action)action)();

        readonly MpscLinkedQueue<IRunnable> taskQueue = new MpscLinkedQueue<IRunnable>();

        //TODO: support for EventExecutorGroup

        public bool InEventLoop
        {
            get { return this.IsInEventLoop(Thread.CurrentThread); }
        }

        public abstract bool IsShuttingDown { get; }

        public abstract Task TerminationCompletion { get; }

        public abstract bool IsShutdown { get; }

        public abstract bool IsTerminated { get; }

        public abstract bool IsInEventLoop(Thread thread);

        public virtual IEventExecutor Unwrap()
        {
            return this;
        }

        public abstract void Execute(IRunnable task);

        public void Execute(Action<object> action, object state)
        {
            this.Execute(new TaskQueueNode(action, state));
        }

        public void Execute(Action<object, object> action, object context, object state)
        {
            this.Execute(new TaskQueueNodeWithContext(action, context, state));
        }

        public void Execute(Action action)
        {
            this.Execute(DelegatingAction, action);
        }

        public virtual void Schedule(Action<object> action, object state, TimeSpan delay, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public virtual void Schedule(Action<object> action, object state, TimeSpan delay)
        {
            throw new NotSupportedException();
        }

        public virtual void Schedule(Action action, TimeSpan delay, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public virtual void Schedule(Action action, TimeSpan delay)
        {
            throw new NotSupportedException();
        }

        public virtual void Schedule(Action<object, object> action, object context, object state, TimeSpan delay)
        {
            throw new NotSupportedException();
        }

        public virtual void Schedule(Action<object, object> action, object context, object state, TimeSpan delay, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SubmitAsync(Func<object, Task> func, object state)
        {
            var tcs = new TaskCompletionSource(state);
            // todo: allocation?
            this.Execute(async _ =>
            {
                var asTcs = (TaskCompletionSource)_;
                try
                {
                    await func(asTcs.Task.AsyncState);
                    asTcs.TryComplete();
                }
                catch (Exception ex)
                {
                    // todo: support cancellation
                    asTcs.TrySetException(ex);
                }
            }, tcs);
            return tcs.Task;
        }

        public Task SubmitAsync(Func<Task> func)
        {
            var tcs = new TaskCompletionSource();
            // todo: allocation?
            this.Execute(async _ =>
            {
                var asTcs = (TaskCompletionSource)_;
                try
                {
                    await func();
                    asTcs.TryComplete();
                }
                catch (Exception ex)
                {
                    // todo: support cancellation
                    asTcs.TrySetException(ex);
                }
            }, tcs);
            return tcs.Task;
        }

        public Task SubmitAsync(Func<object, object, Task> func, object context, object state)
        {
            var tcs = new TaskCompletionSource(context);
            // todo: allocation?
            this.Execute(async (s1, s2) =>
            {
                var asTcs = (TaskCompletionSource)s1;
                try
                {
                    await func(asTcs.Task.AsyncState, s2);
                    asTcs.TryComplete();
                }
                catch (Exception ex)
                {
                    // todo: support cancellation
                    asTcs.TrySetException(ex);
                }
            }, tcs, state);
            return tcs.Task;
        }

        public Task ShutdownGracefullyAsync()
        {
            return this.ShutdownGracefullyAsync(DefaultShutdownQuietPeriod, DefaultShutdownTimeout);
        }

        public abstract Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout);

        #region Queuing data structures

        sealed class TaskQueueNode : MpscLinkedQueueNode<IRunnable>, IRunnable
        {
            readonly Action<object> action;
            readonly object state;

            public TaskQueueNode(Action<object> action, object state)
            {
                this.action = action;
                this.state = state;
            }

            public override IRunnable Value
            {
                get { return this; }
            }

            public void Run()
            {
                this.action(this.state);
            }
        }

        sealed class TaskQueueNodeWithContext : MpscLinkedQueueNode<IRunnable>, IRunnable
        {
            readonly Action<object, object> action;
            readonly object context;
            readonly object state;

            public TaskQueueNodeWithContext(Action<object, object> action, object context, object state)
            {
                this.action = action;
                this.context = context;
                this.state = state;
            }

            public override IRunnable Value
            {
                get { return this; }
            }

            public void Run()
            {
                this.action(this.context, this.state);
            }
        }

        #endregion
    }
}