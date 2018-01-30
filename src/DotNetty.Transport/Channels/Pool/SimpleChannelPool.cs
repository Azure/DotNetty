// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Pool
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;

    /**
 * Simple {@link ChannelPool} implementation which will create new {@link Channel}s if someone tries to acquire
 * a {@link Channel} but none is in the pool atm. No limit on the maximal concurrent {@link Channel}s is enforced.
 *
 * This implementation uses LIFO order for {@link Channel}s in the {@link ChannelPool}.
 *
 */
    public class SimpleChannelPool : IChannelPool
    {
        public static readonly AttributeKey<SimpleChannelPool> PoolKey = AttributeKey<SimpleChannelPool>.NewInstance("channelPool");

        static readonly InvalidOperationException FullException = new InvalidOperationException("ChannelPool full");

        readonly IQueue<IChannel> store;

        /**
         * Creates a new instance using the {@link IChannelHealthChecker#ACTIVE}.
         *
         * @param bootstrap         the {@link Bootstrap} that is used for connections
         * @param handler           the {@link IChannelPoolHandler} that will be notified for the different pool actions
         */
        public SimpleChannelPool(Bootstrap bootstrap, IChannelPoolHandler handler)
            : this(bootstrap, handler, ChannelActiveHealthChecker.Instance)
        {
        }

        /**
         * Creates a new instance.
         *
         * @param bootstrap         the {@link Bootstrap} that is used for connections
         * @param handler           the {@link IChannelPoolHandler} that will be notified for the different pool actions
         * @param healthCheck       the {@link IChannelHealthChecker} that will be used to check if a {@link Channel} is
         *                          still healthy when obtain from the {@link ChannelPool}
         */
        public SimpleChannelPool(Bootstrap bootstrap, IChannelPoolHandler handler, IChannelHealthChecker healthChecker)
            : this(bootstrap, handler, healthChecker, true)
        {
        }

        /**
         * Creates a new instance.
         *
         * @param bootstrap          the {@link Bootstrap} that is used for connections
         * @param handler            the {@link IChannelPoolHandler} that will be notified for the different pool actions
         * @param healthCheck        the {@link IChannelHealthChecker} that will be used to check if a {@link Channel} is
         *                           still healthy when obtain from the {@link ChannelPool}
         * @param releaseHealthCheck will check channel health before offering back if this parameter set to {@code true};
         *                           otherwise, channel health is only checked at acquisition time
         */
        public SimpleChannelPool(Bootstrap bootstrap, IChannelPoolHandler handler, IChannelHealthChecker healthChecker, bool releaseHealthCheck)
            : this(bootstrap, handler, healthChecker, releaseHealthCheck, true)
        {
        }

        /**
         * Creates a new instance.
         *
         * @param bootstrap          the {@link Bootstrap} that is used for connections
         * @param handler            the {@link IChannelPoolHandler} that will be notified for the different pool actions
         * @param healthCheck        the {@link IChannelHealthChecker} that will be used to check if a {@link Channel} is
         *                           still healthy when obtain from the {@link ChannelPool}
         * @param releaseHealthCheck will check channel health before offering back if this parameter set to {@code true};
         *                           otherwise, channel health is only checked at acquisition time
         * @param lastRecentUsed    {@code true} {@link Channel} selection will be LIFO, if {@code false} FIFO.
         */
        public SimpleChannelPool(Bootstrap bootstrap, IChannelPoolHandler handler, IChannelHealthChecker healthChecker, bool releaseHealthCheck, bool lastRecentUsed)
        {
            Contract.Requires(handler != null);
            Contract.Requires(healthChecker != null);
            Contract.Requires(bootstrap != null);

            this.Handler = handler;
            this.HealthChecker = healthChecker;
            this.ReleaseHealthCheck = releaseHealthCheck;

            // Clone the original Bootstrap as we want to set our own handler
            this.Bootstrap = bootstrap.Clone();
            this.Bootstrap.Handler(new ActionChannelInitializer<IChannel>(this.OnChannelInitializing));
            this.store =
                lastRecentUsed
                    ? (IQueue<IChannel>)new CompatibleConcurrentStack<IChannel>()
                    : new CompatibleConcurrentQueue<IChannel>();
        }

        void OnChannelInitializing(IChannel channel)
        {
            Contract.Assert(channel.EventLoop.InEventLoop);
            this.Handler.ChannelCreated(channel);
        }

        /**
         * Returns the {@link Bootstrap} this pool will use to open new connections.
         *
         * @return the {@link Bootstrap} this pool will use to open new connections
         */
        internal Bootstrap Bootstrap { get; }

        /**
         * Returns the {@link IChannelPoolHandler} that will be notified for the different pool actions.
         *
         * @return the {@link IChannelPoolHandler} that will be notified for the different pool actions
         */
        internal IChannelPoolHandler Handler { get; }

        /**
         * Returns the {@link IChannelHealthChecker} that will be used to check if a {@link Channel} is healthy.
         *
         * @return the {@link IChannelHealthChecker} that will be used to check if a {@link Channel} is healthy
         */
        internal IChannelHealthChecker HealthChecker { get; }

        /**
         * Indicates whether this pool will check the health of channels before offering them back into the pool.
         *
         * @return {@code true} if this pool will check the health of channels before offering them back into the pool, or
         * {@code false} if channel health is only checked at acquisition time
         */
        internal bool ReleaseHealthCheck { get; }

        public virtual ValueTask<IChannel> AcquireAsync()
        {
            if (!this.TryPollChannel(out IChannel channel))
            {
                Bootstrap bs = this.Bootstrap.Clone();
                bs.Attribute(PoolKey, this);
                return new ValueTask<IChannel>(this.ConnectChannel(bs));
            }
            
            IEventLoop eventLoop = channel.EventLoop;
            if (eventLoop.InEventLoop)
            {
                return this.DoHealthCheck(channel);
            }
            else
            {
                var completionSource = new TaskCompletionSource<IChannel>();
                eventLoop.Execute(this.DoHealthCheck, channel, completionSource);
                return new ValueTask<IChannel>(completionSource.Task);    
            }
        }

        async void DoHealthCheck(object channel, object state)
        {
            var promise = state as TaskCompletionSource<IChannel>;
            try
            {
                var result = await this.DoHealthCheck((IChannel)channel);
                promise.TrySetResult(result);
            }
            catch (Exception ex)
            {
                promise.TrySetException(ex);
            }
        }

        async ValueTask<IChannel> DoHealthCheck(IChannel channel)
        {
            Contract.Assert(channel.EventLoop.InEventLoop);
            try
            {
                if (await this.HealthChecker.IsHealthyAsync(channel))
                {
                    try
                    {
                        channel.GetAttribute(PoolKey).Set(this);
                        this.Handler.ChannelAcquired(channel);
                        return channel;
                    }
                    catch (Exception)
                    {
                        CloseChannel(channel);
                        throw;
                    }
                }
                else
                {
                    CloseChannel(channel);
                    return await this.AcquireAsync();
                }
            }
            catch
            {
                CloseChannel(channel);
                return await this.AcquireAsync();
            }
        }

        /**
         * Bootstrap a new {@link Channel}. The default implementation uses {@link Bootstrap#connect()}, sub-classes may
         * override this.
         * <p>
         * The {@link Bootstrap} that is passed in here is cloned via {@link Bootstrap#clone()}, so it is safe to modify.
         */
        protected virtual Task<IChannel> ConnectChannel(Bootstrap bs) => bs.ConnectAsync();

        public virtual async ValueTask<bool> ReleaseAsync(IChannel channel)
        {
            Contract.Requires(channel != null);
            try
            {
                IEventLoop loop = channel.EventLoop;
                if (loop.InEventLoop)
                {
                    return await this.DoReleaseChannel(channel);
                }
                else
                {
                    var promise = new TaskCompletionSource<bool>();
                    loop.Execute(this.DoReleaseChannel, channel, promise);
                    return await promise.Task;
                }
            }
            catch (Exception)
            {
                CloseChannel(channel);
                throw;
            }
        }
        
        async void DoReleaseChannel(object channel, object state)
        {
            var promise = state as TaskCompletionSource<bool>;
            try
            {
                var result = await this.DoReleaseChannel((IChannel)channel);
                promise.TrySetResult(result);
            }
            catch (Exception ex)
            {
                promise.TrySetException(ex);
            }
        }

        async ValueTask<bool> DoReleaseChannel(IChannel channel)
        {
            Contract.Assert(channel.EventLoop.InEventLoop);

            // Remove the POOL_KEY attribute from the Channel and check if it was acquired from this pool, if not fail.
            if (channel.GetAttribute(PoolKey).GetAndSet(null) != this)
            {
                CloseChannel(channel);
                // Better include a stacktrace here as this is an user error.
                throw new ArgumentException($"Channel {channel} was not acquired from this ChannelPool");
            }
            else
            {
                try
                {
                    if (this.ReleaseHealthCheck)
                    {
                        return await this.DoHealthCheckOnRelease(channel);
                    }
                    else
                    {
                        this.ReleaseAndOffer(channel);
                        return true;
                    }
                }
                catch
                {
                    CloseChannel(channel);
                    throw;
                }
            }
        }

        /**
         * Adds the channel back to the pool only if the channel is healthy.
         * @param channel the channel to put back to the pool
         * @param promise offer operation promise.
         * @param future the future that contains information fif channel is healthy or not.
         * @throws Exception in case when failed to notify handler about release operation.
         */
        async ValueTask<bool> DoHealthCheckOnRelease(IChannel channel)
        {
            if (await this.HealthChecker.IsHealthyAsync(channel))
            {
                //channel turns out to be healthy, offering and releasing it.
                this.ReleaseAndOffer(channel);
                return true;
            }
            else
            {
                //channel not healthy, just releasing it.
                this.Handler.ChannelReleased(channel);
                return false;
            }
        }
        
        void ReleaseAndOffer(IChannel channel)
        {
            if (this.TryOfferChannel(channel))
            {
                this.Handler.ChannelReleased(channel);
            }
            else
            {
                CloseChannel(channel);
                throw FullException;
            }
        }
       
        static void CloseChannel(IChannel channel)
        {
            channel.GetAttribute(PoolKey).GetAndSet(null);
            channel.CloseAsync();
        }

        /**
         * Poll a {@link Channel} out of the internal storage to reuse it. This will return {@code null} if no
         * {@link Channel} is ready to be reused.
         *
         * Sub-classes may override {@link #pollChannel()} and {@link #offerChannel(Channel)}. Be aware that
         * implementations of these methods needs to be thread-safe!
         */
        protected virtual bool TryPollChannel(out IChannel channel) => this.store.TryDequeue(out channel);

        /**
         * Offer a {@link Channel} back to the internal storage. This will return {@code true} if the {@link Channel}
         * could be added, {@code false} otherwise.
         *
         * Sub-classes may override {@link #pollChannel()} and {@link #offerChannel(Channel)}. Be aware that
         * implementations of these methods needs to be thread-safe!
         */
        protected virtual bool TryOfferChannel(IChannel channel) => this.store.TryEnqueue(channel);

        public virtual void Dispose()
        {
            while (this.TryPollChannel(out IChannel channel))
            {
                channel.CloseAsync();
            }
        }
        
        class CompatibleConcurrentStack<T> : ConcurrentStack<T>, IQueue<T>
        {
            public bool TryEnqueue(T item)
            {
                this.Push(item);
                return true;
            }

            public bool TryDequeue(out T item) => this.TryPop(out item);
        }
    }
}