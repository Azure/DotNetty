// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    public abstract class AbstractChannel : DefaultAttributeMap, IChannel
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<AbstractChannel>();

        static readonly NotYetConnectedException NotYetConnectedException = new NotYetConnectedException();

        readonly IChannelUnsafe channelUnsafe;

        readonly DefaultChannelPipeline pipeline;
        readonly IPromise closeFuture;

        volatile EndPoint localAddress;
        volatile EndPoint remoteAddress;
        volatile IEventLoop eventLoop;
        volatile bool registered;

        /// <summary>Cache for the string representation of this channel</summary>
        bool strValActive;

        string strVal;

        /// <summary>
        ///     Creates a new instance.
        /// </summary>
        /// <param name="parent">the parent of this channel. <c>null</c> if there's no parent.</param>
        protected AbstractChannel(IChannel parent)
        {
            this.Parent = parent;
            this.Id = this.NewId();
            this.channelUnsafe = this.NewUnsafe();
            this.pipeline = this.NewChannelPipeline();
            this.closeFuture = this.NewPromise();
        }

        /// <summary>
        ///     Creates a new instance.
        /// </summary>
        /// <param name="parent">the parent of this channel. <c>null</c> if there's no parent.</param>
        protected AbstractChannel(IChannel parent, IChannelId id)
        {
            this.Parent = parent;
            this.Id = id;
            this.channelUnsafe = this.NewUnsafe();
            this.pipeline = this.NewChannelPipeline();
            this.closeFuture = this.NewPromise();
        }

        public IChannelId Id { get; }

        public bool IsWritable
        {
            get
            {
                ChannelOutboundBuffer buf = this.channelUnsafe.OutboundBuffer;
                return buf != null && buf.IsWritable;
            }
        }

        public IChannel Parent { get; }

        public IChannelPipeline Pipeline => this.pipeline;

        public abstract IChannelConfiguration Configuration { get; }

        public IByteBufferAllocator Allocator => this.Configuration.Allocator;

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

        public abstract ChannelMetadata Metadata { get; }

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

        protected void InvalidateLocalAddress() => this.localAddress = null;

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

        protected void InvalidateRemoteAddress() => this.remoteAddress = null;

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
        ///     Reset the stored remoteAddress
        /// </summary>
        public bool Registered => this.registered;

        /// Returns a new <see cref="DefaultChannelId"/> instance. Subclasses may override this method to assign custom
        /// <see cref="IChannelId"/>s to <see cref="IChannel"/>s that use the <see cref="AbstractChannel"/> constructor.
        protected virtual IChannelId NewId() => DefaultChannelId.NewInstance();

        /// <summary>Returns a new pipeline instance.</summary>
        protected virtual DefaultChannelPipeline NewChannelPipeline() => new DefaultChannelPipeline(this);

        public virtual Task BindAsync(EndPoint localAddress) => this.pipeline.BindAsync(localAddress);

        public virtual Task ConnectAsync(EndPoint remoteAddress) => this.pipeline.ConnectAsync(remoteAddress);

        public virtual Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress) => this.pipeline.ConnectAsync(remoteAddress, localAddress);

        public virtual Task DisconnectAsync() => this.pipeline.DisconnectAsync();

        public virtual Task CloseAsync() => this.pipeline.CloseAsync();

        public Task DeregisterAsync() => this.pipeline.DeregisterAsync();

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

        public Task WriteAsync(object msg) => this.pipeline.WriteAsync(msg);

        public Task WriteAsync(object message, IPromise promise) => this.pipeline.WriteAsync(message, promise);

        public Task WriteAndFlushAsync(object message) => this.pipeline.WriteAndFlushAsync(message);
        
        public Task WriteAndFlushAsync(object message, IPromise promise) => this.pipeline.WriteAndFlushAsync(message, promise);

        public IPromise NewPromise() => new TaskCompletionSource();
        
        public IPromise NewPromise(object state) => new TaskCompletionSource(state);
        
        public IPromise VoidPromise() => DotNetty.Common.Concurrency.VoidPromise.Instance;

        public Task CloseCompletion => this.closeFuture.Task;

        public IChannelUnsafe Unsafe => this.channelUnsafe;

        /// <summary>
        ///     Create a new <see cref="AbstractUnsafe" /> instance which will be used for the life-time of the
        ///     <see cref="IChannel" />
        /// </summary>
        protected abstract IChannelUnsafe NewUnsafe();

        /// <summary>
        ///     Returns the ID of this channel.
        /// </summary>
        public override int GetHashCode() => this.Id.GetHashCode();

        /// <summary>
        ///     Returns <c>true</c> if and only if the specified object is identical
        ///     with this channel (i.e. <c>this == o</c>).
        /// </summary>
        public override bool Equals(object o) => this == o;

        public int CompareTo(IChannel o) => ReferenceEquals(this, o) ? 0 : this.Id.CompareTo(o.Id);

        /// <summary>
        ///     Returns the string representation of this channel.  The returned
        ///     string contains the {@linkplain #hashCode()} ID}, {@linkplain #localAddress() local address},
        ///     and {@linkplain #remoteAddress() remote address} of this channel for
        ///     easier identification.
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
                    .Append(this.Id.AsShortText())
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
                    .Append(this.Id.AsShortText())
                    .Append(", ")
                    .Append(localAddr)
                    .Append(']');
                this.strVal = buf.ToString();
            }
            else
            {
                StringBuilder buf = new StringBuilder(16)
                    .Append("[id: 0x")
                    .Append(this.Id.AsShortText())
                    .Append(']');
                this.strVal = buf.ToString();
            }

            this.strValActive = active;
            return this.strVal;
        }

        /// <summary>
        ///     <see cref="IChannelUnsafe" /> implementation which sub-classes must extend and use.
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
                => this.recvHandle ?? (this.recvHandle = this.channel.Configuration.RecvByteBufAllocator.NewHandle());

            //public ChannelHandlerInvoker invoker() {
            //    // return the unwrapped invoker.
            //    return ((PausableChannelEventExecutor) eventLoop().asInvoker()).unwrapInvoker();
            //}

            protected AbstractUnsafe(AbstractChannel channel)
            {
                this.channel = channel;
                this.outboundBuffer = new ChannelOutboundBuffer(channel);
            }

            public ChannelOutboundBuffer OutboundBuffer => this.outboundBuffer;

            void AssertEventLoop() => Contract.Assert(!this.channel.registered || this.channel.eventLoop.InEventLoop);

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

                this.channel.eventLoop = eventLoop;

                var promise = this.channel.NewPromise();

                if (eventLoop.InEventLoop)
                {
                    this.Register0(promise);
                }
                else
                {
                    try
                    {
                        eventLoop.Execute((u, p) => ((AbstractUnsafe)u).Register0((TaskCompletionSource)p), this, promise);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Force-closing a channel whose registration task was not accepted by an event loop: {}", this.channel, ex);
                        this.CloseForcibly();
                        this.channel.closeFuture.Complete();
                        Util.SafeSetFailure(promise, ex, Logger);
                    }
                }

                return promise.Task;
            }

            void Register0(IPromise promise)
            {
                try
                {
                    // check if the channel is still open as it could be closed input the mean time when the register
                    // call was outside of the eventLoop
                    if (!promise.SetUncancellable() || !this.EnsureOpen(promise))
                    {
                        Util.SafeSetFailure(promise, new ClosedChannelException(), Logger);
                        return;
                    }
                    bool firstRegistration = this.neverRegistered;
                    this.channel.DoRegister();
                    this.neverRegistered = false;
                    this.channel.registered = true;

                    Util.SafeSetSuccess(promise, Logger);
                    this.channel.pipeline.FireChannelRegistered();
                    // Only fire a channelActive if the channel has never been registered. This prevents firing
                    // multiple channel actives if the channel is deregistered and re-registered.
                    if (this.channel.Active)
                    {
                        if (firstRegistration)
                        {
                            this.channel.pipeline.FireChannelActive();
                        }
                        else if (this.channel.Configuration.AutoRead)
                        {
                            // This channel was registered before and autoRead() is set. This means we need to begin read
                            // again so that we process inbound data.
                            //
                            // See https://github.com/netty/netty/issues/4805
                            this.BeginRead();
                        }
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
                this.AssertEventLoop();

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
                try
                {
                    this.channel.DoBind(localAddress);
                }
                catch (Exception t)
                {
                    this.CloseIfClosed();
                    return TaskEx.FromException(t);
                }

                if (!wasActive && this.channel.Active)
                {
                    this.InvokeLater(() => this.channel.pipeline.FireChannelActive());
                }

                return TaskEx.Completed;
            }

            public abstract Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress);

            public Task DisconnectAsync()
            {
                this.AssertEventLoop();

                bool wasActive = this.channel.Active;
                try
                {
                    this.channel.DoDisconnect();
                }
                catch (Exception t)
                {
                    this.CloseIfClosed();
                    return TaskEx.FromException(t);
                }

                if (wasActive && !this.channel.Active)
                {
                    this.InvokeLater(() => this.channel.pipeline.FireChannelInactive());
                }

                this.CloseIfClosed(); // doDisconnect() might have closed the channel

                return TaskEx.Completed;
            }

            public Task CloseAsync() /*CancellationToken cancellationToken) */
            {
                this.AssertEventLoop();

                return this.CloseAsync(new ClosedChannelException(), false);
            }

            Task CloseAsync(Exception cause, bool notify)
            {
                var promise = this.channel.NewPromise();
                if (!promise.SetUncancellable())
                {
                    return promise.Task;
                }

                ChannelOutboundBuffer outboundBuffer = this.outboundBuffer;
                if (outboundBuffer == null)
                {
                    // Only needed if no VoidChannelPromise.
                    if (!promise.IsVoid)
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
                                outboundBuffer.FailFlushed(cause, notify);
                                outboundBuffer.Close(new ClosedChannelException());
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
                        outboundBuffer.FailFlushed(cause, notify);
                        outboundBuffer.Close(new ClosedChannelException());
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

            void DoClose0(IPromise promise)
            {
                try
                {
                    this.channel.DoClose();
                    this.channel.closeFuture.Complete();
                    Util.SafeSetSuccess(promise, Logger);
                }
                catch (Exception t)
                {
                    this.channel.closeFuture.Complete();
                    Util.SafeSetFailure(promise, t, Logger);
                }
            }

            void FireChannelInactiveAndDeregister(bool wasActive) => this.DeregisterAsync(wasActive && !this.channel.Active);

            public void CloseForcibly()
            {
                this.AssertEventLoop();

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
            ///     This method must NEVER be called directly, but be executed as an
            ///     extra task with a clean call stack instead. The reason for this
            ///     is that this method calls {@link ChannelPipeline#fireChannelUnregistered()}
            ///     directly, which might lead to an unfortunate nesting of independent inbound/outbound
            ///     events. See the comments input {@link #invokeLater(Runnable)} for more details.
            /// </summary>
            public Task DeregisterAsync()
            {
                this.AssertEventLoop();

                return this.DeregisterAsync(false);
            }

            Task DeregisterAsync(bool fireChannelInactive)
            {
                //if (!promise.setUncancellable())
                //{
                //    return;
                //}

                if (!this.channel.registered)
                {
                    return TaskEx.Completed;
                }

                var promise = this.channel.NewPromise();

                // As a user may call deregister() from within any method while doing processing in the ChannelPipeline,
                // we need to ensure we do the actual deregister operation later. This is needed as for example,
                // we may be in the ByteToMessageDecoder.callDecode(...) method and so still try to do processing in
                // the old EventLoop while the user already registered the Channel to a new EventLoop. Without delay,
                // the deregister operation this could lead to have a handler invoked by different EventLoop and so
                // threads.
                //
                // See:
                // https://github.com/netty/netty/issues/4435
                this.InvokeLater(() =>
                {
                    try
                    {
                        this.channel.DoDeregister();
                    }
                    catch (Exception t)
                    {
                        Logger.Warn("Unexpected exception occurred while deregistering a channel.", t);
                    }
                    finally
                    {
                        if (fireChannelInactive)
                        {
                            this.channel.pipeline.FireChannelInactive();
                        }
                        // Some transports like local and AIO does not allow the deregistration of
                        // an open channel.  Their doDeregister() calls close(). Consequently,
                        // close() calls deregister() again - no need to fire channelUnregistered, so check
                        // if it was registered.
                        if (this.channel.registered)
                        {
                            this.channel.registered = false;
                            this.channel.pipeline.FireChannelUnregistered();
                        }
                        Util.SafeSetSuccess(promise, Logger);
                    }
                });

                return promise.Task;
            }

            public void BeginRead()
            {
                this.AssertEventLoop();

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
                    this.CloseSafe();
                }
            }
            
            public void Write(object msg, IPromise promise)
            {
                this.AssertEventLoop();

                ChannelOutboundBuffer outboundBuffer = this.outboundBuffer;
                if (outboundBuffer == null)
                {
                    // If the outboundBuffer is null we know the channel was closed and so
                    // need to fail the future right away. If it is not null the handling of the rest
                    // will be done input flush0()
                    // See https://github.com/netty/netty/issues/2362

                    // release message now to prevent resource-leak
                    ReferenceCountUtil.Release(msg);
                    Util.SafeSetFailure(promise, new ClosedChannelException(), Logger);
                    return;
                }

                int size;
                try
                {
                    msg = this.channel.FilterOutboundMessage(msg);
                    size = this.channel.pipeline.EstimatorHandle.Size(msg);
                    if (size < 0)
                    {
                        size = 0;
                    }
                }
                catch (Exception t)
                {
                    ReferenceCountUtil.Release(msg);
                    Util.SafeSetFailure(promise, t, Logger);
                    return;
                }

                outboundBuffer.AddMessage(msg, size, promise);
            }

            public void Flush()
            {
                this.AssertEventLoop();

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
                if (!this.CanWrite)
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
                            outboundBuffer.FailFlushed(new ClosedChannelException(), false);
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

            protected virtual bool CanWrite => this.channel.Active;

            protected bool EnsureOpen(IPromise promise)
            {
                if (this.channel.Open)
                {
                    return true;
                }

                Util.SafeSetFailure(promise, new ClosedChannelException(), Logger);
                return false;
            }

            protected Task CreateClosedChannelExceptionTask() => TaskEx.FromException(new ClosedChannelException());

            protected void CloseIfClosed()
            {
                if (this.channel.Open)
                {
                    return;
                }
                this.CloseSafe();
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

            /// <summary>
            /// Prepares to close the <see cref="IChannel"/>. If this method returns an <see cref="IEventExecutor"/>, the
            /// caller must call the <see cref="IEventExecutor.Execute(DotNetty.Common.Concurrency.IRunnable)"/> method with a task that calls
            /// <see cref="AbstractChannel.DoClose"/> on the returned <see cref="IEventExecutor"/>. If this method returns <c>null</c>,
            /// <see cref="AbstractChannel.DoClose"/> must be called from the caller thread. (i.e. <see cref="IEventLoop"/>)
            /// </summary>
            protected virtual IEventExecutor PrepareToClose() => null;
        }

        /// <summary>
        ///     Return {@code true} if the given {@link EventLoop} is compatible with this instance.
        /// </summary>
        protected abstract bool IsCompatible(IEventLoop eventLoop);

        /// <summary>
        ///     Is called after the {@link Channel} is registered with its {@link EventLoop} as part of the register process.
        ///     Sub-classes may override this method
        /// </summary>
        protected virtual void DoRegister()
        {
            // NOOP
        }

        /// <summary>
        ///     Bind the {@link Channel} to the {@link EndPoint}
        /// </summary>
        protected abstract void DoBind(EndPoint localAddress);

        /// <summary>
        ///     Disconnect this {@link Channel} from its remote peer
        /// </summary>
        protected abstract void DoDisconnect();

        /// <summary>
        ///     Close the {@link Channel}
        /// </summary>
        protected abstract void DoClose();

        /// <summary>
        ///     Deregister the {@link Channel} from its {@link EventLoop}.
        ///     Sub-classes may override this method
        /// </summary>
        protected virtual void DoDeregister()
        {
            // NOOP
        }

        /// <summary>
        ///     ScheduleAsync a read operation.
        /// </summary>
        protected abstract void DoBeginRead();

        /// <summary>
        ///     Flush the content of the given buffer to the remote peer.
        /// </summary>
        protected abstract void DoWrite(ChannelOutboundBuffer input);

        /// <summary>
        ///     Invoked when a new message is added to a {@link ChannelOutboundBuffer} of this {@link AbstractChannel}, so that
        ///     the {@link Channel} implementation converts the message to another. (e.g. heap buffer -> direct buffer)
        /// </summary>
        protected virtual object FilterOutboundMessage(object msg) => msg;
    }
}