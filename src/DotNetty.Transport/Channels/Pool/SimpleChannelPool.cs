// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace DotNetty.Transport.Channels.Pool
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
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
    static readonly AttributeKey<SimpleChannelPool> PoolKey = AttributeKey<SimpleChannelPool>.NewInstance("channelPool");

    static readonly InvalidOperationException FullException = new InvalidOperationException("ChannelPool full");

    readonly IQueue<IChannel> deque = PlatformDependent.NewMpscQueue<IChannel>();
    readonly bool lastRecentUsed;

    /**
     * Creates a new instance using the {@link IChannelHealthChecker#ACTIVE}.
     *
     * @param bootstrap         the {@link Bootstrap} that is used for connections
     * @param handler           the {@link IChannelPoolHandler} that will be notified for the different pool actions
     */
    public SimpleChannelPool(Bootstrap bootstrap, IChannelPoolHandler handler) 
        : this(bootstrap, handler, ChannelActiveHealthChecker.Instance)
    { }

    /**
     * Creates a new instance.
     *
     * @param bootstrap         the {@link Bootstrap} that is used for connections
     * @param handler           the {@link IChannelPoolHandler} that will be notified for the different pool actions
     * @param healthCheck       the {@link IChannelHealthChecker} that will be used to check if a {@link Channel} is
     *                          still healthy when obtain from the {@link ChannelPool}
     */
    public SimpleChannelPool(Bootstrap bootstrap, IChannelPoolHandler handler, IChannelHealthChecker healthCheck) 
        : this(bootstrap, handler, healthCheck, true)
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
    public SimpleChannelPool(Bootstrap bootstrap, IChannelPoolHandler handler, IChannelHealthChecker healthCheck, bool releaseHealthCheck) 
        : this(bootstrap, handler, healthCheck, releaseHealthCheck, true)
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
    public SimpleChannelPool(Bootstrap bootstrap, IChannelPoolHandler handler, IChannelHealthChecker healthCheck, bool releaseHealthCheck, bool lastRecentUsed) 
    {
        Contract.Requires(handler != null);
        Contract.Requires(healthCheck != null);
        Contract.Requires(bootstrap != null);
        
        this.Handler = handler;
        this.HealthCheck = healthCheck;
        this.ReleaseHealthCheck = releaseHealthCheck;
        
        // Clone the original Bootstrap as we want to set our own handler
        this.Bootstrap = bootstrap.Clone();
        this.Bootstrap.Handler(new ActionChannelInitializer<IChannel>(this.OnChannelInitializing));
        this.lastRecentUsed = lastRecentUsed;
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
    protected Bootstrap Bootstrap { get; }

    /**
     * Returns the {@link IChannelPoolHandler} that will be notified for the different pool actions.
     *
     * @return the {@link IChannelPoolHandler} that will be notified for the different pool actions
     */
    protected IChannelPoolHandler Handler { get; }

    /**
     * Returns the {@link IChannelHealthChecker} that will be used to check if a {@link Channel} is healthy.
     *
     * @return the {@link IChannelHealthChecker} that will be used to check if a {@link Channel} is healthy
     */
    protected IChannelHealthChecker HealthCheck { get; }

    /**
     * Indicates whether this pool will check the health of channels before offering them back into the pool.
     *
     * @return {@code true} if this pool will check the health of channels before offering them back into the pool, or
     * {@code false} if channel health is only checked at acquisition time
     */
    protected bool ReleaseHealthCheck { get; }

    public Task<IChannel> AquireAsync()
    {
        var promise = new TaskCompletionSource<IChannel>();
        this.AcquireHealthyFromPoolOrNew(promise);
        return promise.Task;
    }

    /**
     * Tries to retrieve healthy channel from the pool if any or creates a new channel otherwise.
     * @param promise the promise to provide acquire result.
     * @return future for acquiring a channel.
     */
    async void AcquireHealthyFromPoolOrNew(TaskCompletionSource<IChannel> promise)
    {
        try
        {
           var channel = this.PollChannel();

           if (channel == null) 
           {
                // No Channel left in the pool bootstrap a new Channel
                Bootstrap bs = this.Bootstrap.Clone();
                bs.Attribute(PoolKey, this);
               try
               {
                   channel = await this.ConnectChannel(bs);
                   if (!promise.TrySetResult(channel))
                   {
                       this.ReleaseAsync(channel);
                   }
               }
               catch (Exception e)
               {
                   promise.TrySetException(e);
               }
                
                return;
           }
            
            var loop = channel.EventLoop;
            if (loop.InEventLoop) 
            {
                this.DoHealthCheck(channel, promise);
            } 
            else 
            {
                loop.Execute(this.DoHealthCheck, channel, promise);
            }
        } 
        catch (Exception ex)
        {
            promise.TrySetException(ex);
        }
    }

    void DoHealthCheck(object channel, object promise) => this.DoHealthCheck((IChannel)channel, (TaskCompletionSource<IChannel>)promise);

    async void DoHealthCheck(IChannel channel, TaskCompletionSource<IChannel> promise) 
    {
        Contract.Assert(channel.EventLoop.InEventLoop);
        try
        {
            if (await this.HealthCheck.IsHealthyAsync(channel))
            {
                try 
                {
                    channel.GetAttribute(PoolKey).Set(this);
                    this.Handler.ChannelAquired(channel);
                    promise.TrySetResult(channel);
                } 
                catch (Exception ex)
                {
                    CloseAndFail(channel, ex, promise);
                }
            }
            else
            {
                CloseChannel(channel);
                this.AcquireHealthyFromPoolOrNew(promise);
            }
        }
        catch
        {
            CloseChannel(channel);
            this.AcquireHealthyFromPoolOrNew(promise);
        }
    }

    /**
     * Bootstrap a new {@link Channel}. The default implementation uses {@link Bootstrap#connect()}, sub-classes may
     * override this.
     * <p>
     * The {@link Bootstrap} that is passed in here is cloned via {@link Bootstrap#clone()}, so it is safe to modify.
     */
    protected virtual Task<IChannel> ConnectChannel(Bootstrap bs) => bs.ConnectAsync();

    public Task ReleaseAsync(IChannel channel) 
    {
        Contract.Requires(channel != null);
        var promise = new TaskCompletionSource();
        try 
        {
            var loop = channel.EventLoop;
            if (loop.InEventLoop) 
            {
                this.DoReleaseChannel(channel, promise);
            } 
            else 
            {
                loop.Execute(this.DoReleaseChannel, channel, promise);
            }
        } 
        catch (Exception ex) 
        {
            CloseAndFail(channel, ex, promise);
        }
        return promise.Task;
    }
    
    void DoReleaseChannel(object channel, object promise) => this.DoReleaseChannel((IChannel)channel, (TaskCompletionSource)promise);

    void DoReleaseChannel(IChannel channel, TaskCompletionSource promise) 
    {
        Contract.Assert(channel.EventLoop.InEventLoop);

        // Remove the POOL_KEY attribute from the Channel and check if it was acquired from this pool, if not fail.
        if (channel.GetAttribute(PoolKey).GetAndSet(null) != this)
        {
            // Better include a stacktrace here as this is an user error.
            CloseAndFail(channel, new ArgumentException($"Channel {channel} was not acquired from this ChannelPool"), promise);
        } 
        else 
        {
            try 
            {
                if (this.ReleaseHealthCheck) 
                {
                    this.DoHealthCheckOnRelease(channel, promise);
                } 
                else 
                {
                    this.ReleaseAndOffer(channel, promise);
                }
            } 
            catch (Exception cause) 
            {
                CloseAndFail(channel, cause, promise);
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
    async void DoHealthCheckOnRelease(IChannel channel, TaskCompletionSource promise) 
    {
        if (await this.HealthCheck.IsHealthyAsync(channel)) 
        { 
            //channel turns out to be healthy, offering and releasing it.
            this.ReleaseAndOffer(channel, promise);
        } 
        else 
        { 
            //channel not healthy, just releasing it.
            this.Handler.ChannelReleased(channel);
            promise.Complete();
        }
    }

    void ReleaseAndOffer(IChannel channel, TaskCompletionSource promise)
    {
        if (this.OfferChannel(channel))
        {
            this.Handler.ChannelReleased(channel);
            promise.Complete();
        } 
        else 
        {
            CloseAndFail(channel, FullException, promise);
        }
    }

    static void CloseChannel(IChannel channel) 
    {
        channel.GetAttribute(PoolKey).GetAndSet(null);
        channel.CloseAsync();
    }

    static void CloseAndFail(IChannel channel, Exception cause, TaskCompletionSource promise)
    {
        CloseChannel(channel);
        promise.TrySetException(cause);
    }
    
    static void CloseAndFail<T>(IChannel channel, Exception cause, TaskCompletionSource<T> promise)
    {
        CloseChannel(channel);
        promise.TrySetException(cause);
    }
    
    /**
     * Poll a {@link Channel} out of the internal storage to reuse it. This will return {@code null} if no
     * {@link Channel} is ready to be reused.
     *
     * Sub-classes may override {@link #pollChannel()} and {@link #offerChannel(Channel)}. Be aware that
     * implementations of these methods needs to be thread-safe!
     */
    protected IChannel PollChannel() 
        => this.lastRecentUsed ? this.deque.pollLast() : this.deque.pollFirst();

    /**
     * Offer a {@link Channel} back to the internal storage. This will return {@code true} if the {@link Channel}
     * could be added, {@code false} otherwise.
     *
     * Sub-classes may override {@link #pollChannel()} and {@link #offerChannel(Channel)}. Be aware that
     * implementations of these methods needs to be thread-safe!
     */
    protected bool OfferChannel(IChannel channel)
        => deque.offer(channel);
    

    public void Dispose() 
    {
        for (;;) 
        {
            IChannel channel = this.PollChannel();
            if (channel == null) 
            {
                break;
            }
            channel.CloseAsync();
        }
    }
}

}
            
            
            