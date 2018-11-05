// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Groups
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    public sealed class DefaultChannelGroupPromise : AbstractPromise
    {
        readonly int count;
        int failureCount;
        int successCount;
        IList<KeyValuePair<IChannel, Exception>> failures;

        public DefaultChannelGroupPromise(Dictionary<IChannel, ValueTask> futures)
        {
            Contract.Requires(futures != null);
            
            if (futures.Count == 0)
            {
                this.TryComplete();
            }
            else
            {
                this.count = futures.Count;
                foreach (KeyValuePair<IChannel, ValueTask> pair in futures)
                {
                    this.Await(pair);
                }
            }
        }

        async void Await(KeyValuePair<IChannel, ValueTask> pair)
        {
            try
            {
                await pair.Value;
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

            bool callSetDone = this.successCount + this.failureCount == this.count;
            Contract.Assert(this.successCount + this.failureCount <= this.count);

            if (callSetDone)
            {
                if (this.failureCount > 0)
                {
                    this.TrySetException(new ChannelGroupException(this.failures));
                }
                else
                {
                    this.TryComplete();
                }
            }
        }
        
    }
}