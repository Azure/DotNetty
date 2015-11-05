// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Threading;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// Abstract base class for <see cref="IEventExecutor"/>s that need to support scheduling.
    /// </summary>
    public abstract class AbstractScheduledEventExecutor : AbstractEventExecutor
    {
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
        /// Cancel all scheduled tasks
        /// 
        /// This method MUST be called only when <see cref="IEventExecutor.InEventLoop"/> is <c>true</c>.
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
                // TODO: cancellation support
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
            return nextScheduledRunnable == null ? PreciseTimeSpan.Zero : nextScheduledRunnable.Deadline;
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

        public override void Schedule(Action<object> action, object state, TimeSpan delay)
        {
            this.Schedule(action, state, delay, CancellationToken.None);
        }

        public override void Schedule(Action action, TimeSpan delay)
        {
            this.Schedule(action, delay, CancellationToken.None);
        }

        public override void Schedule(Action action, TimeSpan delay, CancellationToken cancellationToken)
        {
            // todo: check for allocation
            this.Schedule(_ => action(), null, delay, cancellationToken);
        }

        public override void Schedule(Action<object> action, object state, TimeSpan delay, CancellationToken cancellationToken)
        {
            var queueNode = new ScheduledTask(action, state, PreciseTimeSpan.Deadline(delay), cancellationToken);
            if (this.InEventLoop)
            {
                this.ScheduledTaskQueue.Enqueue(queueNode);
            }
            else
            {
                this.Execute(e => ((AbstractScheduledEventExecutor)e).ScheduledTaskQueue.Enqueue(queueNode), this); // it is an allocation but it should not happen often (cross-thread scheduling)
            }
        }

        public override void Schedule(Action<object, object> action, object context, object state, TimeSpan delay)
        {
            this.Schedule(action, context, state, delay, CancellationToken.None);
        }

        public override void Schedule(Action<object, object> action, object context, object state, TimeSpan delay, CancellationToken cancellationToken)
        {
            var queueNode = new ScheduledTaskWithContext(action, context, state, PreciseTimeSpan.Deadline(delay), cancellationToken);
            if (this.InEventLoop)
            {
                this.ScheduledTaskQueue.Enqueue(queueNode);
            }
            else
            {
                this.Execute(e => ((AbstractScheduledEventExecutor)e).ScheduledTaskQueue.Enqueue(queueNode), this); // it is an allocation but it should not happen often (cross-thread scheduling)
            }
        }

        #region Scheduled task data structures

        protected interface IScheduledRunnable : IRunnable, IComparable<IScheduledRunnable>
        {
            PreciseTimeSpan Deadline { get; }

            //TODO: need an ability to cancel IScheduledRunnable directly
        }

        protected class ScheduledTask : MpscLinkedQueueNode<IRunnable>, IScheduledRunnable
        {
            readonly Action<object> action;
            readonly object state;

            public ScheduledTask(Action<object> action, object state, PreciseTimeSpan deadline,
                CancellationToken cancellationToken)
            {
                this.action = action;
                this.state = state;
                this.Deadline = deadline;
                this.CancellationToken = cancellationToken;
            }

            public PreciseTimeSpan Deadline { get; private set; }

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
                if (!this.CancellationToken.IsCancellationRequested)
                {
                    this.action(this.state);
                }
            }
        }

        protected class ScheduledTaskWithContext : MpscLinkedQueueNode<IRunnable>, IScheduledRunnable
        {
            readonly Action<object, object> action;
            readonly object context;
            readonly object state;

            public ScheduledTaskWithContext(Action<object, object> action, object context, object state, PreciseTimeSpan deadline,
                CancellationToken cancellationToken)
            {
                this.action = action;
                this.context = context;
                this.state = state;
                this.Deadline = deadline;
                this.CancellationToken = cancellationToken;
            }

            public PreciseTimeSpan Deadline { get; private set; }

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
                if (!this.CancellationToken.IsCancellationRequested)
                {
                    this.action(this.context, this.state);
                }
            }
        }

        #endregion
    }
}