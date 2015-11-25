// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Groups
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;

    public class DefaultChannelGroup : IChannelGroup
    {
        static int nextId;
        readonly IEventExecutor executor;
        readonly string name;
        readonly ConcurrentDictionary<IChannelId, IChannel> nonServerChannels = new ConcurrentDictionary<IChannelId, IChannel>();
        readonly ConcurrentDictionary<IChannelId, IChannel> serverChannels = new ConcurrentDictionary<IChannelId, IChannel>();

        public DefaultChannelGroup(IEventExecutor executor)
            : this(string.Format("group-{0:X2}", Interlocked.Increment(ref nextId)), executor)
        {
        }

        public DefaultChannelGroup(string name, IEventExecutor executor)
        {
            if (name == null)
            {
                throw new ArgumentNullException("name");
            }
            this.name = name;
            this.executor = executor;
        }

        public bool IsEmpty
        {
            get { return this.serverChannels.Count == 0 && this.nonServerChannels.Count == 0; }
        }

        public string Name
        {
            get { return this.name; }
        }

        public IChannel Find(IChannelId id)
        {
            IChannel channel = null;
            if (this.nonServerChannels.TryGetValue(id, out channel))
            {
                return channel;
            }
            else if (this.serverChannels.TryGetValue(id, out channel))
            {
                return channel;
            }
            else
            {
                return channel;
            }
        }

        public Task WriteAsync(object message)
        {
            return this.WriteAsync(message, ChannelMatchers.All());
        }

        public Task WriteAsync(object message, IChannelMatcher matcher)
        {
            Contract.Requires(message != null);
            Contract.Requires(matcher != null);
            var futures = new Dictionary<IChannel, Task>();
            foreach (IChannel c in this.nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.WriteAsync(SafeDuplicate(message)));
                }
            }

            ReferenceCountUtil.Release(message);
            return new DefaultChannelGroupCompletionSource(this, futures /*, this.executor*/).Task;
        }

        public IChannelGroup Flush(IChannelMatcher matcher)
        {
            foreach (IChannel c in this.nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    c.Flush();
                }
            }
            return this;
        }

        public IChannelGroup Flush()
        {
            return this.Flush(ChannelMatchers.All());
        }

        public int CompareTo(IChannelGroup other)
        {
            int v = string.Compare(this.Name, other.Name, StringComparison.Ordinal);
            if (v != 0)
            {
                return v;
            }

            return this.GetHashCode() - other.GetHashCode();
        }

        void ICollection<IChannel>.Add(IChannel item)
        {
            this.Add(item);
        }

        public void Clear()
        {
            this.serverChannels.Clear();
            this.nonServerChannels.Clear();
        }

        public bool Contains(IChannel item)
        {
            IChannel channel;
            if (item is IServerChannel)
            {
                return this.serverChannels.TryGetValue(item.Id, out channel) && channel == item;
            }
            else
            {
                return this.nonServerChannels.TryGetValue(item.Id, out channel) && channel == item;
            }
        }

        public void CopyTo(IChannel[] array, int arrayIndex)
        {
            this.ToArray().CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return this.nonServerChannels.Count + this.serverChannels.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(IChannel channel)
        {
            IChannel ch = null;
            if (channel is IServerChannel)
            {
                return this.serverChannels.TryRemove(channel.Id, out ch);
            }
            else
            {
                return this.nonServerChannels.TryRemove(channel.Id, out ch);
            }
        }

        public IEnumerator<IChannel> GetEnumerator()
        {
            return new CombinedEnumerator<IChannel>(this.serverChannels.Values.GetEnumerator(),
                this.nonServerChannels.Values.GetEnumerator());
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new CombinedEnumerator<IChannel>(this.serverChannels.Values.GetEnumerator(),
                this.nonServerChannels.Values.GetEnumerator());
        }

        public Task WriteAndFlushAsync(object message)
        {
            return this.WriteAndFlushAsync(message, ChannelMatchers.All());
        }

        public Task WriteAndFlushAsync(object message, IChannelMatcher matcher)
        {
            Contract.Requires(message != null);
            Contract.Requires(matcher != null);
            var futures = new Dictionary<IChannel, Task>();
            foreach (IChannel c in this.nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.WriteAndFlushAsync(SafeDuplicate(message)));
                }
            }

            ReferenceCountUtil.Release(message);
            return new DefaultChannelGroupCompletionSource(this, futures /*, this.executor*/).Task;
        }

        public Task DisconnectAsync()
        {
            return this.DisconnectAsync(ChannelMatchers.All());
        }

        public Task DisconnectAsync(IChannelMatcher matcher)
        {
            Contract.Requires(matcher != null);
            var futures = new Dictionary<IChannel, Task>();
            foreach (IChannel c in this.nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.DisconnectAsync());
                }
            }
            foreach (IChannel c in this.serverChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.DisconnectAsync());
                }
            }

            return new DefaultChannelGroupCompletionSource(this, futures /*, this.executor*/).Task;
        }

        public Task CloseAsync()
        {
            return this.CloseAsync(ChannelMatchers.All());
        }

        public Task CloseAsync(IChannelMatcher matcher)
        {
            Contract.Requires(matcher != null);
            var futures = new Dictionary<IChannel, Task>();
            foreach (IChannel c in this.nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.CloseAsync());
                }
            }
            foreach (IChannel c in this.serverChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.CloseAsync());
                }
            }

            return new DefaultChannelGroupCompletionSource(this, futures /*, this.executor*/).Task;
        }

        public Task DeregisterAsync()
        {
            return this.DeregisterAsync(ChannelMatchers.All());
        }

        public Task DeregisterAsync(IChannelMatcher matcher)
        {
            Contract.Requires(matcher != null);
            var futures = new Dictionary<IChannel, Task>();
            foreach (IChannel c in this.nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.DeregisterAsync());
                }
            }
            foreach (IChannel c in this.serverChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.DeregisterAsync());
                }
            }

            return new DefaultChannelGroupCompletionSource(this, futures /*, this.executor*/).Task;
        }

        public Task NewCloseFuture()
        {
            return this.NewCloseFuture(ChannelMatchers.All());
        }

        public Task NewCloseFuture(IChannelMatcher matcher)
        {
            Contract.Requires(matcher != null);
            var futures = new Dictionary<IChannel, Task>();
            foreach (IChannel c in this.nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.CloseCompletion);
                }
            }
            foreach (IChannel c in this.serverChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.CloseCompletion);
                }
            }

            return new DefaultChannelGroupCompletionSource(this, futures /*, this.executor*/).Task;
        }

        static object SafeDuplicate(object message)
        {
            var buffer = message as IByteBuffer;
            if (buffer != null)
            {
                return buffer.Duplicate().Retain();
            }

            var byteBufferHolder = message as IByteBufferHolder;
            if (byteBufferHolder != null)
            {
                return byteBufferHolder.Duplicate().Retain();
            }

            return ReferenceCountUtil.Retain(message);
        }

        public override bool Equals(object obj)
        {
            return this == obj;
        }

        public override string ToString()
        {
            return string.Format("{0}(name: {1}, size: {2})", this.GetType().Name, this.Name, this.Count);
        }

        public bool Add(IChannel channel)
        {
            ConcurrentDictionary<IChannelId, IChannel> map = channel is IServerChannel ? this.serverChannels : this.nonServerChannels;
            bool added = map.TryAdd(channel.Id, channel);
            if (added)
            {
                channel.CloseCompletion.ContinueWith(x => this.Remove(channel));
            }
            return added;
        }

        public IChannel[] ToArray()
        {
            var channels = new List<IChannel>(this.Count);
            channels.AddRange(this.serverChannels.Values);
            channels.AddRange(this.nonServerChannels.Values);
            return channels.ToArray();
        }

        public bool Remove(IChannelId channelId)
        {
            IChannel ch;

            if (this.serverChannels.TryRemove(channelId, out ch))
            {
                return true;
            }

            if (this.nonServerChannels.TryRemove(channelId, out ch))
            {
                return true;
            }

            return false;
        }

        public bool Remove(object o)
        {
            var id = o as IChannelId;
            if (id != null)
            {
                return this.Remove(id);
            }
            else
            {
                var channel = o as IChannel;
                if (channel != null)
                {
                    return this.Remove(channel);
                }
            }
            return false;
        }
    }
}