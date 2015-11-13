// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    public abstract class AbstractChannel : /*DefaultAttributeMap, */ IChannel
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<AbstractChannel>();

        protected static readonly ClosedChannelException ClosedChannelException = new ClosedChannelException();
        static readonly NotYetConnectedException NotYetConnectedException = new NotYetConnectedException();

        IMessageSizeEstimatorHandle estimatorHandle;

        readonly IChannelUnsafe channelUnsafe;
        readonly DefaultChannelPipeline pipeline;
        readonly TaskCompletionSource closeFuture = new TaskCompletionSource();

        volatile EndPoint localAddress;
        volatile EndPoint remoteAddress;
        volatile PausableChannelEventLoop eventLoop;
        volatile bool registered;

        /// <summary> Cache for the string representation of this channel /// </summary>
        bool strValActive;

        string strVal;

        /// <summary>
        /// Creates a new instance.
        ///
        /// @param parent
        ///        the parent of this channel. {@code null} if there's no parent.
        /// </summary>
        protected AbstractChannel(IChannel parent)
            : this(parent, DefaultChannelId.NewInstance())
        {
        }
        /// <summary>
        /// Creates a new instance.
        ///
        //* @param parent
        //*        the parent of this channel. {@code null} if there's no parent.
        /// </summary>
        protected AbstractChannel(IChannel parent , IChannelId id)
        {
            this.Parent = parent;
            this.Id = id;
            this.channelUnsafe = this.NewUnsafe();
            this.pipeline = new DefaultChannelPipeline(this);
        }

        public IChannelId Id { get; private set; }

        public bool IsWritable
        {
            get
            {
                ChannelOutboundBuffer buf = this.channelUnsafe.OutboundBuffer;
                return buf != null && buf.IsWritable;
            }
        }

        public IChannel Parent { get; private set; }

        public IChannelPipeline Pipeline
        {
            get { return this.pipeline; }
        }

        public abstract IChannelConfiguration Configuration { get; }

        public IByteBufferAllocator Allocator
        {
            get { return this.Configuration.Allocator; }
        }

        public IEventLoop EventLoop
        {
            get
            {
                IEventLoop eventLoop = this.eventLoop;
                if (eventLoop == null)
                {
                    throw new InvalidOperationException("channel not registered to an event loop");
                }
                return eventLoop;
            }
        }

        public abstract bool Open { get; }

        public abstract bool Active { get; }

        public abstract bool DisconnectSupported { get; }

        public EndPoint LocalAddress
        {
            get
            {
                EndPoint address = this.localAddress;
                return address ?? this.CacheLocalAddress();
            }
        }

        public EndPoint RemoteAddress
        {
            get
            {
                EndPoint address = this.remoteAddress;
                return address ?? this.CacheRemoteAddress();
            }
        }

        protected abstract EndPoint LocalAddressInternal { get; }

        protected void InvalidateLocalAddress()
        {
            this.localAddress = null;
        }

        protected EndPoint CacheLocalAddress()
        {
            try
            {
                return this.localAddress = this.LocalAddressInternal;
            }
            catch (Exception)
            {
                // Sometimes fails on a closed socket in Windows.
                return null;
            }
        }

        protected abstract EndPoint RemoteAddressInternal { get; }

        protected void InvalidateRemoteAddress()
        {
            this.remoteAddress = null;
        }

        protected EndPoint CacheRemoteAddress()
        {
            try
            {
                return this.remoteAddress = this.RemoteAddressInternal;
            }
            catch (Exception)
            {
                // Sometimes fails on a closed socket in Windows.
                return null;
            }
        }

        /// <summary>
        /// Reset the stored remoteAddress
        /// </summary>
        public bool Registered
        {
            get { return this.registered; }
        }

        public virtual Task BindAsync(EndPoint localAddress)
        {
            return this.pipeline.BindAsync(localAddress);
        }

        public virtual Task ConnectAsync(EndPoint remoteAddress)
        {
            return this.pipeline.ConnectAsync(remoteAddress);
        }

        public virtual Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
        {
            return this.pipeline.ConnectAsync(remoteAddress, localAddress);
        }

        public virtual Task DisconnectAsync()
        {
            return this.pipeline.DisconnectAsync();
        }

        public virtual Task CloseAsync()
        {
            return this.pipeline.CloseAsync();
        }

        public Task DeregisterAsync()
        {
            /// <summary>
            /// One problem of channel deregistration is that after a channel has been deregistered
            /// there may still be tasks, created from within one of the channel's ChannelHandlers,
            /// input the {@link EventLoop}'s task queue. That way, an unfortunate twist of events could lead
            /// to tasks still being input the old {@link EventLoop}'s queue even after the channel has been
            /// registered with a new {@link EventLoop}. This would lead to the tasks being executed by two
            /// different {@link EventLoop}s.
            ///
            /// Our solution to this problem is to always perform the actual deregistration of
            /// the channel as a task and to reject any submission of new tasks, from within
            /// one of the channel's ChannelHandlers, until the channel is registered with
            /// another {@link EventLoop}. That way we can be sure that there are no more tasks regarding
            /// that particular channel after it has been deregistered (because the deregistration
            /// task is the last one.).
            ///
            /// This only works for one time tasks. To see how we handle periodic/delayed tasks have a look
            /// at {@link io.netty.util.concurrent.ScheduledFutureTask#run()}.
            ///
            /// Also see {@link HeadContext#deregister(ChannelHandlerContext, ChannelPromise)}.
            /// </summary>
            this.eventLoop.RejectNewTasks();
            return this.pipeline.DeregisterAsync();
        }

        public IChannel Flush()
        {
            this.pipeline.Flush();
            return this;
        }

        public IChannel Read()
        {
            this.pipeline.Read();
            return this;
        }

        public Task WriteAsync(object msg)
        {
            return this.pipeline.WriteAsync(msg);
        }

        public Task WriteAndFlushAsync(object message)
        {
            return this.pipeline.WriteAndFlushAsync(message);
        }

        public Task CloseCompletion
        {
            get { return this.closeFuture.Task; }
        }

        public IChannelUnsafe Unsafe
        {
            get { return this.channelUnsafe; }
        }

        /// <summary>
        /// Create a new <see cref="AbstractUnsafe"/> instance which will be used for the life-time of the <see cref="IChannel"/>
        /// </summary>
        protected abstract IChannelUnsafe NewUnsafe();

           /// <summary>
        /// Returns the ID of this channel.
        /// </summary>

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        /// <summary>
        /// Returns {@code true} if and only if the specified object is identical
        /// with this channel (i.e: {@code this == o}).
        /// </summary>
        public override bool Equals(object o)
        {
            return this == o;
        }

        public int CompareTo(IChannel o)
        {
            if (ReferenceEquals(this, o))
            {
                return 0;
            }

            return Id.CompareTo(o.Id);
        }

        /// <summary>
        /// Returns the {@link String} representation of this channel.  The returned
        /// string contains the {@linkplain #hashCode()} ID}, {@linkplain #localAddress() local address},
        /// and {@linkplain #remoteAddress() remote address} of this channel for
        /// easier identification.
        /// </summary>
        public override string ToString()
        {
            bool active = this.Active;
            if (this.strValActive == active && this.strVal != null)
            {
                return this.strVal;
            }

            EndPoint remoteAddr = this.RemoteAddress;
            EndPoint localAddr = this.LocalAddress;
            if (remoteAddr != null)
            {
                EndPoint srcAddr;
                EndPoint dstAddr;
                if (this.Parent == null)
                {
                    srcAddr = localAddr;
                    dstAddr = remoteAddr;
                }
                else
                {
                    srcAddr = remoteAddr;
                    dstAddr = localAddr;
                }

                StringBuilder buf = new StringBuilder(96)
                    .Append("[id: 0x")
                    .Append(Id.AsShortText())
                    .Append(", ")
                    .Append(srcAddr)
                    .Append(active ? " => " : " :> ")
                    .Append(dstAddr)
                    .Append(']');
                this.strVal = buf.ToString();
            }
            else if (localAddr != null)
            {
                StringBuilder buf = new StringBuilder(64)
                    .Append("[id: 0x")
                    .Append(Id.AsShortText())
                    .Append(", ")
                    .Append(localAddr)
                    .Append(']');
                this.strVal = buf.ToString();
            }
            else
            {
                StringBuilder buf = new StringBuilder(16)
                    .Append("[id: 0x")
                    .Append(Id.AsShortText())
                    .Append(']');
                this.strVal = buf.ToString();
            }

            this.strValActive = active;
            return this.strVal;
        }

        internal IMessageSizeEstimatorHandle EstimatorHandle
        {
            get
            {
                if (this.estimatorHandle == null)
                {
                    this.estimatorHandle = this.Configuration.MessageSizeEstimator.NewHandle();
                }
                return this.estimatorHandle;
            }
        }

        /// <summary>
        /// <see cref="IChannelUnsafe"/> implementation which sub-classes must extend and use.
        /// </summary>
        protected abstract class AbstractUnsafe : IChannelUnsafe
        {
            protected readonly AbstractChannel channel;
            ChannelOutboundBuffer outboundBuffer;
            IRecvByteBufAllocatorHandle recvHandle;
            bool inFlush0;

            /// <summary> true if the channel has never been registered, false otherwise /// </summary>
            bool neverRegistered = true;

            public IRecvByteBufAllocatorHandle RecvBufAllocHandle
            {
                get
                {
                    if (this.recvHandle == null)
                    {
                        this.recvHandle = this.channel.Configuration.RecvByteBufAllocator.NewHandle();
                    }
                    return this.recvHandle;
                }
            }

            //public ChannelHandlerInvoker invoker() {
            //    // return the unwrapped invoker.
            //    return ((PausableChannelEventExecutor) eventLoop().asInvoker()).unwrapInvoker();
            //}

            protected AbstractUnsafe(AbstractChannel channel)
            {
                this.channel = channel;
                this.outboundBuffer = new ChannelOutboundBuffer(channel);
            }

            public ChannelOutboundBuffer OutboundBuffer
            {
                get { return this.outboundBuffer; }
            }

            public Task RegisterAsync(IEventLoop eventLoop)
            {
                Contract.Requires(eventLoop != null);
                if (this.channel.Registered)
                {
                    return TaskEx.FromException(new InvalidOperationException("registered to an event loop already"));
                }
                if (!this.channel.IsCompatible(eventLoop))
                {
                    return TaskEx.FromException(new InvalidOperationException("incompatible event loop type: " + eventLoop.GetType().Name));
                }

                // It's necessary to reuse the wrapped eventloop object. Otherwise the user will end up with multiple
                // objects that do not share a common state.
                if (this.channel.eventLoop == null)
                {
                    this.channel.eventLoop = new PausableChannelEventLoop(this.channel, eventLoop);
                }
                else
                {
                    this.channel.eventLoop.Unwrapped = eventLoop;
                }

                var promise = new TaskCompletionSource();

                if (eventLoop.InEventLoop)
                {
                    this.Register0(promise);
                }
                else
                {
                    try
                    {
                        eventLoop.Execute(() => this.Register0(promise));
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(
                            string.Format("Force-closing a channel whose registration task was not accepted by an event loop: {0}", this.channel),
                            ex);
                        this.CloseForcibly();
                        this.channel.closeFuture.Complete();
                        Util.SafeSetFailure(promise, ex, Logger);
                    }
                }

                return promise.Task;
            }

            void Register0(TaskCompletionSource promise)
            {
                try
                {
                    // check if the channel is still open as it could be closed input the mean time when the register
                    // call was outside of the eventLoop
                    if (!promise.setUncancellable() || !this.EnsureOpen(promise))
                    {
                        Util.SafeSetFailure(promise, ClosedChannelException, Logger);
                        return;
                    }
                    bool firstRegistration = this.neverRegistered;
                    this.channel.DoRegister();
                    this.neverRegistered = false;
                    this.channel.registered = true;
                    this.channel.eventLoop.AcceptNewTasks();
                    Util.SafeSetSuccess(promise, Logger);
                    this.channel.pipeline.FireChannelRegistered();
                    // Only fire a channelActive if the channel has never been registered. This prevents firing
                    // multiple channel actives if the channel is deregistered and re-registered.
                    if (firstRegistration && this.channel.Active)
                    {
                        this.channel.pipeline.FireChannelActive();
                    }
                }
                catch (Exception t)
                {
                    // Close the channel directly to avoid FD leak.
                    this.CloseForcibly();
                    this.channel.closeFuture.Complete();
                    Util.SafeSetFailure(promise, t, Logger);
                }
            }

            public Task BindAsync(EndPoint localAddress)
            {
                // todo: cancellation support
                if ( /*!promise.setUncancellable() || */!this.channel.Open)
                {
                    return this.CreateClosedChannelExceptionTask();
                }

                //// See: https://github.com/netty/netty/issues/576
                //if (bool.TrueString.Equals(this.channel.Configuration.getOption(ChannelOption.SO_BROADCAST)) &&
                //    localAddress is IPEndPoint &&
                //    !((IPEndPoint)localAddress).Address.getAddress().isAnyLocalAddress() &&
                //    !Environment.OSVersion.Platform == PlatformID.Win32NT && !Environment.isRoot())
                //{
                //    // Warn a user about the fact that a non-root user can't receive a
                //    // broadcast packet on *nix if the socket is bound on non-wildcard address.
                //    logger.Warn(
                //        "A non-root user can't receive a broadcast packet if the socket " +
                //            "is not bound to a wildcard address; binding to a non-wildcard " +
                //            "address (" + localAddress + ") anyway as requested.");
                //}

                bool wasActive = this.channel.Active;
                var promise = new TaskCompletionSource();
                try
                {
                    this.channel.DoBind(localAddress);
                }
                catch (Exception t)
                {
                    Util.SafeSetFailure(promise, t, Logger);
                    this.CloseIfClosed();
                    return promise.Task;
                }

                if (!wasActive && this.channel.Active)
                {
                    this.InvokeLater(() => this.channel.pipeline.FireChannelActive());
                }

                this.SafeSetSuccess(promise);

                return promise.Task;
            }

            public abstract Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress);

            void SafeSetFailure(TaskCompletionSource promise, Exception cause)
            {
                Util.SafeSetFailure(promise, cause, Logger);
            }

            public Task DisconnectAsync()
            {
                var promise = new TaskCompletionSource();
                if (!promise.setUncancellable())
                {
                    return promise.Task;
                }

                bool wasActive = this.channel.Active;
                try
                {
                    this.channel.DoDisconnect();
                }
                catch (Exception t)
                {
                    this.SafeSetFailure(promise, t);
                    this.CloseIfClosed();
                    return promise.Task;
                }

                if (wasActive && !this.channel.Active)
                {
                    this.InvokeLater(() => this.channel.pipeline.FireChannelInactive());
                }

                this.SafeSetSuccess(promise);
                this.CloseIfClosed(); // doDisconnect() might have closed the channel

                return promise.Task;
            }

            void SafeSetSuccess(TaskCompletionSource promise)
            {
                Util.SafeSetSuccess(promise, Logger);
            }

            public Task CloseAsync() //CancellationToken cancellationToken)
            {
                var promise = new TaskCompletionSource();
                if (!promise.setUncancellable())
                {
                    return promise.Task;
                }
                //if (cancellationToken.IsCancellationRequested)
                //{
                //    return TaskEx.Cancelled;
                //}

                if (this.outboundBuffer == null)
                {
                    // Only needed if no VoidChannelPromise.
                    if (promise != TaskCompletionSource.Void)
                    {
                        // This means close() was called before so we just register a listener and return
                        return this.channel.closeFuture.Task;
                    }
                    return promise.Task;
                }

                if (this.channel.closeFuture.Task.IsCompleted)
                {
                    // Closed already.
                    Util.SafeSetSuccess(promise, Logger);
                    return promise.Task;
                }

                bool wasActive = this.channel.Active;
                ChannelOutboundBuffer buffer = this.outboundBuffer;
                this.outboundBuffer = null; // Disallow adding any messages and flushes to outboundBuffer.
                IEventExecutor closeExecutor = null; // todo closeExecutor();
                if (closeExecutor != null)
                {
                    closeExecutor.Execute(() =>
                    {
                        try
                        {
                            // Execute the close.
                            this.DoClose0(promise);
                        }
                        finally
                        {
                            // Call invokeLater so closeAndDeregister is executed input the EventLoop again!
                            this.InvokeLater(() =>
                            {
                                // Fail all the queued messages
                                buffer.FailFlushed(ClosedChannelException,
                                    false);
                                buffer.Close(ClosedChannelException);
                                this.FireChannelInactiveAndDeregister(wasActive);
                            });
                        }
                    });
                }
                else
                {
                    try
                    {
                        // Close the channel and fail the queued messages input all cases.
                        this.DoClose0(promise);
                    }
                    finally
                    {
                        // Fail all the queued messages.
                        buffer.FailFlushed(ClosedChannelException, false);
                        buffer.Close(ClosedChannelException);
                    }
                    if (this.inFlush0)
                    {
                        this.InvokeLater(() => this.FireChannelInactiveAndDeregister(wasActive));
                    }
                    else
                    {
                        this.FireChannelInactiveAndDeregister(wasActive);
                    }
                }

                return promise.Task;
            }

            void DoClose0(TaskCompletionSource promise)
            {
                try
                {
                    this.channel.DoClose();
                    this.channel.closeFuture.Complete();
                    this.SafeSetSuccess(promise);
                }
                catch (Exception t)
                {
                    this.channel.closeFuture.Complete();
                    this.SafeSetFailure(promise, t);
                }
            }

            void FireChannelInactiveAndDeregister(bool wasActive)
            {
                if (wasActive && !this.channel.Active)
                {
                    this.InvokeLater(() =>
                    {
                        this.channel.pipeline.FireChannelInactive();
                        this.DeregisterAsync();
                    });
                }
                else
                {
                    this.InvokeLater(() => this.DeregisterAsync());
                }
            }

            public void CloseForcibly()
            {
                try
                {
                    this.channel.DoClose();
                }
                catch (Exception e)
                {
                    Logger.Warn("Failed to close a channel.", e);
                }
            }

            /// <summary>
            /// This method must NEVER be called directly, but be executed as an
            /// extra task with a clean call stack instead. The reason for this
            /// is that this method calls {@link ChannelPipeline#fireChannelUnregistered()}
            /// directly, which might lead to an unfortunate nesting of independent inbound/outbound
            /// events. See the comments input {@link #invokeLater(Runnable)} for more details.
            /// </summary>
            public Task DeregisterAsync()
            {
                //if (!promise.setUncancellable())
                //{
                //    return;
                //}

                if (!this.channel.registered)
                {
                    return TaskEx.Completed;
                }

                try
                {
                    this.channel.DoDeregister();
                }
                catch (Exception t)
                {
                    Logger.Warn("Unexpected exception occurred while deregistering a channel.", t);
                    return TaskEx.FromException(t);
                }
                finally
                {
                    if (this.channel.registered)
                    {
                        this.channel.registered = false;
                        this.channel.pipeline.FireChannelUnregistered();
                    }
                    else
                    {
                        // Some transports like local and AIO does not allow the deregistration of
                        // an open channel.  Their doDeregister() calls close().  Consequently,
                        // close() calls deregister() again - no need to fire channelUnregistered.
                    }
                }
                return TaskEx.Completed;
            }

            public void BeginRead()
            {
                if (!this.channel.Active)
                {
                    return;
                }

                try
                {
                    this.channel.DoBeginRead();
                }
                catch (Exception e)
                {
                    this.InvokeLater(() => this.channel.pipeline.FireExceptionCaught(e));
                    this.CloseAsync();
                }
            }

            public Task WriteAsync(object msg)
            {
                ChannelOutboundBuffer outboundBuffer = this.outboundBuffer;
                if (outboundBuffer == null)
                {
                    // If the outboundBuffer is null we know the channel was closed and so
                    // need to fail the future right away. If it is not null the handling of the rest
                    // will be done input flush0()
                    // See https://github.com/netty/netty/issues/2362

                    // release message now to prevent resource-leak
                    ReferenceCountUtil.Release(msg);
                    return TaskEx.FromException(ClosedChannelException);
                }

                int size;
                try
                {
                    msg = this.channel.FilterOutboundMessage(msg);
                    size = this.channel.EstimatorHandle.Size(msg);
                    if (size < 0)
                    {
                        size = 0;
                    }
                }
                catch (Exception t)
                {
                    ReferenceCountUtil.Release(msg);

                    return TaskEx.FromException(t);
                }

                var promise = new TaskCompletionSource();
                outboundBuffer.AddMessage(msg, size, promise);
                return promise.Task;
            }

            public void Flush()
            {
                ChannelOutboundBuffer outboundBuffer = this.outboundBuffer;
                if (outboundBuffer == null)
                {
                    return;
                }

                outboundBuffer.AddFlush();
                this.Flush0();
            }

            protected virtual void Flush0()
            {
                if (this.inFlush0)
                {
                    // Avoid re-entrance
                    return;
                }

                ChannelOutboundBuffer outboundBuffer = this.outboundBuffer;
                if (outboundBuffer == null || outboundBuffer.IsEmpty)
                {
                    return;
                }

                this.inFlush0 = true;

                // Mark all pending write requests as failure if the channel is inactive.
                if (!this.channel.Active)
                {
                    try
                    {
                        if (this.channel.Open)
                        {
                            outboundBuffer.FailFlushed(NotYetConnectedException, true);
                        }
                        else
                        {
                            // Do not trigger channelWritabilityChanged because the channel is closed already.
                            outboundBuffer.FailFlushed(ClosedChannelException, false);
                        }
                    }
                    finally
                    {
                        this.inFlush0 = false;
                    }
                    return;
                }

                try
                {
                    this.channel.DoWrite(outboundBuffer);
                }
                catch (Exception t)
                {
                    outboundBuffer.FailFlushed(t, true);
                }
                finally
                {
                    this.inFlush0 = false;
                }
            }

            protected bool EnsureOpen(TaskCompletionSource promise)
            {
                if (this.channel.Open)
                {
                    return true;
                }

                Util.SafeSetFailure(promise, ClosedChannelException, Logger);
                return false;
            }

            protected Task CreateClosedChannelExceptionTask()
            {
                return TaskEx.FromException(ClosedChannelException);
            }

            protected void CloseIfClosed()
            {
                if (this.channel.Open)
                {
                    return;
                }
                this.CloseAsync();
            }

            void InvokeLater(Action task)
            {
                try
                {
                    // This method is used by outbound operation implementations to trigger an inbound event later.
                    // They do not trigger an inbound event immediately because an outbound operation might have been
                    // triggered by another inbound event handler method.  If fired immediately, the call stack
                    // will look like this for example:
                    //
                    //   handlerA.inboundBufferUpdated() - (1) an inbound handler method closes a connection.
                    //   -> handlerA.ctx.close()
                    //      -> channel.unsafe.close()
                    //         -> handlerA.channelInactive() - (2) another inbound handler method called while input (1) yet
                    //
                    // which means the execution of two inbound handler methods of the same handler overlap undesirably.
                    this.channel.EventLoop.Execute(task);
                }
                catch (RejectedExecutionException e)
                {
                    Logger.Warn("Can't invoke task later as EventLoop rejected it", e);
                }
            }

            protected Exception AnnotateConnectException(Exception exception, EndPoint remoteAddress)
            {
                if (exception is SocketException)
                {
                    return new ConnectException("LogError connecting to " + remoteAddress, exception);
                }

                return exception;
            }

            //   /// <summary>
            //* @return {@link EventLoop} to execute {@link #doClose()} or {@code null} if it should be done input the
            //* {@link EventLoop}.
            //+
            ///// </summary>

            //   protected IEventExecutor closeExecutor()
            //   {
            //       return null;
            //   }
        }

        /// <summary>
        /// Return {@code true} if the given {@link EventLoop} is compatible with this instance.
        /// </summary>
        protected abstract bool IsCompatible(IEventLoop eventLoop);

        /// <summary>
        /// Is called after the {@link Channel} is registered with its {@link EventLoop} as part of the register process.
        ///
        /// Sub-classes may override this method
        /// </summary>
        protected virtual void DoRegister()
        {
            // NOOP
        }

        /// <summary>
        /// Bind the {@link Channel} to the {@link EndPoint}
        /// </summary>
        protected abstract void DoBind(EndPoint localAddress);

        /// <summary>
        /// Disconnect this {@link Channel} from its remote peer
        /// </summary>
        protected abstract void DoDisconnect();

        /// <summary>
        /// Close the {@link Channel}
        /// </summary>
        protected abstract void DoClose();

        /// <summary>
        /// Deregister the {@link Channel} from its {@link EventLoop}.
        ///
        /// Sub-classes may override this method
        /// </summary>
        protected virtual void DoDeregister()
        {
            // NOOP
        }

        /// <summary>
        /// ScheduleAsync a read operation.
        /// </summary>
        protected abstract void DoBeginRead();

        /// <summary>
        /// Flush the content of the given buffer to the remote peer.
        /// </summary>
        protected abstract void DoWrite(ChannelOutboundBuffer input);

        /// <summary>
        /// Invoked when a new message is added to a {@link ChannelOutboundBuffer} of this {@link AbstractChannel}, so that
        /// the {@link Channel} implementation converts the message to another. (e.g. heap buffer -> direct buffer)
        /// </summary>
        protected virtual object FilterOutboundMessage(object msg)
        {
            return msg;
        }

        sealed class PausableChannelEventLoop : PausableChannelEventExecutor, IEventLoop
        {
            volatile bool isAcceptingNewTasks = true;
            public volatile IEventLoop Unwrapped;
            readonly IChannel channel;

            public PausableChannelEventLoop(IChannel channel, IEventLoop unwrapped)
            {
                this.channel = channel;
                this.Unwrapped = unwrapped;
            }

            public override void RejectNewTasks()
            {
                this.isAcceptingNewTasks = false;
            }

            public override void AcceptNewTasks()
            {
                this.isAcceptingNewTasks = true;
            }

            public override bool IsAcceptingNewTasks
            {
                get { return this.isAcceptingNewTasks; }
            }

            public override IEventExecutor Unwrap()
            {
                return this.Unwrapped;
            }

            IEventLoop IEventLoop.Unwrap()
            {
                return this.Unwrapped;
            }

            public IChannelHandlerInvoker Invoker
            {
                get { return this.Unwrapped.Invoker; }
            }

            public Task RegisterAsync(IChannel c)
            {
                return this.Unwrapped.RegisterAsync(c);
            }

            internal override IChannel Channel
            {
                get { return this.channel; }
            }
        }
    }
}