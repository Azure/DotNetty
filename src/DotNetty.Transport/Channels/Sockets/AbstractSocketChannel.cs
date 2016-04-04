// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    public abstract class AbstractSocketChannel : AbstractChannel
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<AbstractSocketChannel>();

        [Flags]
        protected enum StateFlags
        {
            Open = 1,
            ReadScheduled = 1 << 1,
            WriteScheduled = 1 << 2,
            Active = 1 << 3
            // todo: add input shutdown and read pending here as well?
        }

        internal static readonly EventHandler<SocketAsyncEventArgs> IoCompletedCallback = OnIoCompleted;
        static readonly Action<object, object> ConnectCallbackAction = (u, e) => ((ISocketChannelUnsafe)u).FinishConnect((SocketChannelAsyncOperation)e);
        static readonly Action<object, object> ReadCallbackAction = (u, e) => ((ISocketChannelUnsafe)u).FinishRead((SocketChannelAsyncOperation)e);
        static readonly Action<object, object> WriteCallbackAction = (u, e) => ((ISocketChannelUnsafe)u).FinishWrite((SocketChannelAsyncOperation)e);

        protected readonly Socket Socket;
        SocketChannelAsyncOperation readOperation;
        SocketChannelAsyncOperation writeOperation;
        volatile bool inputShutdown;
        volatile bool readPending;
        volatile StateFlags state;

        TaskCompletionSource connectPromise;
        IScheduledTask connectCancellationTask;

        protected AbstractSocketChannel(IChannel parent, Socket socket)
            : base(parent)
        {
            this.Socket = socket;
            this.state = StateFlags.Open;

            try
            {
                this.Socket.Blocking = false;
            }
            catch (SocketException ex)
            {
                try
                {
                    socket.Close();
                }
                catch (SocketException ex2)
                {
                    if (Logger.WarnEnabled)
                    {
                        Logger.Warn("Failed to close a partially initialized socket.", ex2);
                    }
                }

                throw new ChannelException("Failed to enter non-blocking mode.", ex);
            }
        }

        public override bool Open
        {
            get { return this.IsInState(StateFlags.Open); }
        }

        public override bool Active
        {
            get { return this.IsInState(StateFlags.Active); }
        }

        protected bool ReadPending
        {
            get { return this.readPending; }
            set { this.readPending = value; }
        }

        protected bool InputShutdown
        {
            get { return this.inputShutdown; }
        }

        protected void ShutdownInput()
        {
            this.inputShutdown = true;
        }

        protected void SetState(StateFlags stateToSet)
        {
            this.state |= stateToSet;
        }

        protected bool ResetState(StateFlags stateToReset)
        {
            StateFlags oldState = this.state;
            if ((oldState & stateToReset) != 0)
            {
                this.state = oldState & ~stateToReset;
                return true;
            }
            return false;
        }

        protected bool IsInState(StateFlags stateToCheck)
        {
            return (this.state & stateToCheck) == stateToCheck;
        }

        protected SocketChannelAsyncOperation ReadOperation
        {
            get { return this.readOperation ?? (this.readOperation = new SocketChannelAsyncOperation(this, true)); }
        }

        protected SocketChannelAsyncOperation PrepareWriteOperation(IByteBuffer buffer)
        {
            SocketChannelAsyncOperation operation = this.writeOperation ?? (this.writeOperation = new SocketChannelAsyncOperation(this, false));
            if (!buffer.HasArray)
            {
                throw new NotImplementedException("IByteBuffer implementations not backed by array are currently not supported.");
            }
            operation.SetBuffer(buffer.Array, buffer.ArrayOffset + buffer.WriterIndex, buffer.WritableBytes);
            return operation;
        }

        protected void ResetWriteOperation()
        {
            SocketChannelAsyncOperation operation = this.writeOperation;
            Contract.Requires(operation != null);
            operation.SetBuffer(null, 0, 0);
        }

        /// <remarks>PORT NOTE: matches behavior of NioEventLoop.processSelectedKey</remarks>
        static void OnIoCompleted(object sender, SocketAsyncEventArgs args)
        {
            var operation = (SocketChannelAsyncOperation)args;
            AbstractSocketChannel channel = operation.Channel;
            var @unsafe = (ISocketChannelUnsafe)channel.Unsafe;
            IEventLoop eventLoop = channel.EventLoop;
            switch (args.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    if (eventLoop.InEventLoop)
                    {
                        @unsafe.FinishRead(operation);
                    }
                    else
                    {
                        eventLoop.Execute(ReadCallbackAction, @unsafe, operation);
                    }
                    break;
                case SocketAsyncOperation.Connect:
                    if (eventLoop.InEventLoop)
                    {
                        @unsafe.FinishConnect(operation);
                    }
                    else
                    {
                        eventLoop.Execute(ConnectCallbackAction, @unsafe, operation);
                    }
                    break;
                case SocketAsyncOperation.Receive:
                    if (eventLoop.InEventLoop)
                    {
                        @unsafe.FinishRead(operation);
                    }
                    else
                    {
                        eventLoop.Execute(ReadCallbackAction, @unsafe, operation);
                    }
                    break;
                case SocketAsyncOperation.Send:
                    if (eventLoop.InEventLoop)
                    {
                        @unsafe.FinishWrite(operation);
                    }
                    else
                    {
                        eventLoop.Execute(WriteCallbackAction, @unsafe, operation);
                    }
                    break;
                default:
                    // todo: think of a better way to comm exception
                    throw new ArgumentException("The last operation completed on the socket was not expected");
            }
        }

        internal interface ISocketChannelUnsafe : IChannelUnsafe
        {
            /// <summary>
            /// Finish connect
            /// </summary>
            void FinishConnect(SocketChannelAsyncOperation operation);

            /// <summary>
            /// Read from underlying {@link SelectableChannel}
            /// </summary>
            void FinishRead(SocketChannelAsyncOperation operation);

            void FinishWrite(SocketChannelAsyncOperation operation);
        }

        protected abstract class AbstractSocketUnsafe : AbstractUnsafe, ISocketChannelUnsafe
        {
            protected AbstractSocketUnsafe(AbstractSocketChannel channel)
                : base(channel)
            {
            }

            public AbstractSocketChannel Channel
            {
                get { return (AbstractSocketChannel)this.channel; }
            }

            public sealed override Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
            {
                // todo: handle cancellation
                AbstractSocketChannel ch = this.Channel;
                if (!ch.Open)
                {
                    return this.CreateClosedChannelExceptionTask();
                }

                try
                {
                    if (ch.connectPromise != null)
                    {
                        throw new InvalidOperationException("connection attempt already made");
                    }

                    bool wasActive = this.channel.Active;
                    if (ch.DoConnect(remoteAddress, localAddress))
                    {
                        this.FulfillConnectPromise(wasActive);
                        return TaskEx.Completed;
                    }
                    else
                    {
                        ch.connectPromise = new TaskCompletionSource(remoteAddress);

                        // Schedule connect timeout.
                        TimeSpan connectTimeout = ch.Configuration.ConnectTimeout;
                        if (connectTimeout > TimeSpan.Zero)
                        {
                            ch.connectCancellationTask = ch.EventLoop.Schedule(
                                (c, a) =>
                                {
                                    // todo: make static / cache delegate?..
                                    var self = (AbstractSocketChannel)c;
                                    // todo: call Socket.CancelConnectAsync(...)
                                    TaskCompletionSource promise = self.connectPromise;
                                    var cause = new ConnectTimeoutException("connection timed out: " + a.ToString());
                                    if (promise != null && promise.TrySetException(cause))
                                    {
                                        self.CloseAsync();
                                    }
                                },
                                this.channel,
                                remoteAddress,
                                connectTimeout);
                        }

                        ch.connectPromise.Task.ContinueWith(
                            (t, s) =>
                            {
                                var c = (AbstractSocketChannel)s;
                                if (c.connectCancellationTask != null)
                                {
                                    c.connectCancellationTask.Cancel();
                                }
                                c.connectPromise = null;
                                c.CloseAsync();
                            },
                            ch,
                            TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.ExecuteSynchronously);

                        return ch.connectPromise.Task;
                    }
                }
                catch (Exception ex)
                {
                    this.CloseIfClosed();
                    return TaskEx.FromException(this.AnnotateConnectException(ex, remoteAddress));
                }
            }

            void FulfillConnectPromise(bool wasActive)
            {
                TaskCompletionSource promise = this.Channel.connectPromise;
                if (promise == null)
                {
                    // Closed via cancellation and the promise has been notified already.
                    return;
                }

                // trySuccess() will return false if a user cancelled the connection attempt.
                bool promiseSet = promise.TryComplete();

                // Regardless if the connection attempt was cancelled, channelActive() event should be triggered,
                // because what happened is what happened.
                if (!wasActive && this.channel.Active)
                {
                    this.channel.Pipeline.FireChannelActive();
                }

                // If a user cancelled the connection attempt, close the channel, which is followed by channelInactive().
                if (!promiseSet)
                {
                    this.CloseAsync();
                }
            }

            void FulfillConnectPromise(Exception cause)
            {
                TaskCompletionSource promise = this.Channel.connectPromise;
                if (promise == null)
                {
                    // Closed via cancellation and the promise has been notified already.
                    return;
                }

                // Use tryFailure() instead of setFailure() to avoid the race against cancel().
                promise.TrySetException(cause);
                this.CloseIfClosed();
            }

            public void FinishConnect(SocketChannelAsyncOperation operation)
            {
                Contract.Assert(this.channel.EventLoop.InEventLoop);

                AbstractSocketChannel ch = this.Channel;
                try
                {
                    bool wasActive = ch.Active;
                    ch.DoFinishConnect(operation);
                    this.FulfillConnectPromise(wasActive);
                }
                catch (Exception ex)
                {
                    TaskCompletionSource promise = ch.connectPromise;
                    EndPoint remoteAddress = promise == null ? null : (EndPoint)promise.Task.AsyncState;
                    this.FulfillConnectPromise(this.AnnotateConnectException(ex, remoteAddress));
                }
                finally
                {
                    // Check for null as the connectTimeoutFuture is only created if a connectTimeoutMillis > 0 is used
                    // See https://github.com/netty/netty/issues/1770
                    if (ch.connectCancellationTask != null)
                    {
                        ch.connectCancellationTask.Cancel();
                    }
                    ch.connectPromise = null;
                }
            }

            public abstract void FinishRead(SocketChannelAsyncOperation operation);

            protected override sealed void Flush0()
            {
                // Flush immediately only when there's no pending flush.
                // If there's a pending flush operation, event loop will call FinishWrite() later,
                // and thus there's no need to call it now.
                if (this.IsFlushPending())
                {
                    return;
                }
                base.Flush0();
            }

            public void FinishWrite(SocketChannelAsyncOperation operation)
            {
                ChannelOutboundBuffer input = this.OutboundBuffer;
                try
                {
                    operation.Validate();
                    int sent = operation.BytesTransferred;
                    this.Channel.ResetWriteOperation();
                    if (sent > 0)
                    {
                        object msg = input.Current;
                        var buffer = msg as IByteBuffer;
                        if (buffer != null)
                        {
                            buffer.SetWriterIndex(buffer.WriterIndex + sent);
                        }
                        // todo: FileRegion support
                    }
                }
                catch (Exception ex)
                {
                    input.FailFlushed(ex, true);
                    throw;
                }

                // directly call super.flush0() to force a flush now
                base.Flush0();
            }

            bool IsFlushPending()
            {
                return this.Channel.IsInState(StateFlags.WriteScheduled);
            }
        }

        protected override bool IsCompatible(IEventLoop eventLoop)
        {
            return true;
        }

        protected override void DoBeginRead()
        {
            if (this.inputShutdown)
            {
                return;
            }

            if (!this.Open)
            {
                return;
            }

            this.readPending = true;

            if (!this.IsInState(StateFlags.ReadScheduled))
            {
                this.state |= StateFlags.ReadScheduled;
                this.ScheduleSocketRead();
            }
        }

        protected abstract void ScheduleSocketRead();

        /// <summary>
        ///  Connect to the remote peer
        /// </summary>
        protected abstract bool DoConnect(EndPoint remoteAddress, EndPoint localAddress);

        /// <summary>
        /// Finish the connect
        /// </summary>
        protected abstract void DoFinishConnect(SocketChannelAsyncOperation operation);

        protected override void DoClose()
        {
            TaskCompletionSource promise = this.connectPromise;
            if (promise != null)
            {
                // Use TrySetException() instead of SetException() to avoid the race against cancellation due to timeout.
                promise.TrySetException(ClosedChannelException);
                this.connectPromise = null;
            }

            IScheduledTask cancellationTask = this.connectCancellationTask;
            if (cancellationTask != null)
            {
                cancellationTask.Cancel();
                this.connectCancellationTask = null;
            }

            SocketChannelAsyncOperation readOp = this.readOperation;
            if (readOp != null)
            {
                readOp.Dispose();
            }

            SocketChannelAsyncOperation writeOp = this.writeOperation;
            if (writeOp != null)
            {
                writeOp.Dispose();
            }
        }
    }
}