// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;

    sealed class StateActionScheduledTask : ScheduledTask
    {
        readonly Action<object> action;

        public StateActionScheduledTask(AbstractScheduledEventExecutor executor, Action<object> action, object state, PreciseTimeSpan deadline)
            : base(executor, deadline, new TaskCompletionSource(state))
        {
            this.action = action;
        }

        protected override void Execute() => this.action(this.Completion.AsyncState);
    }
}