using DotNetty.Buffers;
using DotNetty.Common.Concurrency;
using DotNetty.Common.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetty.Transport.Channels.Groups
{
    public class DefaultChannelGroup : ISet<IChannel>, IChannelGroup
    {
        private static int nextId = 0;
        private readonly string name;
        private readonly IEventExecutor executor;
        private readonly ConcurrentDictionary<IChannelId, IChannel> serverChannels = new ConcurrentDictionary<IChannelId, IChannel>();
        private readonly ConcurrentDictionary<IChannelId, IChannel> nonServerChannels = new ConcurrentDictionary<IChannelId, IChannel>();

        public DefaultChannelGroup(IEventExecutor executor)
            :this(string.Format("group-{0:X2}",Interlocked.Increment(ref nextId)),executor)
        {

        }

        public DefaultChannelGroup(string name, IEventExecutor executor)
        {
            if (name == null)
                throw new ArgumentNullException("name");
            this.name = name;
            this.executor = executor;

        }

        public string Name
        {
            get { return this.name; }
        }

        public IChannel Find(IChannelId id)
        {
            IChannel channel = null;
            if (this.nonServerChannels.TryGetValue(id, out channel))
                return channel;
            else if (this.serverChannels.TryGetValue(id, out channel))
                return channel;
            else
                return channel;
            
        }

        public Task WriteAsync(object message)
        {
            return WriteAsync(message, ChannelMatchers.All());
        }

        public Task WriteAsync(object message, IChannelMatcher matcher)
        {
           Contract.Requires(message != null);
           Contract.Requires(matcher != null);
           var futures = new Dictionary<IChannel,Task>();
            foreach(var c in nonServerChannels.Values)
            {
                if(matcher.Matches(c))
                {
                    futures.Add(c, c.WriteAsync(message));
                }
            }

            ReferenceCountUtil.Release(message);
            return new DefaultChannelGroupCompletionSource(this, futures/*, this.executor*/).Task;
        }

        private static object SafeDuplicate(object message)
        {
            if(message is IByteBuffer)
            {
                return ((IByteBuffer)message).Duplicate().Retain();
            }
            else if(message is IByteBufferHolder)
            {
                return ((IByteBufferHolder)message).Duplicate().Retain();
            }
            else
            {
                return ReferenceCountUtil.Retain(message);
            }
        }

        public IChannelGroup Flush(IChannelMatcher matcher)
        {
            foreach (var c in nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                    c.Flush();
            }
            return this;
        }

        public IChannelGroup Flush()
        {
            return Flush(ChannelMatchers.All());
        }
       

        public int CompareTo(IChannelGroup other)
        {
            int v = Name.CompareTo(other.Name);
            if (v != 0)
            {
                return v;
            }

            return RuntimeHelpers.GetHashCode(this) - RuntimeHelpers.GetHashCode(other);
        }

        public override bool Equals(object obj)
        {
            return this == obj;
        }

        public override int GetHashCode()
        {
            return RuntimeHelpers.GetHashCode(this);
        }

        public override string ToString()
        {
            return string.Format("{0}(name: {1}, size: {2})", this.GetType().Name, Name, Count);
        }

        public bool Add(IChannel channel)
        {
            var map = channel is IServerChannel ? serverChannels : nonServerChannels;
            bool added = map.TryAdd(channel.Id, channel);
            if (added)
                channel.CloseCompletion.ContinueWith(x => Remove(channel));
            return added;

        }

        public void ExceptWith(IEnumerable<IChannel> other)
        {
            
            throw new NotImplementedException();
        }

        public void IntersectWith(IEnumerable<IChannel> other)
        {
            throw new NotImplementedException();
        }

        public bool IsProperSubsetOf(IEnumerable<IChannel> other)
        {
            throw new NotImplementedException();
        }

        public bool IsProperSupersetOf(IEnumerable<IChannel> other)
        {
            throw new NotImplementedException();
        }

        public bool IsSubsetOf(IEnumerable<IChannel> other)
        {
            throw new NotImplementedException();
        }

        public bool IsSupersetOf(IEnumerable<IChannel> other)
        {
            throw new NotImplementedException();
        }

        public bool Overlaps(IEnumerable<IChannel> other)
        {
            throw new NotImplementedException();
        }

        public bool SetEquals(IEnumerable<IChannel> other)
        {
            throw new NotImplementedException();
        }

        public void SymmetricExceptWith(IEnumerable<IChannel> other)
        {
            throw new NotImplementedException();
        }

        public void UnionWith(IEnumerable<IChannel> other)
        {
            throw new NotImplementedException();
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
            if (item is IServerChannel)
                return this.serverChannels.Values.Contains(item);
            else
                return this.nonServerChannels.Values.Contains(item);
        }

        public void CopyTo(IChannel[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IChannel[] ToArray()
        {
            var channels = new List<IChannel>(Count);
            channels.AddRange(this.serverChannels.Values);
            channels.AddRange(this.nonServerChannels.Values);
            return channels.ToArray();

        }

        public bool IsEmpty
        {
            get
            {
                return this.serverChannels.Count == 0 && this.nonServerChannels.Count == 0;
            }
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
            if(channel is IServerChannel)
            {
                return this.serverChannels.TryRemove(channel.Id, out ch);
            }
            else
            {
                return this.nonServerChannels.TryRemove(channel.Id, out ch);
            }

        }

        public bool Remove(IChannelId channelId)
        {
            IChannel ch = null;
            
            if (this.serverChannels.TryRemove(channelId, out ch))
            {
                return true;
            }
            else if (this.nonServerChannels.TryRemove(channelId, out ch))
            {
                return true;
            }
            
            return false;
        }

        public bool Remove(object o)
        {
            if (o is IChannelId)
                return Remove((IChannelId)o);
            else if (o is IChannel)
                return Remove((IChannel)o);
            return false;
        }

        public IEnumerator<IChannel> GetEnumerator()
        {
            return new CombinedEnumerator<IChannel>(this.serverChannels.Values.GetEnumerator(),
                this.nonServerChannels.Values.GetEnumerator());
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new CombinedEnumerator<IChannel>(this.serverChannels.Values.GetEnumerator(),
                this.nonServerChannels.Values.GetEnumerator());
        }


        public Task WriteAndFlushAsync(object message)
        {
            return WriteAndFlushAsync(message, ChannelMatchers.All());
        }

        public Task WriteAndFlushAsync(object message, IChannelMatcher matcher)
        {
            Contract.Requires(message != null);
            Contract.Requires(matcher != null);
            var futures = new Dictionary<IChannel, Task>();
            foreach (var c in nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.WriteAndFlushAsync(message));
                }
            }

            ReferenceCountUtil.Release(message);
            return new DefaultChannelGroupCompletionSource(this, futures/*, this.executor*/).Task;

        }

        public Task DisconnectAsync()
        {
            return DisconnectAsync(ChannelMatchers.All());
        }

        public Task DisconnectAsync(IChannelMatcher matcher)
        {
            Contract.Requires(matcher != null);
            var futures = new Dictionary<IChannel, Task>();
            foreach (var c in nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.DisconnectAsync());
                }
            }
            foreach (var c in serverChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.DisconnectAsync());
                }
            }

            
            return new DefaultChannelGroupCompletionSource(this, futures/*, this.executor*/).Task;
        }

        public Task CloseAsync()
        {
            return CloseAsync(ChannelMatchers.All());
        }

        public Task CloseAsync(IChannelMatcher matcher)
        {
            Contract.Requires(matcher != null);
            var futures = new Dictionary<IChannel, Task>();
            foreach (var c in nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.CloseAsync());
                }
            }
            foreach (var c in serverChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.CloseAsync());
                }
            }


            return new DefaultChannelGroupCompletionSource(this, futures/*, this.executor*/).Task;
        }

        public Task DeregisterAsync()
        {
            return DeregisterAsync(ChannelMatchers.All());
        }

        public Task DeregisterAsync(IChannelMatcher matcher)
        {
            Contract.Requires(matcher != null);
            var futures = new Dictionary<IChannel, Task>();
            foreach (var c in nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.DeregisterAsync());
                }
            }
            foreach (var c in serverChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.DeregisterAsync());
                }
            }


            return new DefaultChannelGroupCompletionSource(this, futures/*, this.executor*/).Task;
        }

        public Task NewCloseFuture()
        {
            return NewCloseFuture(ChannelMatchers.All());
        }

        public Task NewCloseFuture(IChannelMatcher matcher)
        {
            Contract.Requires(matcher != null);
            var futures = new Dictionary<IChannel, Task>();
            foreach (var c in nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.CloseCompletion);
                }
            }
            foreach (var c in serverChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.CloseCompletion);
                }
            }


            return new DefaultChannelGroupCompletionSource(this, futures/*, this.executor*/).Task;
        }
    }
}
