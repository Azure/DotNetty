// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    abstract class ScheduledTask : IScheduledRunnable
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

        public PreciseTimeSpan Deadline { get; }

        public bool Cancel()
        {
            if (!this.AtomicCancellationStateUpdate(CancellationRequested, CancellationProhibited))
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

        public Task Completion => this.Promise.Task;

        public TaskAwaiter GetAwaiter() => this.Completion.GetAwaiter();

        int IComparable<IScheduledRunnable>.CompareTo(IScheduledRunnable other)
        {
            Contract.Requires(other != null);

            return this.Deadline.CompareTo(other.Deadline);
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

        bool TrySetUncancelable() => this.AtomicCancellationStateUpdate(CancellationProhibited, CancellationRequested);

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