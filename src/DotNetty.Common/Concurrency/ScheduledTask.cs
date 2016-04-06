// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;

    abstract class ScheduledTask : MpscLinkedQueueNode<IRunnable>, IScheduledRunnable
    {
        const int CancellationProhibited = 1;
        const int CancellationRequested = 1 << 1;

        protected readonly TaskCompletionSource Promise;
        protected readonly AbstractScheduledEventExecutor Executor;
        int volatileCancellationState;

        protected ScheduledTask(AbstractScheduledEventExecutor executor, PreciseTimeSpan deadline, TaskCompletionSource promise)
        {
            this.Executor = executor;
            this.Promise = promise;
            this.Deadline = deadline;
        }

        public PreciseTimeSpan Deadline { get; private set; }

        public bool Cancel()
        {
            if (!this.AtomicCancellationStateUpdate(CancellationProhibited, CancellationRequested))
            {
                return false;
            }
            
            bool canceled = this.Promise.TrySetCanceled();
            if (canceled)
            {
                this.Executor.RemoveScheduled(this);
            }
            return canceled;
        }

        public Task Completion
        {
            get { return this.Promise.Task; }
        }

        public TaskAwaiter GetAwaiter()
        {
            return this.Completion.GetAwaiter();
        }

        int IComparable<IScheduledRunnable>.CompareTo(IScheduledRunnable other)
        {
            Contract.Requires(other != null);

            return this.Deadline.CompareTo(other.Deadline);
        }

        public override IRunnable Value
        {
            get { return this; }
        }

        public virtual void Run()
        {
            if (this.TrySetUncancelable())
            {
                try
                {
                    this.Execute();
                    this.Promise.TryComplete();
                }
                catch (Exception ex)
                {
                    // todo: check for fatal
                    this.Promise.TrySetException(ex);
                }
            }
        }

        protected abstract void Execute();

        bool TrySetUncancelable()
        {
            return this.AtomicCancellationStateUpdate(CancellationProhibited, CancellationRequested);
        }

        bool AtomicCancellationStateUpdate(int newBits, int illegalBits)
        {
            int cancellationState = Volatile.Read(ref this.volatileCancellationState);
            int oldCancellationState;
            do
            {
                oldCancellationState = cancellationState;
                if ((cancellationState & illegalBits) != 0)
                {
                    return false;
                }
                cancellationState = Interlocked.CompareExchange(ref this.volatileCancellationState, cancellationState | newBits, cancellationState);
            }
            while (cancellationState != oldCancellationState);

            return true;
        }
    }
}