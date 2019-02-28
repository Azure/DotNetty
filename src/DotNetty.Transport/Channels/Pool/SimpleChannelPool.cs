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

    /// <summary>
    /// Simple <see cref="IChannelPool"/> implementation which will create new <see cref="IChannel"/>s if someone tries to acquire
    /// a <see cref="IChannel"/> but none is in the pool atm. No limit on the maximal concurrent <see cref="IChannel"/>s is enforced.
    /// This implementation uses LIFO order for <see cref="IChannel"/>s in the <see cref="IChannelPool"/>.
    /// </summary>
    public class SimpleChannelPool : IChannelPool
    {
        public static readonly AttributeKey<SimpleChannelPool> PoolKey = AttributeKey<SimpleChannelPool>.NewInstance("channelPool");

        static readonly InvalidOperationException FullException = new InvalidOperationException("ChannelPool full");

        readonly IQueue<IChannel> store;

        /// <summary>
        /// Creates a new <see cref="SimpleChannelPool"/> instance using the <see cref="ChannelActiveHealthChecker"/>.
        /// </summary>
        /// <param name="bootstrap">The <see cref="Bootstrapping.Bootstrap"/> that is used for connections.</param>
        /// <param name="handler">The <see cref="IChannelPoolHandler"/> that will be notified for the different pool actions.</param>
        public SimpleChannelPool(Bootstrap bootstrap, IChannelPoolHandler handler)
            : this(bootstrap, handler, ChannelActiveHealthChecker.Instance)
        {
        }

        /// <summary>
        /// Creates a new <see cref="SimpleChannelPool"/> instance.
        /// </summary>
        /// <param name="bootstrap">The <see cref="Bootstrapping.Bootstrap"/> that is used for connections.</param>
        /// <param name="handler">
        /// The <see cref="IChannelPoolHandler"/> that will be notified for the different pool actions.
        /// </param>
        /// <param name="healthChecker">
        /// The <see cref="IChannelHealthChecker"/> that will be used to check if a <see cref="IChannel"/> is still
        /// healthy when obtained from the <see cref="IChannelPool"/>.
        /// </param>
        public SimpleChannelPool(Bootstrap bootstrap, IChannelPoolHandler handler, IChannelHealthChecker healthChecker)
            : this(bootstrap, handler, healthChecker, true)
        {
        }

        /// <summary>
        /// Creates a new <see cref="SimpleChannelPool"/> instance.
        /// </summary>
        /// <param name="bootstrap">The <see cref="Bootstrapping.Bootstrap"/> that is used for connections.</param>
        /// <param name="handler">
        /// The <see cref="IChannelPoolHandler"/> that will be notified for the different pool actions.
        /// </param>
        /// <param name="healthChecker">
        /// The <see cref="IChannelHealthChecker"/> that will be used to check if a <see cref="IChannel"/> is still
        /// healthy when obtained from the <see cref="IChannelPool"/>.
        /// </param>
        /// <param name="releaseHealthCheck">
        /// If <c>true</c>, will check channel health before offering back. Otherwise, channel health is only checked
        /// at acquisition time.
        /// </param>
        public SimpleChannelPool(Bootstrap bootstrap, IChannelPoolHandler handler, IChannelHealthChecker healthChecker, bool releaseHealthCheck)
            : this(bootstrap, handler, healthChecker, releaseHealthCheck, true)
        {
        }

        /// <summary>
        /// Creates a new <see cref="SimpleChannelPool"/> instance.
        /// </summary>
        /// <param name="bootstrap">The <see cref="Bootstrapping.Bootstrap"/> that is used for connections.</param>
        /// <param name="handler">
        /// The <see cref="IChannelPoolHandler"/> that will be notified for the different pool actions.
        /// </param>
        /// <param name="healthChecker">
        /// The <see cref="IChannelHealthChecker"/> that will be used to check if a <see cref="IChannel"/> is still
        /// healthy when obtained from the <see cref="IChannelPool"/>.
        /// </param>
        /// <param name="releaseHealthCheck">
        /// If <c>true</c>, will check channel health before offering back. Otherwise, channel health is only checked
        /// at acquisition time.
        /// </param>
        /// <param name="lastRecentUsed">
        /// If <c>true</c>, <see cref="IChannel"/> selection will be LIFO. If <c>false</c>, it will be FIFO.
        /// </param>
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

        /// <summary>
        /// Returns the <see cref="Bootstrapping.Bootstrap"/> this pool will use to open new connections. 
        /// </summary>
        internal Bootstrap Bootstrap { get; }

        /// <summary>
        /// Returns the <see cref="IChannelPoolHandler"/> that will be notified for the different pool actions.
        /// </summary>
        internal IChannelPoolHandler Handler { get; }

        /// <summary>
        /// Returns the <see cref="IChannelHealthChecker"/> that will be used to check if an <see cref="IChannel"/> is healthy.
        /// </summary>
        internal IChannelHealthChecker HealthChecker { get; }

        /// <summary>
        /// Indicates whether this pool will check the health of channels before offering them back into the pool.
        /// Returns <c>true</c> if this pool will check the health of channels before offering them back into the pool, or
        /// <c>false</c> if channel health is only checked at acquisition time.
        /// </summary>
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
        /// <summary>
        /// Bootstrap a new <see cref="IChannel"/>. The default implementation uses
        /// <see cref="Bootstrapping.Bootstrap.ConnectAsync()"/>, sub-classes may override this.
        /// </summary>
        /// <param name="bs">
        /// The <see cref="Bootstrapping.Bootstrap"/> instance to use to bootstrap a new <see cref="IChannel"/>.
        /// The <see cref="Bootstrapping.Bootstrap"/> passed here is cloned via
        /// <see cref="Bootstrapping.Bootstrap.Clone()"/>, so it is safe to modify.
        /// </param>
        /// <returns>The newly connected <see cref="IChannel"/>.</returns>
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

        /// <summary>
        /// Releases the channel back to the pool only if the channel is healthy.
        /// </summary>
        /// <param name="channel">The <see cref="IChannel"/> to put back to the pool.</param>
        /// <returns>
        /// <c>true</c> if the <see cref="IChannel"/> was healthy, released, and offered back to the pool.
        /// <c>false</c> if the <see cref="IChannel"/> was NOT healthy and was simply released.
        /// </returns>
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

        /// <summary>
        /// Polls an <see cref="IChannel"/> out of the internal storage to reuse it.
        /// </summary>
        /// <remarks>
        /// Sub-classes may override <see cref="TryPollChannel"/> and <see cref="TryOfferChannel"/>.
        /// Be aware that implementations of these methods needs to be thread-safe!
        /// </remarks>
        /// <param name="channel">
        /// An output parameter that will contain the <see cref="IChannel"/> obtained from the pool.
        /// </param>
        /// <returns>
        /// <c>true</c> if an <see cref="IChannel"/> was retrieved from the pool, otherwise <c>false</c>.
        /// </returns>
        protected virtual bool TryPollChannel(out IChannel channel) => this.store.TryDequeue(out channel);

        /// <summary>
        /// Offers a <see cref="IChannel"/> back to the internal storage. This will return 
        /// </summary>
        /// <remarks>
        /// Sub-classes may override <see cref="TryPollChannel"/> and <see cref="TryOfferChannel"/>.
        /// Be aware that implementations of these methods needs to be thread-safe!
        /// </remarks>
        /// <param name="channel"></param>
        /// <returns><c>true</c> if the <see cref="IChannel"/> could be added, otherwise <c>false</c>.</returns>
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