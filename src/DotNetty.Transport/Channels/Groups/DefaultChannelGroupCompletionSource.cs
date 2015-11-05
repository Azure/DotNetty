using DotNetty.Common.Concurrency;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace DotNetty.Transport.Channels.Groups
{
    public class DefaultChannelGroupCompletionSource : TaskCompletionSource<int>, IChannelGroupTaskCompletionSource
    {
        readonly IChannelGroup group;
        readonly Dictionary<IChannel, Task> futures;
        int successCount;
        int failureCount;

        public DefaultChannelGroupCompletionSource(IChannelGroup group, Dictionary<IChannel, Task> futures /*, IEventExecutor executor*/)
            : this(group, futures /*,executor*/, null)
        {
        }

        public DefaultChannelGroupCompletionSource(IChannelGroup group, Dictionary<IChannel, Task> futures /*, IEventExecutor executor*/, object state)
            : base(state)
        {
            Contract.Requires(group != null);
            Contract.Requires(futures != null);

            this.group = group;
            foreach (var pair in futures)
            {
                this.futures.Add(pair.Key, pair.Value);
                pair.Value.ContinueWith(x =>
                {
                    bool success = x.IsCompleted && !x.IsFaulted && !x.IsCanceled;
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
                        Debug.Assert(this.successCount + this.failureCount <= this.futures.Count);
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

        public IChannelGroup Group
        {
            get { return this.group; }
        }

        public Task Find(IChannel channel)
        {
            return this.futures[channel];
        }

        public bool IsPartialSucess()
        {
            lock (this)
            {
                return this.successCount != 0 && this.successCount != this.futures.Count;
            }
        }

        public bool IsSucess()
        {
            return this.Task.IsCompleted && !this.Task.IsFaulted && !this.Task.IsCanceled;
        }

        public bool IsPartialFailure()
        {
            lock (this)
            {
                return this.failureCount != 0 && this.failureCount != this.futures.Count;
            }
        }

        public ChannelGroupException Cause
        {
            get { return (ChannelGroupException)this.Task.Exception.InnerException; }
        }

        public Task Current
        {
            get { return this.futures.Values.GetEnumerator().Current; }
        }

        public void Dispose()
        {
            this.futures.Values.GetEnumerator().Dispose();
        }

        object System.Collections.IEnumerator.Current
        {
            get { return this.futures.Values.GetEnumerator().Current; }
        }

        public bool MoveNext()
        {
            return this.futures.Values.GetEnumerator().MoveNext();
        }

        public void Reset()
        {
            ((System.Collections.IEnumerator)this.futures.Values.GetEnumerator()).Reset();
        }
    }
}