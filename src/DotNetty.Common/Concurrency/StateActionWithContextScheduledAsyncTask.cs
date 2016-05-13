// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Threading;

    sealed class StateActionWithContextScheduledAsyncTask : ScheduledAsyncTask
    {
        readonly Action<object, object> action;
        readonly object context;

        public StateActionWithContextScheduledAsyncTask(AbstractScheduledEventExecutor executor, Action<object, object> action, object context, object state,
            PreciseTimeSpan deadline, CancellationToken cancellationToken)
            : base(executor, deadline, new TaskCompletionSource(state), cancellationToken)
        {
            this.action = action;
            this.context = context;
        }

        protected override void Execute() => this.action(this.context, this.Completion.AsyncState);
    }
}