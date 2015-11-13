// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;

    /// <summary>
    ///     Abstract base class for <see cref="IEventExecutor" />s that need to support scheduling.
    /// </summary>
    public abstract class AbstractScheduledEventExecutor : AbstractEventExecutor
    {
        static readonly Action<object, object> AddScheduledTaskAction = (e, t) => ((AbstractScheduledEventExecutor)e).ScheduledTaskQueue.Enqueue((IScheduledRunnable)t);

        protected readonly PriorityQueue<IScheduledRunnable> ScheduledTaskQueue = new PriorityQueue<IScheduledRunnable>();

        // TODO: support for EventExecutorGroup

        protected static PreciseTimeSpan GetNanos()
        {
            return PreciseTimeSpan.FromStart;
        }

        protected static bool IsNullOrEmpty<T>(PriorityQueue<T> taskQueue) where T : class
        {
            return taskQueue == null || taskQueue.Count == 0;
        }

        /// <summary>
        ///     Cancel all scheduled tasks
        ///     This method MUST be called only when <see cref="IEventExecutor.InEventLoop" /> is <c>true</c>.
        /// </summary>
        protected virtual void CancelScheduledTasks()
        {
            Contract.Assert(this.InEventLoop);
            PriorityQueue<IScheduledRunnable> scheduledTaskQueue = this.ScheduledTaskQueue;
            if (IsNullOrEmpty(scheduledTaskQueue))
            {
                return;
            }

            IScheduledRunnable[] tasks = scheduledTaskQueue.ToArray();
            foreach (IScheduledRunnable t in tasks)
            {
                t.Cancel();
            }

            this.ScheduledTaskQueue.Clear();
        }

        protected IScheduledRunnable PollScheduledTask()
        {
            return this.PollScheduledTask(GetNanos());
        }

        protected IScheduledRunnable PollScheduledTask(PreciseTimeSpan nanoTime)
        {
            Contract.Assert(this.InEventLoop);

            IScheduledRunnable scheduledTask = this.ScheduledTaskQueue.Peek();
            if (scheduledTask == null)
            {
                return null;
            }

            if (scheduledTask.Deadline <= nanoTime)
            {
                this.ScheduledTaskQueue.Dequeue();
                return scheduledTask;
            }
            return null;
        }

        protected PreciseTimeSpan NextScheduledTaskNanos()
        {
            IScheduledRunnable nextScheduledRunnable = this.PeekScheduledTask();
            return nextScheduledRunnable == null ? PreciseTimeSpan.MinusOne : nextScheduledRunnable.Deadline;
        }

        protected IScheduledRunnable PeekScheduledTask()
        {
            PriorityQueue<IScheduledRunnable> scheduledTaskQueue = this.ScheduledTaskQueue;
            return IsNullOrEmpty(scheduledTaskQueue) ? null : scheduledTaskQueue.Peek();
        }

        protected bool HasScheduledTasks()
        {
            IScheduledRunnable scheduledTask = this.ScheduledTaskQueue.Peek();
            return scheduledTask != null && scheduledTask.Deadline <= PreciseTimeSpan.FromStart;
        }

        public override Task ScheduleAsync(Action action, TimeSpan delay, CancellationToken cancellationToken)
        {
            var scheduledTask = new ActionScheduledTask(action, PreciseTimeSpan.Deadline(delay), cancellationToken);
            if (this.InEventLoop)
            {
                this.ScheduledTaskQueue.Enqueue(scheduledTask);
            }
            else
            {
                this.Execute(AddScheduledTaskAction, this, scheduledTask);
            }
            return scheduledTask.Completion;
        }

        public override Task ScheduleAsync(Action<object> action, object state, TimeSpan delay, CancellationToken cancellationToken)
        {
            var scheduledTask = new StateActionScheduledTask(action, state, PreciseTimeSpan.Deadline(delay), cancellationToken);
            if (this.InEventLoop)
            {
                this.ScheduledTaskQueue.Enqueue(scheduledTask);
            }
            else
            {
                this.Execute(AddScheduledTaskAction, this, scheduledTask);
            }
            return scheduledTask.Completion;
        }

        public override Task ScheduleAsync(Action<object, object> action, object context, object state, TimeSpan delay, CancellationToken cancellationToken)
        {
            var scheduledTask = new StateActionWithContextScheduledTask(action, context, state, PreciseTimeSpan.Deadline(delay), cancellationToken);
            if (this.InEventLoop)
            {
                this.ScheduledTaskQueue.Enqueue(scheduledTask);
            }
            else
            {
                this.Execute(AddScheduledTaskAction, this, scheduledTask);
            }
            return scheduledTask.Completion;
        }

        #region Scheduled task data structures

        protected interface IScheduledRunnable : IRunnable, IComparable<IScheduledRunnable>
        {
            PreciseTimeSpan Deadline { get; }

            bool Cancel();
        }

        protected abstract class ScheduledTaskBase : MpscLinkedQueueNode<IRunnable>, IScheduledRunnable
        {
            readonly TaskCompletionSource promise;

            protected ScheduledTaskBase(PreciseTimeSpan deadline, TaskCompletionSource promise, CancellationToken cancellationToken)
            {
                this.promise = promise;
                this.Deadline = deadline;
                this.CancellationToken = cancellationToken;
            }

            public PreciseTimeSpan Deadline { get; private set; }

            public bool Cancel()
            {
                return this.promise.TrySetCanceled();
            }

            public Task Completion
            {
                get { return this.promise.Task; }
            }

            public CancellationToken CancellationToken { get; private set; }

            int IComparable<IScheduledRunnable>.CompareTo(IScheduledRunnable other)
            {
                Contract.Requires(other != null);

                return this.Deadline.CompareTo(other.Deadline);
            }

            public override IRunnable Value
            {
                get { return this; }
            }

            public void Run()
            {
                if (this.CancellationToken.IsCancellationRequested)
                {
                    this.promise.TrySetCanceled();
                    return;
                }
                if (this.Completion.IsCanceled)
                {
                    return;
                }
                try
                {
                    this.Execute();
                    this.promise.TryComplete();
                }
                catch (Exception ex)
                {
                    // todo: check for fatal
                    this.promise.TrySetException(ex);
                }
            }

            protected abstract void Execute();
        }

        sealed class ActionScheduledTask : ScheduledTaskBase
        {
            readonly Action action;

            public ActionScheduledTask(Action action, PreciseTimeSpan deadline, CancellationToken cancellationToken)
                : base(deadline, new TaskCompletionSource(), cancellationToken)
            {
                this.action = action;
            }

            protected override void Execute()
            {
                this.action();
            }
        }

        sealed class StateActionScheduledTask : ScheduledTaskBase
        {
            readonly Action<object> action;

            public StateActionScheduledTask(Action<object> action, object state, PreciseTimeSpan deadline,
                CancellationToken cancellationToken)
                : base(deadline, new TaskCompletionSource(state), cancellationToken)
            {
                this.action = action;
            }

            protected override void Execute()
            {
                this.action(this.Completion.AsyncState);
            }
        }

        sealed class StateActionWithContextScheduledTask : ScheduledTaskBase
        {
            readonly Action<object, object> action;
            readonly object context;

            public StateActionWithContextScheduledTask(Action<object, object> action, object context, object state,
                PreciseTimeSpan deadline, CancellationToken cancellationToken)
                : base(deadline, new TaskCompletionSource(state), cancellationToken)
            {
                this.action = action;
                this.context = context;
            }

            protected override void Execute()
            {
                this.action(this.context, this.Completion.AsyncState);
            }
        }

        #endregion
    }
}