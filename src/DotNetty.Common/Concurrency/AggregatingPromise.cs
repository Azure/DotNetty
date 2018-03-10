// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks.Sources;

    public sealed class AggregatingPromise : AbstractPromise
    {
        readonly IList<IValueTaskSource> futures;
        int successCount;
        int failureCount;

        IList<Exception> failures;

        public AggregatingPromise(IList<IValueTaskSource> futures)
        {
            Contract.Requires(futures != null);
            this.futures = futures;

            foreach (IValueTaskSource future in futures)
            {
                future.OnCompleted(this.OnFutureCompleted, future, 0, ValueTaskSourceOnCompletedFlags.None);
            }

            // Done on arrival?
            if (futures.Count == 0)
            {
                this.TryComplete();
            }
        }

        void OnFutureCompleted(object obj)
        {
            IValueTaskSource future = obj as IValueTaskSource;
            Contract.Assert(future != null);
            
            try
            {
                future.GetResult(0);
                this.successCount++;
            }
            catch(Exception ex)
            {
                this.failureCount++;
                
                if (this.failures == null)
                {
                    this.failures = new List<Exception>();
                }
                this.failures.Add(ex);
            }

            bool callSetDone = this.successCount + this.failureCount == this.futures.Count;
            Contract.Assert(this.successCount + this.failureCount <= this.futures.Count);

            if (callSetDone)
            {
                if (this.failureCount > 0)
                {
                    this.TrySetException(new AggregateException(this.failures));
                }
                else
                {
                    this.TryComplete();
                }
            }
        }
    }
}