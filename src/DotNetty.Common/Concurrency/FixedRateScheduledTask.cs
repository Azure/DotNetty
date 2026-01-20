// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace DotNetty.Common.Concurrency
{
    sealed class FixedRateScheduledTask : ScheduledTask
    {
        readonly Action action;
        public FixedRateScheduledTask(AbstractScheduledEventExecutor executor, IRunnable action, PreciseTimeSpan deadline, PreciseTimeSpan period)
            : this(executor, action.Run, deadline, period)
        {
        }

        public FixedRateScheduledTask(AbstractScheduledEventExecutor executor, Action action, PreciseTimeSpan deadline, PreciseTimeSpan period)
            : base(executor, deadline, new TaskCompletionSource())
        {
            if (period.Ticks <= 0)
                throw new ArgumentException("period: 0 (expected: != 0)");

            this.Period = period;
            this.action = action;
        }

        public PreciseTimeSpan Period { get; }

        protected override void Execute() => this.action();

        public override void Run()
        {
            try
            {
                this.Execute();
                if (!Executor.IsShutdown)
                {
                    this.Deadline = PreciseTimeSpan.FromTicks(Deadline.Ticks + Period.Ticks);
                    this.Executor.Schedule(this);
                }
                else
                {
                    this.Promise.TryComplete();
                }
            }
            catch (Exception ex)
            {
                // todo: check for fatal
                this.Promise.TrySetException(ex);
            }
        }
    }
}
