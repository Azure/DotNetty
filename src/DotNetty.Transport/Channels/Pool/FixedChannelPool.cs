// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Pool
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Transport.Bootstrapping;

    /**
 * {@link ChannelPool} implementation that takes another {@link ChannelPool} implementation and enforce a maximum
 * number of concurrent connections.
 */
    public class FixedChannelPool : SimpleChannelPool
    {
        static readonly InvalidOperationException FullException = new InvalidOperationException("Too many outstanding acquire operations");

        static readonly TimeoutException TimeoutException = new TimeoutException("Acquire operation took longer then configured maximum time");

        static readonly InvalidOperationException PoolClosedOnReleaseException = new InvalidOperationException("FixedChannelPooled was closed");

        static readonly InvalidOperationException PoolClosedOnAcquireException = new InvalidOperationException("FixedChannelPooled was closed");

        public enum AcquireTimeoutAction
        {
            None,

            /**
             * Create a new connection when the timeout is detected.
             */
            New,

            /**
             * Fail the {@link Future} of the acquire call with a {@link TimeoutException}.
             */
            Fail
        }

        readonly IEventExecutor executor;
        readonly TimeSpan acquireTimeout;
        readonly IRunnable timeoutTask;

        // There is no need to worry about synchronization as everything that modified the queue or counts is done
        // by the above EventExecutor.
        readonly IQueue<AcquireTask> pendingAcquireQueue = PlatformDependent.NewMpscQueue<AcquireTask>();

        readonly int maxConnections;
        readonly int maxPendingAcquires;
        int acquiredChannelCount;
        int pendingAcquireCount;
        bool closed;

        /**
         * Creates a new instance using the {@link ChannelHealthChecker#ACTIVE}.
         *
         * @param bootstrap         the {@link Bootstrap} that is used for connections
         * @param handler           the {@link ChannelPoolHandler} that will be notified for the different pool actions
         * @param maxConnections    the number of maximal active connections, once this is reached new tries to acquire
         *                          a {@link Channel} will be delayed until a connection is returned to the pool again.
         */
        public FixedChannelPool(Bootstrap bootstrap, IChannelPoolHandler handler, int maxConnections)
            : this(bootstrap, handler, maxConnections, int.MaxValue)
        {
        }

        /**
         * Creates a new instance using the {@link ChannelHealthChecker#ACTIVE}.
         *
         * @param bootstrap             the {@link Bootstrap} that is used for connections
         * @param handler               the {@link ChannelPoolHandler} that will be notified for the different pool actions
         * @param maxConnections        the number of maximal active connections, once this is reached new tries to
         *                              acquire a {@link Channel} will be delayed until a connection is returned to the
         *                              pool again.
         * @param maxPendingAcquires    the maximum number of pending acquires. Once this is exceed acquire tries will
         *                              be failed.
         */
        public FixedChannelPool(Bootstrap bootstrap, IChannelPoolHandler handler, int maxConnections, int maxPendingAcquires)
            : this(bootstrap, handler, ChannelActiveHealthChecker.Instance, AcquireTimeoutAction.None, -1, maxConnections, maxPendingAcquires)
        {
        }

        /**
         * Creates a new instance.
         *
         * @param bootstrap             the {@link Bootstrap} that is used for connections
         * @param handler               the {@link ChannelPoolHandler} that will be notified for the different pool actions
         * @param healthCheck           the {@link ChannelHealthChecker} that will be used to check if a {@link Channel} is
         *                              still healthy when obtain from the {@link ChannelPool}
         * @param action                the {@link AcquireTimeoutAction} to use or {@code null} if non should be used.
         *                              In this case {@param acquireTimeoutMillis} must be {@code -1}.
         * @param acquireTimeoutMillis  the time (in milliseconds) after which an pending acquire must complete or
         *                              the {@link AcquireTimeoutAction} takes place.
         * @param maxConnections        the number of maximal active connections, once this is reached new tries to
         *                              acquire a {@link Channel} will be delayed until a connection is returned to the
         *                              pool again.
         * @param maxPendingAcquires    the maximum number of pending acquires. Once this is exceed acquire tries will
         *                              be failed.
         */
        public FixedChannelPool(Bootstrap bootstrap, IChannelPoolHandler handler, IChannelHealthChecker healthCheck, AcquireTimeoutAction action, long acquireTimeoutMillis, int maxConnections, int maxPendingAcquires)
            : this(bootstrap, handler, healthCheck, action, acquireTimeoutMillis, maxConnections, maxPendingAcquires, true)
        {
        }

        /**
         * Creates a new instance.
         *
         * @param bootstrap             the {@link Bootstrap} that is used for connections
         * @param handler               the {@link ChannelPoolHandler} that will be notified for the different pool actions
         * @param healthCheck           the {@link ChannelHealthChecker} that will be used to check if a {@link Channel} is
         *                              still healthy when obtain from the {@link ChannelPool}
         * @param action                the {@link AcquireTimeoutAction} to use or {@code null} if non should be used.
         *                              In this case {@param acquireTimeoutMillis} must be {@code -1}.
         * @param acquireTimeoutMillis  the time (in milliseconds) after which an pending acquire must complete or
         *                              the {@link AcquireTimeoutAction} takes place.
         * @param maxConnections        the number of maximal active connections, once this is reached new tries to
         *                              acquire a {@link Channel} will be delayed until a connection is returned to the
         *                              pool again.
         * @param maxPendingAcquires    the maximum number of pending acquires. Once this is exceed acquire tries will
         *                              be failed.
         * @param releaseHealthCheck    will check channel health before offering back if this parameter set to
         *                              {@code true}.
         */
        public FixedChannelPool(Bootstrap bootstrap, IChannelPoolHandler handler, IChannelHealthChecker healthCheck, AcquireTimeoutAction action, long acquireTimeoutMillis, int maxConnections, int maxPendingAcquires, bool releaseHealthCheck)
            : this(bootstrap, handler, healthCheck, action, acquireTimeoutMillis, maxConnections, maxPendingAcquires, releaseHealthCheck, true)
        {
        }

        /**
         * Creates a new instance.
         *
         * @param bootstrap             the {@link Bootstrap} that is used for connections
         * @param handler               the {@link ChannelPoolHandler} that will be notified for the different pool actions
         * @param healthCheck           the {@link ChannelHealthChecker} that will be used to check if a {@link Channel} is
         *                              still healthy when obtain from the {@link ChannelPool}
         * @param action                the {@link AcquireTimeoutAction} to use or {@code null} if non should be used.
         *                              In this case {@param acquireTimeoutMillis} must be {@code -1}.
         * @param acquireTimeoutMillis  the time (in milliseconds) after which an pending acquire must complete or
         *                              the {@link AcquireTimeoutAction} takes place.
         * @param maxConnections        the number of maximal active connections, once this is reached new tries to
         *                              acquire a {@link Channel} will be delayed until a connection is returned to the
         *                              pool again.
         * @param maxPendingAcquires    the maximum number of pending acquires. Once this is exceed acquire tries will
         *                              be failed.
         * @param releaseHealthCheck    will check channel health before offering back if this parameter set to
         *                              {@code true}.
         * @param lastRecentUsed        {@code true} {@link Channel} selection will be LIFO, if {@code false} FIFO.
         */
        public FixedChannelPool(Bootstrap bootstrap, IChannelPoolHandler handler, IChannelHealthChecker healthCheck, AcquireTimeoutAction action, long acquireTimeoutMillis, int maxConnections, int maxPendingAcquires, bool releaseHealthCheck, bool lastRecentUsed)
            : base(bootstrap, handler, healthCheck, releaseHealthCheck, lastRecentUsed)
        {
            if (maxConnections < 1)
            {
                throw new ArgumentException($"maxConnections: {maxConnections} (expected: >= 1)");
            }
            if (maxPendingAcquires < 1)
            {
                throw new ArgumentException($"maxPendingAcquires: {maxPendingAcquires} (expected: >= 1)");
            }

            if (action == AcquireTimeoutAction.None && acquireTimeoutMillis == -1)
            {
                this.timeoutTask = null;
                this.acquireTimeout = TimeSpan.Zero;
            }
            else if (action == AcquireTimeoutAction.None && acquireTimeoutMillis != -1)
            {
                throw new ArgumentException("action");
            }
            else if (action != AcquireTimeoutAction.None && acquireTimeoutMillis < 0)
            {
                throw new ArgumentException($"acquireTimeoutMillis: {acquireTimeoutMillis} (expected: >= 1)");
            }
            else
            {
                this.acquireTimeout = TimeSpan.FromMilliseconds(acquireTimeoutMillis);
                switch (action)
                {
                    case AcquireTimeoutAction.Fail:
                        this.timeoutTask = new NewTimeoutTask(this);
                        break;
                    case AcquireTimeoutAction.New:
                        this.timeoutTask = new FailTimeoutTask(this);
                        break;
                    default:
                        throw new ArgumentException("action");
                }
            }

            this.executor = bootstrap.Group().GetNext();
            this.maxConnections = maxConnections;
            this.maxPendingAcquires = maxPendingAcquires;
        }

        public override Task<IChannel> AcquireAsync()
        {
            var promise = new TaskCompletionSource<IChannel>();
            try
            {
                if (this.executor.InEventLoop)
                {
                    this.Acquire0(promise);
                }
                else
                {
                    this.executor.Execute(this.Acquire0, promise);
                }
            }
            catch (Exception cause)
            {
                promise.TrySetException(cause);
            }
            return promise.Task;
        }

        void Acquire0(object promise) => this.Acquire0((TaskCompletionSource<IChannel>)promise);

        void Acquire0(TaskCompletionSource<IChannel> promise)
        {
            Contract.Assert(this.executor.InEventLoop);

            if (this.closed)
            {
                promise.TrySetException(PoolClosedOnAcquireException);
                return;
            }

            if (this.acquiredChannelCount < this.maxConnections)
            {
                Contract.Assert(this.acquiredChannelCount >= 0);

                var l = new AcquireListener(this, promise);
                l.Acquired();

                // We need to create a new promise as we need to ensure the AcquireListener runs in the correct
                // EventLoop
                var p = new TaskCompletionSource<IChannel>();
                p.Task.ContinueWith(l.Completed);
                this.Acquire(p);
            }
            else
            {
                if (this.pendingAcquireCount >= this.maxPendingAcquires)
                {
                    promise.TrySetException(FullException);
                }
                else
                {
                    var task = new AcquireTask(this, promise);
                    if (this.pendingAcquireQueue.TryEnqueue(task))
                    {
                        ++this.pendingAcquireCount;

                        if (this.timeoutTask != null)
                        {
                            task.TimeoutTask = this.executor.Schedule(this.timeoutTask, this.acquireTimeout);
                        }
                    }
                    else
                    {
                        promise.TrySetException(FullException);
                    }
                }

                Contract.Assert(this.pendingAcquireCount > 0);
            }
        }

        public override async Task ReleaseAsync(IChannel channel)
        {
            try
            {
                await base.ReleaseAsync(channel);

                Contract.Assert(this.executor.InEventLoop);

                if (this.closed)
                {
                    // Since the pool is closed, we have no choice but to close the channel
                    channel.CloseAsync();
                    throw PoolClosedOnReleaseException;
                }

                this.DecrementAndRunTaskQueue();
            }
            catch (Exception e)
            {
                if (this.closed)
                {
                    // Since the pool is closed, we have no choice but to close the channel
                    channel.CloseAsync();
                    throw PoolClosedOnReleaseException;
                }

                if (!(e is ArgumentException))
                {
                    this.DecrementAndRunTaskQueue();
                }

                throw;
            }
        }

        void DecrementAndRunTaskQueue()
        {
            --this.acquiredChannelCount;

            // We should never have a negative value.
            Contract.Assert(this.acquiredChannelCount >= 0);

            // Run the pending acquire tasks before notify the original promise so if the user would
            // try to acquire again from the ChannelFutureListener and the pendingAcquireCount is >=
            // maxPendingAcquires we may be able to run some pending tasks first and so allow to add
            // more.
            this.RunTaskQueue();
        }

        void RunTaskQueue()
        {
            while (this.acquiredChannelCount < this.maxConnections)
            {
                if (!this.pendingAcquireQueue.TryDequeue(out AcquireTask task))
                {
                    break;
                }

                // Cancel the timeout if one was scheduled
                task.TimeoutTask?.Cancel();

                --this.pendingAcquireCount;
                task.Acquired();

                this.Acquire(task.Promise);
            }

            // We should never have a negative value.
            Contract.Assert(this.pendingAcquireCount >= 0);
            Contract.Assert(this.acquiredChannelCount >= 0);
        }

        public override void Dispose()
            =>
            this.executor.Execute(
                () =>
                {
                    if (!this.closed)
                    {
                        this.closed = true;
                        for (;;)
                        {
                            if (!this.pendingAcquireQueue.TryDequeue(out AcquireTask task))
                            {
                                break;
                            }

                            task.TimeoutTask?.Cancel();

                            task.Promise.TrySetException(new ClosedChannelException());
                        }
                        this.acquiredChannelCount = 0;
                        this.pendingAcquireCount = 0;
                        base.Dispose();
                    }
                });
        

        abstract class TimeoutTask : IRunnable
        {
            protected readonly FixedChannelPool Pool;

            protected TimeoutTask(FixedChannelPool pool)
            {
                this.Pool = pool;
            }

            public void Run()
            {
                Contract.Assert(this.Pool.executor.InEventLoop);
                for (;;)
                {
                    if (!this.Pool.pendingAcquireQueue.TryPeek(out AcquireTask task) || PreciseTimeSpan.FromTicks(Stopwatch.GetTimestamp()) < task.ExpireTime)
                    {
                        break;
                    }

                    this.Pool.pendingAcquireQueue.TryDequeue(out _);

                    --this.Pool.pendingAcquireCount;
                    this.OnTimeout(task);
                }
            }

            protected abstract void OnTimeout(AcquireTask task);
        }

        class NewTimeoutTask : TimeoutTask
        {
            public NewTimeoutTask(FixedChannelPool pool)
                : base(pool)
            {
            }

            protected override void OnTimeout(AcquireTask task)
            {
                // Increment the acquire count and delegate to super to actually acquire a Channel which will
                // create a new connection.
                task.Acquired();

                this.Pool.Acquire(task.Promise);
            }
        }

        class FailTimeoutTask : TimeoutTask
        {
            public FailTimeoutTask(FixedChannelPool pool)
                : base(pool)
            {
            }

            protected override void OnTimeout(AcquireTask task)
            {
                task.Promise.TrySetException(TimeoutException);
            }
        }

        // AcquireTask : AcquireListener to reduce object creations and so GC pressure
        class AcquireTask : AcquireListener
        {
            public readonly PreciseTimeSpan ExpireTime;
            public readonly TaskCompletionSource<IChannel> Promise;
            public IScheduledTask TimeoutTask;

            public AcquireTask(FixedChannelPool pool, TaskCompletionSource<IChannel> promise)
                : base(pool, promise)
            {
                // We need to create a new promise as we need to ensure the AcquireListener runs in the correct
                // EventLoop.
                this.Promise = new TaskCompletionSource<IChannel>();
                this.Promise.Task.ContinueWith(this.Completed);
                this.ExpireTime = PreciseTimeSpan.FromTicks(Stopwatch.GetTimestamp()) + pool.acquireTimeout;
            }
        }

        class AcquireListener
        {
            readonly FixedChannelPool pool;
            readonly TaskCompletionSource<IChannel> originalPromise;
            bool acquired;

            public AcquireListener(FixedChannelPool pool, TaskCompletionSource<IChannel> originalPromise)
            {
                this.pool = pool;
                this.originalPromise = originalPromise;
            }

            public void Completed(Task<IChannel> future)
            {
                Contract.Assert(this.pool.executor.InEventLoop);

                if (this.pool.closed)
                {
                    if (future.Status == TaskStatus.RanToCompletion)
                    {
                        // Since the pool is closed, we have no choice but to close the channel
                        future.Result.CloseAsync();
                    }
                    this.originalPromise.TrySetException(PoolClosedOnAcquireException);
                    return;
                }

                if (future.Status == TaskStatus.RanToCompletion)
                {
                    this.originalPromise.TrySetResult(future.Result);
                }
                else
                {
                    if (this.acquired)
                    {
                        this.pool.DecrementAndRunTaskQueue();
                    }
                    else
                    {
                        this.pool.RunTaskQueue();
                    }

                    this.originalPromise.TrySetException(future.Exception);
                }
            }

            public void Acquired()
            {
                if (this.acquired)
                {
                    return;
                }
                this.pool.acquiredChannelCount++;
                this.acquired = true;
            }
        }
    }
}