// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    sealed class RunnableScheduledTask : ScheduledTask
    {
        readonly IRunnable action;

        public RunnableScheduledTask(AbstractScheduledEventExecutor executor, IRunnable action, PreciseTimeSpan deadline)
            : base(executor, deadline, new TaskCompletionSource())
        {
            this.action = action;
        }

        protected override void Execute() => this.action.Run();
    }
}