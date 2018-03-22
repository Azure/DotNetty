// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    public abstract class NativeChannel : AbstractChannel
    {
        protected static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<NativeChannel>();

        [Flags]
        protected enum StateFlags
        {
            Open = 1,
            ReadScheduled = 1 << 1,
            WriteScheduled = 1 << 2,
            Active = 1 << 3
        }

        internal bool ReadPending;
        volatile StateFlags state;

        TaskCompletionSource connectPromise;
        IScheduledTask connectCancellationTask;

        protected NativeChannel(IChannel parent) : base(parent)
        {
            this.state = StateFlags.Open;
        }

        public override bool Open => this.IsInState(StateFlags.Open);

        public override bool Active => this.IsInState(StateFlags.Active);

        protected override bool IsCompatible(IEventLoop eventLoop) => eventLoop is LoopExecutor;

        protected bool IsInState(StateFlags stateToCheck) => (this.state & stateToCheck) == stateToCheck;

        protected void SetState(StateFlags stateToSet) => this.state |= stateToSet;

        protected StateFlags ResetState(StateFlags stateToReset)
        {
            StateFlags oldState = this.state;
            if ((oldState & stateToReset) != 0)
            {
                this.state = oldState & ~stateToReset;
            }
            return oldState;
        }

        protected bool TryResetState(StateFlags stateToReset)
        {
            StateFlags oldState = this.state;
            if ((oldState & stateToReset) != 0)
            {
                this.state = oldState & ~stateToReset;
                return true;
            }
            return false;
        }

        void DoConnect(EndPoint remoteAddress, EndPoint localAddress)
        {
            ConnectRequest request = null;
            try
            {
                if (localAddress != null)
                {
                    this.DoBind(localAddress);
                }
                request = new TcpConnect((INativeUnsafe)this.Unsafe, (IPEndPoint)remoteAddress);
            }
            catch
            {
                request?.Dispose();
                throw;
            }
        }

        void DoFinishConnect() => this.OnConnected();

        protected override void DoClose()
        {
            TaskCompletionSource promise = this.connectPromise;
            if (promise != null)
            {
                promise.TrySetException(new ClosedChannelException());
                this.connectPromise = null;
            }
        }

        protected virtual void OnConnected()
        {
            this.SetState(StateFlags.Active);
            this.CacheLocalAddress();
            this.CacheRemoteAddress();
        }

        protected abstract void DoStopRead();

        internal abstract NativeHandle GetHandle();

        internal interface INativeUnsafe
        {
            IntPtr UnsafeHandle { get; }

            void FinishConnect(ConnectRequest request);

            uv_buf_t PrepareRead(ReadOperation readOperation);

            void FinishRead(ReadOperation readOperation);

            void FinishWrite(int bytesWritten, OperationException error);
        }

        protected abstract class NativeChannelUnsafe : AbstractUnsafe, INativeUnsafe
        {
            protected NativeChannelUnsafe(NativeChannel channel) : base(channel)
            {
            }

            public override Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
            {
                var ch = (NativeChannel)this.channel;
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

                    ch.connectPromise = new TaskCompletionSource(remoteAddress);

                    // Schedule connect timeout.
                    TimeSpan connectTimeout = ch.Configuration.ConnectTimeout;
                    if (connectTimeout > TimeSpan.Zero)
                    {
                        ch.connectCancellationTask = ch.EventLoop
                            .Schedule(CancelConnect, ch, remoteAddress, connectTimeout);
                    }

                    ch.DoConnect(remoteAddress, localAddress);
                    return ch.connectPromise.Task;
                }
                catch (Exception ex)
                {
                    this.CloseIfClosed();
                    return TaskEx.FromException(this.AnnotateConnectException(ex, remoteAddress));
                }
            }

            static void CancelConnect(object context, object state)
            {
                var ch = (NativeChannel)context;
                var address = (IPEndPoint)state;
                TaskCompletionSource promise = ch.connectPromise;
                var cause = new ConnectTimeoutException($"connection timed out: {address}");
                if (promise != null && promise.TrySetException(cause))
                {
                    ((NativeChannelUnsafe)ch.Unsafe).CloseSafe();
                }
            }

            // Connect request callback from libuv thread
            void INativeUnsafe.FinishConnect(ConnectRequest request)
            {
                var ch = (NativeChannel)this.channel;
                ch.connectCancellationTask?.Cancel();

                TaskCompletionSource promise = ch.connectPromise;
                bool success = false;
                try
                {
                    if (promise != null) // Not cancelled from timed out
                    {
                        OperationException error = request.Error;
                        if (error != null)
                        {
                            if (error.ErrorCode == ErrorCode.ETIMEDOUT)
                            {
                                // Connection timed out should use the standard ConnectTimeoutException
                                promise.TrySetException(new ConnectTimeoutException(error.ToString()));
                            }
                            else
                            {
                                promise.TrySetException(new ChannelException(error));
                            }
                        }
                        else
                        {
                            bool wasActive = ch.Active;
                            ch.DoFinishConnect();
                            success = promise.TryComplete();

                            // Regardless if the connection attempt was cancelled, channelActive() 
                            // event should be triggered, because what happened is what happened.
                            if (!wasActive && ch.Active)
                            {
                                ch.Pipeline.FireChannelActive();
                            }
                        }
                    }
                }
                finally
                {
                    request.Dispose();
                    ch.connectPromise = null;
                    if (!success)
                    {
                        this.CloseSafe();
                    }
                }
            }

            public abstract IntPtr UnsafeHandle { get; }

            // Allocate callback from libuv thread
            uv_buf_t INativeUnsafe.PrepareRead(ReadOperation readOperation)
            {
                Debug.Assert(readOperation != null);

                var ch = (NativeChannel)this.channel;
                IChannelConfiguration config = ch.Configuration;
                IByteBufferAllocator allocator = config.Allocator;

                IRecvByteBufAllocatorHandle allocHandle = this.RecvBufAllocHandle;
                IByteBuffer buffer = allocHandle.Allocate(allocator);
                allocHandle.AttemptedBytesRead = buffer.WritableBytes;

                return readOperation.GetBuffer(buffer);
            }

            // Read callback from libuv thread
            void INativeUnsafe.FinishRead(ReadOperation operation)
            {
                var ch = (NativeChannel)this.channel;
                IChannelConfiguration config = ch.Configuration;
                IChannelPipeline pipeline = ch.Pipeline;
                OperationException error = operation.Error;

                bool close = error != null || operation.EndOfStream;
                IRecvByteBufAllocatorHandle allocHandle = this.RecvBufAllocHandle;
                allocHandle.Reset(config);

                IByteBuffer buffer = operation.Buffer;
                Debug.Assert(buffer != null);

                allocHandle.LastBytesRead = operation.Status;
                if (allocHandle.LastBytesRead <= 0)
                {
                    // nothing was read -> release the buffer.
                    buffer.Release();
                }
                else
                {
                    buffer.SetWriterIndex(buffer.WriterIndex + operation.Status);
                    allocHandle.IncMessagesRead(1);

                    ch.ReadPending = false;
                    pipeline.FireChannelRead(buffer);
                }

                allocHandle.ReadComplete();
                pipeline.FireChannelReadComplete();

                if (close)
                {
                    if (error != null)
                    {
                        pipeline.FireExceptionCaught(new ChannelException(error));
                    }
                    this.CloseSafe();
                }
                else
                {
                    // If read is called from channel read or read complete
                    // do not stop reading
                    if (!ch.ReadPending && !config.AutoRead)
                    {
                        ch.DoStopRead();
                    }
                }
            }

            internal void CloseSafe() => CloseSafe(this.channel, this.channel.CloseAsync());

            internal static async void CloseSafe(object channelObject, Task closeTask)
            {
                try
                {
                    await closeTask;
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception ex)
                {
                    if (Logger.DebugEnabled)
                    {
                        Logger.Debug($"Failed to close channel {channelObject} cleanly.", ex);
                    }
                }
            }

            protected sealed override void Flush0()
            {
                var ch = (NativeChannel)this.channel;
                if (!ch.IsInState(StateFlags.WriteScheduled))
                {
                    base.Flush0();
                }
            }

            // Write request callback from libuv thread
            void INativeUnsafe.FinishWrite(int bytesWritten, OperationException error)
            {
                var ch = (NativeChannel)this.channel;
                bool resetWritePending = ch.TryResetState(StateFlags.WriteScheduled);
                Debug.Assert(resetWritePending);

                try
                {
                    ChannelOutboundBuffer input = this.OutboundBuffer;
                    if (error != null)
                    {
                        input.FailFlushed(error, true);
                        CloseSafe(ch, this.CloseAsync(new ChannelException("Failed to write", error), false));
                    }
                    else
                    {
                        if (bytesWritten > 0)
                        {
                            input.RemoveBytes(bytesWritten);
                        }
                    }
                }
                catch (Exception ex)
                {
                    CloseSafe(ch, this.CloseAsync(new ClosedChannelException("Failed to write", ex), false));
                }
                this.Flush0();
            }
        }
    }
}
