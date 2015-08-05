using DotNetty.Common.Concurrency;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace DotNetty.Transport.Channels.Groups
{
    public class DefaultChannelGroupCompletionSource : TaskCompletionSource<int> ,IChannelGroupTaskCompletionSource
    {
        private readonly IChannelGroup group;
        private readonly Dictionary<IChannel,Task> futures;
        private int successCount;
        private int failureCount;
        public DefaultChannelGroupCompletionSource(IChannelGroup group, Dictionary<IChannel, Task> futures/*, IEventExecutor executor*/)
            :this(group,futures/*,executor*/,null)
        {
            
        }

        public DefaultChannelGroupCompletionSource(IChannelGroup group, Dictionary<IChannel, Task> futures/*, IEventExecutor executor*/, object state)
            :base(state)
        {
            Contract.Requires(group != null);
            Contract.Requires(futures != null);
            
            this.group = group;
            foreach(var pair in futures)
            {
                this.futures.Add(pair.Key,pair.Value);
                pair.Value.ContinueWith(x =>
                {
                    bool success = x.IsCompleted && !x.IsFaulted && !x.IsCanceled;
                    bool callSetDone;
                    lock (this)
                    {
                        if (success)
                            successCount++;
                        else
                            failureCount++;

                        callSetDone = successCount + failureCount == this.futures.Count;
                        Debug.Assert(successCount + failureCount <= this.futures.Count);
                    }

                    if (callSetDone)
                    {
                        if (failureCount > 0)
                        {
                            List<KeyValuePair<IChannel, Exception>> failed = new List<KeyValuePair<IChannel, Exception>>();
                            foreach (var ft in this.futures)
                            {
                                IChannel c = ft.Key;
                                Task f = ft.Value;
                                if (f.IsFaulted || f.IsCanceled)
                                    failed.Add(new KeyValuePair<IChannel, Exception>(c, f.Exception.InnerException));
                            }
                            TrySetException(new ChannelGroupException(failed));
                        }
                        else
                        {
                            TrySetResult(0);
                        }
                    }
                });
            }

            // Done on arrival?
            if (futures.Count == 0)
                TrySetResult(0);

        }

        public IChannelGroup Group
        {
            get
            {

                return this.group;
            }
        }

        public Task Find(IChannel channel)
        {
            return this.futures[channel];
        }

        
        public bool IsPartialSucess()
        {
            lock(this)
            {
                return successCount != 0 && successCount != futures.Count;
            }
        }

        public bool IsSucess()
        {
            return base.Task.IsCompleted && !base.Task.IsFaulted && !base.Task.IsCanceled;
        }

        public bool IsPartialFailure()
        {
            lock (this)
            {
                return failureCount != 0 && failureCount != futures.Count;
            }
        }

        public ChannelGroupException Cause
        {
            get { return (ChannelGroupException)Task.Exception.InnerException; }
        }

        public Task Current
        {
            get
            {
                return this.futures.Values.GetEnumerator().Current;
            }
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
