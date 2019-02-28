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
        protected readonly PriorityQueue<IScheduledRunnable> ScheduledTaskQueue = new PriorityQueue<IScheduledRunnable>();

        protected AbstractScheduledEventExecutor()
        {
        }

        protected AbstractScheduledEventExecutor(IEventExecutorGroup parent)
            : base(parent)
        {
        }

        protected static PreciseTimeSpan GetNanos() => PreciseTimeSpan.FromStart;

        protected static bool IsNullOrEmpty<T>(PriorityQueue<T> taskQueue)
            where T : class
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

        protected IScheduledRunnable PollScheduledTask() => this.PollScheduledTask(GetNanos());

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
            return nextScheduledRunnable?.Deadline ?? PreciseTimeSpan.MinusOne;
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

        public override IScheduledTask Schedule(IRunnable action, TimeSpan delay)
        {
            Contract.Requires(action != null);

            return this.Schedule(new RunnableScheduledTask(this, action, PreciseTimeSpan.Deadline(delay)));
        }

        public override IScheduledTask Schedule(Action action, TimeSpan delay)
        {
            Contract.Requires(action != null);

            return this.Schedule(new ActionScheduledTask(this, action, PreciseTimeSpan.Deadline(delay)));
        }

        public override IScheduledTask Schedule(Action<object> action, object state, TimeSpan delay)
        {
            Contract.Requires(action != null);

            return this.Schedule(new StateActionScheduledTask(this, action, state, PreciseTimeSpan.Deadline(delay)));
        }

        public override IScheduledTask Schedule(Action<object, object> action, object context, object state, TimeSpan delay)
        {
            Contract.Requires(action != null);

            return this.Schedule(new StateActionWithContextScheduledTask(this, action, context, state, PreciseTimeSpan.Deadline(delay)));
        }

        public override Task ScheduleAsync(Action action, TimeSpan delay, CancellationToken cancellationToken)
        {
            Contract.Requires(action != null);

            if (cancellationToken.IsCancellationRequested)
            {
                return TaskEx.Cancelled;
            }

            if (!cancellationToken.CanBeCanceled)
            {
                return this.Schedule(action, delay).Completion;
            }

            return this.Schedule(new ActionScheduledAsyncTask(this, action, PreciseTimeSpan.Deadline(delay), cancellationToken)).Completion;
        }

        public override Task ScheduleAsync(Action<object> action, object state, TimeSpan delay, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return TaskEx.Cancelled;
            }

            if (!cancellationToken.CanBeCanceled)
            {
                return this.Schedule(action, state, delay).Completion;
            }

            return this.Schedule(new StateActionScheduledAsyncTask(this, action, state, PreciseTimeSpan.Deadline(delay), cancellationToken)).Completion;
        }

        public override Task ScheduleAsync(Action<object, object> action, object context, object state, TimeSpan delay, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return TaskEx.Cancelled;
            }

            if (!cancellationToken.CanBeCanceled)
            {
                return this.Schedule(action, context, state, delay).Completion;
            }

            return this.Schedule(new StateActionWithContextScheduledAsyncTask(this, action, context, state, PreciseTimeSpan.Deadline(delay), cancellationToken)).Completion;
        }

        protected IScheduledRunnable Schedule(IScheduledRunnable task)
        {
            if (this.InEventLoop)
            {
                this.ScheduledTaskQueue.Enqueue(task);
            }
            else
            {
                this.Execute((e, t) => ((AbstractScheduledEventExecutor)e).ScheduledTaskQueue.Enqueue((IScheduledRunnable)t), this, task);
            }
            return task;
        }

        internal void RemoveScheduled(IScheduledRunnable task)
        {
            if (this.InEventLoop)
            {
                this.ScheduledTaskQueue.Remove(task);
            }
            else
            {
                this.Execute((e, t) => ((AbstractScheduledEventExecutor)e).ScheduledTaskQueue.Remove((IScheduledRunnable)t), this, task);
            }
        }
    }
}