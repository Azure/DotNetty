// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Pool
{
    using System.Collections.Concurrent;
    using System.Diagnostics.Contracts;

    public abstract class AbstractChannelPoolMap<TKey, TPool> : IChannelPoolMap<TKey, TPool>
        //, Iterable<Entry<K, P>> 
        where TPool : IChannelPool

    {
        readonly ConcurrentDictionary<TKey, TPool> map = new ConcurrentDictionary<TKey, TPool>();

        public TPool Get(TKey key)
        {
            Contract.Requires(key != null);

            if (!this.map.TryGetValue(key, out TPool pool))
            {
                pool = this.NewPool(key);
                TPool old = this.map.GetOrAdd(key, pool);
                if (!ReferenceEquals(old, pool))
                {
                    // We need to destroy the newly created pool as we not use it.
                    pool.Dispose();
                    pool = old;
                }
            }

            return pool;
        }

        /**
         * Remove the {@link ChannelPool} from this {@link AbstractChannelPoolMap}. Returns {@code true} if removed,
         * {@code false} otherwise.
         *
         * Please note that {@code null} keys are not allowed.
         */
        public bool Remove(TKey key)
        {
            Contract.Requires(key != null);
            if (this.map.TryRemove(key, out TPool pool))
            {
                pool.Dispose();
                return true;
            }
            return false;
        }

        /*public final Iterator<Entry<K, P>> iterator() {
            return new ReadOnlyIterator<Entry<K, P>>(this.map.entrySet().iterator());
        }*/

        /**
         * Returns the number of {@link ChannelPool}s currently in this {@link AbstractChannelPoolMap}.
         */
        public int Count => this.map.Count;

        /**
         * Returns {@code true} if the {@link AbstractChannelPoolMap} is empty, otherwise {@code false}.
         */
        public bool IsEmpty => this.map.Count == 0;

        public bool Contains(TKey key)
        {
            Contract.Requires(key != null);
            return this.map.ContainsKey(key);
        }

        /**
         * Called once a new {@link ChannelPool} needs to be created as non exists yet for the {@code key}.
         */
        protected abstract TPool NewPool(TKey key);

        public void Dispose()
        {
            foreach (TKey key in this.map.Keys)
                this.Remove(key);
        }
    }
}