// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;

    sealed class ActionScheduledTask : ScheduledTask
    {
        readonly Action action;

        public ActionScheduledTask(AbstractScheduledEventExecutor executor, Action action, PreciseTimeSpan deadline)
            : base(executor, deadline, new TaskCompletionSource())
        {
            this.action = action;
        }

        protected override void Execute() => this.action();
    }
}