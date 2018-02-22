// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Groups
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    public sealed class DefaultChannelGroupPromise : AbstractChannelPromise
    {
        readonly Dictionary<IChannel, ChannelFuture> futures;
        int failureCount;
        int successCount;
        IList<KeyValuePair<IChannel, Exception>> failures;

        public DefaultChannelGroupPromise(Dictionary<IChannel, ChannelFuture> futures /*, IEventExecutor executor*/)
        {
            Contract.Requires(futures != null);

            this.futures = new Dictionary<IChannel, ChannelFuture>();
            foreach (KeyValuePair<IChannel, ChannelFuture> pair in futures)
            {
                this.futures.Add(pair.Key, pair.Value);
                pair.Value.OnCompleted(() =>
                {
                    try
                    {
                        pair.Value.GetResult();
                        this.successCount++;
                    }
                    catch(Exception ex)
                    {
                        this.failureCount++;
                        if (this.failures == null)
                        {
                            this.failures = new List<KeyValuePair<IChannel, Exception>>();
                        }
                        this.failures.Add(new KeyValuePair<IChannel, Exception>(pair.Key, ex));
                    }

                    bool callSetDone = this.successCount + this.failureCount == futures.Count;
                    Contract.Assert(this.successCount + this.failureCount <= futures.Count);

                    if (callSetDone)
                    {
                        if (this.failureCount > 0)
                        {
                            this.TryComplete(new ChannelGroupException(this.failures));
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