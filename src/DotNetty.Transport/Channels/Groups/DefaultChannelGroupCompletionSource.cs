// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Groups
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;

    public class DefaultChannelGroupCompletionSource : TaskCompletionSource<int>, IChannelGroupTaskCompletionSource
    {
        readonly Dictionary<IChannel, Task> futures;
        int failureCount;
        int successCount;

        public DefaultChannelGroupCompletionSource(IChannelGroup group, Dictionary<IChannel, Task> futures /*, IEventExecutor executor*/)
            : this(group, futures /*,executor*/, null)
        {
        }

        public DefaultChannelGroupCompletionSource(IChannelGroup group, Dictionary<IChannel, Task> futures /*, IEventExecutor executor*/, object state)
            : base(state)
        {
            Contract.Requires(group != null);
            Contract.Requires(futures != null);

            this.Group = group;
            this.futures = new Dictionary<IChannel, Task>();
            foreach (KeyValuePair<IChannel, Task> pair in futures)
            {
                this.futures.Add(pair.Key, pair.Value);
                pair.Value.ContinueWith(x =>
                {
                    bool success = x.Status == TaskStatus.RanToCompletion;
                    bool callSetDone;
                    lock (this)
                    {
                        if (success)
                        {
                            this.successCount++;
                        }
                        else
                        {
                            this.failureCount++;
                        }

                        callSetDone = this.successCount + this.failureCount == this.futures.Count;
                        Contract.Assert(this.successCount + this.failureCount <= this.futures.Count);
                    }

                    if (callSetDone)
                    {
                        if (this.failureCount > 0)
                        {
                            var failed = new List<KeyValuePair<IChannel, Exception>>();
                            foreach (KeyValuePair<IChannel, Task> ft in this.futures)
                            {
                                IChannel c = ft.Key;
                                Task f = ft.Value;
                                if (f.IsFaulted || f.IsCanceled)
                                {
                                    if (f.Exception != null)
                                    {
                                        failed.Add(new KeyValuePair<IChannel, Exception>(c, f.Exception.InnerException));
                                    }
                                }
                            }
                            this.TrySetException(new ChannelGroupException(failed));
                        }
                        else
                        {
                            this.TrySetResult(0);
                        }
                    }
                });
            }

            // Done on arrival?
            if (futures.Count == 0)
            {
                this.TrySetResult(0);
            }
        }

        public IChannelGroup Group { get; }

        public Task Find(IChannel channel) => this.futures[channel];

        public bool IsPartialSucess()
        {
            lock (this)
            {
                return this.successCount != 0 && this.successCount != this.futures.Count;
            }
        }

        public bool IsSucess() => this.Task.IsCompleted && !this.Task.IsFaulted && !this.Task.IsCanceled;

        public bool IsPartialFailure()
        {
            lock (this)
            {
                return this.failureCount != 0 && this.failureCount != this.futures.Count;
            }
        }

        public ChannelGroupException Cause => (ChannelGroupException)this.Task.Exception.InnerException;

        public Task Current => this.futures.Values.GetEnumerator().Current;

        public void Dispose() => this.futures.Values.GetEnumerator().Dispose();

        object IEnumerator.Current => this.futures.Values.GetEnumerator().Current;

        public bool MoveNext() => this.futures.Values.GetEnumerator().MoveNext();

        public void Reset() => ((IEnumerator)this.futures.Values.GetEnumerator()).Reset();
    }
}