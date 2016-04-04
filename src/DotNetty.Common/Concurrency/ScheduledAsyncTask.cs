// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System.Threading;

    abstract class ScheduledAsyncTask : ScheduledTask
    {
        readonly CancellationToken cancellationToken;
        CancellationTokenRegistration cancellationTokenRegistration;

        protected ScheduledAsyncTask(AbstractScheduledEventExecutor executor, PreciseTimeSpan deadline, TaskCompletionSource promise, CancellationToken cancellationToken)
            : base(executor, deadline, promise)
        {
            this.cancellationToken = cancellationToken;
            this.cancellationTokenRegistration = cancellationToken.Register(s => ((ScheduledAsyncTask)s).Cancel(), this);
        }

        public override void Run()
        {
            this.cancellationTokenRegistration.Dispose();
            if (this.cancellationToken.IsCancellationRequested)
            {
                this.Promise.TrySetCanceled();
            }
            else
            {
                base.Run();
            }
        }
    }
}