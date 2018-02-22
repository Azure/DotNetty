// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;

    public sealed class AggregatingPromise : AbstractChannelPromise
    {
        int successCount;
        int failureCount;

        IList<Exception> failures;

        public AggregatingPromise(IList<ChannelFuture> futures)
        {
            Contract.Requires(futures != null);

            foreach (ChannelFuture future in futures)
            {
                future.OnCompleted(
                    () =>
                    {
                        try
                        {
                            future.GetResult();
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

                        bool callSetDone = this.successCount + this.failureCount == futures.Count;
                        Contract.Assert(this.successCount + this.failureCount <= futures.Count);

                        if (callSetDone)
                        {
                            if (this.failureCount > 0)
                            {
                                this.TryComplete(new AggregateException(this.failures));
                            }
                            else
                            {
                                this.TryComplete();
                            }
                        }
                    });
            }

            // Done on arrival?
            if (futures.Count == 0)
            {
                this.TryComplete();
            }
        }
    }
}