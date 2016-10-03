// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;

    sealed class StateActionWithContextScheduledTask : ScheduledTask
    {
        readonly Action<object, object> action;
        readonly object context;

        public StateActionWithContextScheduledTask(AbstractScheduledEventExecutor executor, Action<object, object> action, object context, object state,
            PreciseTimeSpan deadline)
            : base(executor, deadline, new TaskCompletionSource(state))
        {
            this.action = action;
            this.context = context;
        }

        protected override void Execute() => this.action(this.context, this.Completion.AsyncState);
    }
}