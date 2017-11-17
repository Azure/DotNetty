// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Local
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    /**
 * A {@link Channel} for the local transport.
 */
    public class LocalChannel : AbstractChannel
    {
        enum State
        {
            Open,
            Bound,
            Connected,
            Closed
        }

        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<LocalChannel>();
        static readonly ChannelMetadata METADATA = new ChannelMetadata(false);
        static readonly int MAX_READER_STACK_DEPTH = 8;
        static readonly ClosedChannelException DoWriteClosedChannelException = new ClosedChannelException();
        static readonly ClosedChannelException DoCloseClosedChannelException = new ClosedChannelException();

        readonly IQueue<object> inboundBuffer = PlatformDependent.NewMpscQueue<object>();

        volatile State state;
        volatile LocalChannel peer;
        volatile LocalAddress localAddress;
        volatile LocalAddress remoteAddress;
        volatile TaskCompletionSource connectPromise;
        volatile bool readInProgress;
        volatile bool registerInProgress;
        volatile bool writeInProgress;
        volatile Task finishReadFuture;

        readonly Action shutdownHook;

        public LocalChannel()
            : this(null, null)
        {
        }

        internal LocalChannel(LocalServerChannel parent, LocalChannel peer)
            : base(parent)
        {
            //this.Configuration.Allocator(new PreferHeapByteBufAllocator(config.getAllocator()));
            this.peer = peer;
            if (parent != null)
            {
                this.localAddress = parent.LocalAddress;
            }
            if (peer != null)
            {
                this.remoteAddress = peer.LocalAddress;
            }

            this.Configuration = new DefaultChannelConfiguration(this);
            this.shutdownHook = () => this.Unsafe.CloseAsync();
        }

        public override ChannelMetadata Metadata => METADATA;

        public override IChannelConfiguration Configuration { get; }

        public new LocalServerChannel Parent => (LocalServerChannel)base.Parent;

        public new LocalAddress LocalAddress => (LocalAddress)base.LocalAddress;

        public new LocalAddress RemoteAddress => (LocalAddress)base.RemoteAddress;

        public override bool Open => this.state != State.Closed;

        public override bool Active => this.state == State.Connected;

        protected override IChannelUnsafe NewUnsafe() => new LocalUnsafe(this);

        protected override bool IsCompatible(IEventLoop loop) => loop is SingleThreadEventLoop;

        protected override EndPoint LocalAddressInternal => this.localAddress;

        protected override EndPoint RemoteAddressInternal => this.remoteAddress;

        void InternalRead()
        {
            IChannelPipeline pipeline = this.Pipeline;
            for (;;)
            {
                if (!this.inboundBuffer.TryDequeue(out object m))
                {
                    break;
                }

                pipeline.FireChannelRead(m);
            }
            pipeline.FireChannelReadComplete();
        }

        protected override void DoRegister()
        {
            // Check if both peer and parent are non-null because this channel was created by a LocalServerChannel.
            // This is needed as a peer may not be null also if a LocalChannel was connected before and
            // deregistered / registered later again.
            //
            // See https://github.com/netty/netty/issues/2400
            if (this.peer != null && this.Parent != null)
            {
                // Store the peer in a local variable as it may be set to null if doClose() is called.
                // Because of this we also set registerInProgress to true as we check for this in doClose() and make sure
                // we delay the fireChannelInactive() to be fired after the fireChannelActive() and so keep the correct
                // order of events.
                //
                // See https://github.com/netty/netty/issues/2144
                var peer = this.peer;
                this.registerInProgress = true;
                this.state = State.Connected;

                peer.remoteAddress = this.Parent?.LocalAddress;
                peer.state = State.Connected;

                // Always call peer.eventLoop().execute() even if peer.eventLoop().InEventLoop is true.
                // This ensures that if both channels are on the same event loop, the peer's channelActive
                // event is triggered *after* this channel's channelRegistered event, so that this channel's
                // pipeline is fully initialized by ChannelInitializer before any channelRead events.
                peer.EventLoop.Execute(
                    () =>
                    {
                        this.registerInProgress = false;
                        var promise = peer.connectPromise;

                        // Only trigger fireChannelActive() if the promise was not null and was not completed yet.
                        // connectPromise may be set to null if doClose() was called in the meantime.
                        if (promise != null && promise.TryComplete())
                        {
                            peer.Pipeline.FireChannelActive();
                        }
                    }
                );
            }
            ((SingleThreadEventExecutor)this.EventLoop).AddShutdownHook(this.shutdownHook);
        }

        protected override void DoBind(EndPoint localAddress)
        {
            this.localAddress = LocalChannelRegistry.Register(this, this.localAddress, localAddress);
            this.state = State.Bound;
        }

        protected override void DoDisconnect() => this.DoClose();

        protected override void DoClose()
        {
            var peer = this.peer;
            var oldState = this.state;

            try
            {
                if (oldState != State.Closed)
                {
                    // Update all internal state before the closeFuture is notified.
                    if (this.localAddress != null)
                    {
                        if (this.Parent == null)
                        {
                            LocalChannelRegistry.Unregister(this.localAddress);
                        }
                        this.localAddress = null;
                    }

                    // State change must happen before finishPeerRead to ensure writes are released either in doWrite or
                    // channelRead.
                    this.state = State.Closed;

                    // Preserve order of event and force a read operation now before the close operation is processed.
                    this.FinishPeerRead(this);

                    TaskCompletionSource promise = this.connectPromise;
                    if (promise != null)
                    {
                        // Use tryFailure() instead of setFailure() to avoid the race against cancel().
                        promise.TrySetException(DoCloseClosedChannelException);
                        this.connectPromise = null;
                    }
                }

                if (peer != null)
                {
                    this.peer = null;
                    // Need to execute the close in the correct EventLoop (see https://github.com/netty/netty/issues/1777).
                    // Also check if the registration was not done yet. In this case we submit the close to the EventLoop
                    // to make sure its run after the registration completes
                    // (see https://github.com/netty/netty/issues/2144).
                    IEventLoop peerEventLoop = peer.EventLoop;
                    bool peerIsActive = peer.Active;
                    if (peerEventLoop.InEventLoop && !this.registerInProgress)
                    {
                        peer.TryClose(peerIsActive);
                    }
                    else
                    {
                        try
                        {
                            peerEventLoop.Execute(() => peer.TryClose(peerIsActive));
                        }
                        catch (Exception cause)
                        {
                            Logger.Warn("Releasing Inbound Queues for channels {}-{} because exception occurred!", this, peer, cause);

                            if (peerEventLoop.InEventLoop)
                            {
                                peer.ReleaseInboundBuffers();
                            }
                            else
                            {
                                // inboundBuffers is a SPSC so we may leak if the event loop is shutdown prematurely or
                                // rejects the close Runnable but give a best effort.
                                peer.CloseAsync();
                            }
                            throw;
                        }
                    }
                }
            }
            finally
            {
                // Release all buffers if the Channel was already registered in the past and if it was not closed before.
                if (oldState != State.Closed)
                {
                    // We need to release all the buffers that may be put into our inbound queue since we closed the Channel
                    // to ensure we not leak any memory. This is fine as it basically gives the same guarantees as TCP which
                    // means even if the promise was notified before its not really guaranteed that the "remote peer" will
                    // see the buffer at all.
                    this.ReleaseInboundBuffers();
                }
            }
        }

        void TryClose(bool isActive)
        {
            if (isActive)
            {
                this.Unsafe.CloseAsync();
            }
            else
            {
                this.ReleaseInboundBuffers();
            }
        }

        protected override void DoDeregister() => ((SingleThreadEventExecutor)this.EventLoop).RemoveShutdownHook(this.shutdownHook);

        protected override void DoBeginRead()
        {
            if (this.readInProgress)
            {
                return;
            }

            IChannelPipeline pipeline = this.Pipeline;
            IQueue<object> inboundBuffer = this.inboundBuffer;
            if (inboundBuffer.IsEmpty)
            {
                this.readInProgress = true;
                return;
            }

            InternalThreadLocalMap threadLocals = InternalThreadLocalMap.Get();
            int stackDepth = threadLocals.LocalChannelReaderStackDepth;
            if (stackDepth < MAX_READER_STACK_DEPTH)
            {
                threadLocals.LocalChannelReaderStackDepth = stackDepth + 1;

                try
                {
                    for (;;)
                    {
                        if (!inboundBuffer.TryDequeue(out object received))
                        {
                            break;
                        }

                        pipeline.FireChannelRead(received);
                    }
                    pipeline.FireChannelReadComplete();
                }
                finally
                {
                    threadLocals.LocalChannelReaderStackDepth = stackDepth;
                }
            }
            else
            {
                try
                {
                    this.EventLoop.Execute(this.InternalRead);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Closing Local channels {}-{} because exception occurred!", this, this.peer, ex);
                    this.CloseAsync();
                    this.peer.CloseAsync();
                    throw;
                }
            }
        }

        protected override void DoWrite(ChannelOutboundBuffer buffer)
        {
            switch (this.state)
            {
                case State.Open:
                case State.Bound:
                    throw new NotYetConnectedException();
                case State.Closed:
                    throw DoWriteClosedChannelException;
                case State.Connected:
                    break;
            }

            LocalChannel peer = this.peer;

            this.writeInProgress = true;
            try
            {
                for (;;)
                {
                    object msg = buffer.Current;
                    if (msg == null)
                    {
                        break;
                    }

                    try
                    {
                        // It is possible the peer could have closed while we are writing, and in this case we should
                        // simulate real socket behavior and ensure the write operation is failed.
                        if (peer.state == State.Connected)
                        {
                            peer.inboundBuffer.TryEnqueue(ReferenceCountUtil.Retain(msg));
                            buffer.Remove();
                        }
                        else
                        {
                            buffer.Remove(DoWriteClosedChannelException);
                        }
                    }
                    catch (Exception cause)
                    {
                        buffer.Remove(cause);
                    }
                }
            }
            finally
            {
                // The following situation may cause trouble:
                // 1. Write (with promise X)
                // 2. promise X is completed when in.remove() is called, and a listener on this promise calls close()
                // 3. Then the close event will be executed for the peer before the write events, when the write events
                // actually happened before the close event.
                this.writeInProgress = false;
            }

            this.FinishPeerRead(peer);
        }

        void FinishPeerRead(LocalChannel peer)
        {
            // If the peer is also writing, then we must schedule the event on the event loop to preserve read order.
            if (peer.EventLoop == this.EventLoop && !peer.writeInProgress)
            {
                this.FinishPeerRead0(peer);
            }
            else
            {
                this.RunFinishPeerReadTask(peer);
            }
        }

        void RunFinishPeerReadTask(LocalChannel peer)
        {
            // If the peer is writing, we must wait until after reads are completed for that peer before we can read. So
            // we keep track of the task, and coordinate later that our read can't happen until the peer is done.
            try
            {
                if (peer.writeInProgress)
                {
                    peer.finishReadFuture = peer.EventLoop.SubmitAsync(
                        () =>
                        {
                            this.FinishPeerRead0(peer);
                            return (object)null;
                        });
                }
                else
                {
                    peer.EventLoop.Execute(() => this.FinishPeerRead0(peer));
                }
            }
            catch (Exception cause)
            {
                Logger.Warn("Closing Local channels {}-{} because exception occurred!", this, peer, cause);
                this.CloseAsync();
                peer.CloseAsync();
                throw;
            }
        }

        void ReleaseInboundBuffers()
        {
            Contract.Assert(this.EventLoop == null || this.EventLoop.InEventLoop);
            this.readInProgress = false;
            var inboundBuffer = this.inboundBuffer;
            while (inboundBuffer.TryDequeue(out object msg))
            {
                ReferenceCountUtil.Release(msg);
            }
        }

        void FinishPeerRead0(LocalChannel peer)
        {
            Task peerFinishReadFuture = peer.finishReadFuture;
            if (peerFinishReadFuture != null)
            {
                if (!peerFinishReadFuture.IsCompleted)
                {
                    this.RunFinishPeerReadTask(peer);
                    return;
                }
                else
                {
                    // Lazy unset to make sure we don't prematurely unset it while scheduling a new task.
                    Interlocked.CompareExchange(ref peer.finishReadFuture, null, peerFinishReadFuture);
                }
            }

            IChannelPipeline peerPipeline = peer.Pipeline;
            if (peer.readInProgress)
            {
                peer.readInProgress = false;
                while (peer.inboundBuffer.TryDequeue(out object received))
                {
                    peerPipeline.FireChannelRead(received);
                }
                peerPipeline.FireChannelReadComplete();
            }
        }

        class LocalUnsafe : AbstractUnsafe
        {
            readonly LocalChannel localChannel;

            public LocalUnsafe(LocalChannel channel)
                : base(channel)
            {
                this.localChannel = channel;
            }

            public override Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
            {
                var promise = new TaskCompletionSource();

                if (this.localChannel.state == State.Connected)
                {
                    var cause = new AlreadyConnectedException();
                    Util.SafeSetFailure(promise, cause, Logger);
                    this.localChannel.Pipeline.FireExceptionCaught(cause);
                    return promise.Task;
                }

                if (this.localChannel.connectPromise != null)
                {
                    throw new ConnectionPendingException();
                }

                this.localChannel.connectPromise = promise;

                if (this.localChannel.state != State.Bound)
                {
                    // Not bound yet and no localAddress specified - get one.
                    if (localAddress == null)
                    {
                        localAddress = new LocalAddress(this.localChannel);
                    }
                }

                if (localAddress != null)
                {
                    try
                    {
                        this.localChannel.DoBind(localAddress);
                    }
                    catch (Exception ex)
                    {
                        Util.SafeSetFailure(promise, ex, Logger);
                        this.channel.CloseAsync();
                        return promise.Task;
                    }
                }

                IChannel boundChannel = LocalChannelRegistry.Get(remoteAddress);
                if (!(boundChannel is LocalServerChannel))
                {
                    Exception cause = new ConnectException($"connection refused: {remoteAddress}", null);
                    Util.SafeSetFailure(promise, cause, Logger);
                    this.localChannel.CloseAsync();
                    return promise.Task;
                }

                this.localChannel.peer = ((LocalServerChannel)boundChannel).Serve(this.localChannel);
                return promise.Task;
            }
        }
    }
}