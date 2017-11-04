// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    public abstract class NativeChannel : AbstractChannel
    {
        [Flags]
        protected enum StateFlags
        {
            Open = 1,
            ReadScheduled = 1 << 1,
            WriteScheduled = 1 << 2,
            Active = 1 << 3
        }

        volatile StateFlags state;
        TaskCompletionSource connectPromise;

        protected NativeChannel(IChannel parent) : base(parent)
        {
            this.state = StateFlags.Open;
        }

        public override bool Open => this.IsInState(StateFlags.Open);

        public override bool Active => this.IsInState(StateFlags.Active);

        protected override bool IsCompatible(IEventLoop eventLoop) => eventLoop is ILoopExecutor;

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

        void DoFinishConnect()
        {
            this.OnConnected();
            this.Pipeline.FireChannelActive();
        }

        protected void OnConnected()
        {
            this.SetState(StateFlags.Active);
            this.CacheLocalAddress();
            this.CacheRemoteAddress();
        }

        protected abstract void DoScheduleRead();

        internal abstract IntPtr GetLoopHandle();

        protected abstract class NativeChannelUnsafe : AbstractUnsafe, INativeUnsafe
        {
            NativeChannel Channel => (NativeChannel)this.channel;

            protected NativeChannelUnsafe(NativeChannel channel) : base(channel)
            {
            }

            public override Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
            {
                NativeChannel ch = this.Channel;
                if (!ch.Open)
                {
                    return this.CreateClosedChannelExceptionTask();
                }

                ConnectRequest request = null;
                try
                {
                    if (ch.connectPromise != null)
                    {
                        throw new InvalidOperationException("connection attempt already made");
                    }

                    ch.connectPromise = new TaskCompletionSource(remoteAddress);

                    if (localAddress != null)
                    {
                        ch.DoBind(localAddress);
                    }
                    request = new TcpConnect(this, (IPEndPoint)remoteAddress);
                    return ch.connectPromise.Task;
                }
                catch (Exception ex)
                {
                    request?.Dispose();
                    this.CloseIfClosed();
                    return TaskEx.FromException(this.AnnotateConnectException(ex, remoteAddress));
                }
            }

            void INativeUnsafe.FinishConnect(ConnectRequest request)
            {
                NativeChannel ch = this.Channel;
                if (ch.EventLoop.InEventLoop)
                {
                    this.FinishConnect(request);
                }
                else
                {
                    ch.EventLoop.Execute(ConnectCallbackAction, this, request);
                }
            }

            static readonly Action<object, object> ConnectCallbackAction = (u, e) => ((NativeChannelUnsafe)u).FinishConnect((ConnectRequest)e);

            void FinishConnect(ConnectRequest request)
            {
                NativeChannel ch = this.Channel;
                TaskCompletionSource promise = ch.connectPromise;
                try
                {
                    if (request.Error != null)
                    {
                        promise.TrySetException(request.Error);
                        this.CloseIfClosed();
                    }
                    else
                    {
                        ch.DoFinishConnect();
                        promise.TryComplete();
                    }
                }
                catch (Exception exception)
                {
                    promise.TrySetException(exception);
                    this.CloseIfClosed();
                }
                finally
                {
                    request.Dispose();
                    ch.connectPromise = null;
                }
            }

            public abstract IntPtr UnsafeHandle { get; }

            ReadOperation INativeUnsafe.PrepareRead()
            {
                NativeChannel ch = this.Channel;
                IChannelConfiguration config = ch.Configuration;
                IByteBufferAllocator allocator = config.Allocator;

                IRecvByteBufAllocatorHandle allocHandle = this.RecvBufAllocHandle;
                allocHandle.Reset(config);
                IByteBuffer buffer = allocHandle.Allocate(allocator);
                allocHandle.AttemptedBytesRead = buffer.WritableBytes;

                return new ReadOperation(this, buffer);
            }

            void INativeUnsafe.FinishRead(ReadOperation operation)
            {
                var ch = (NativeChannel)this.channel;
                if (ch.EventLoop.InEventLoop)
                {
                    this.FinishRead(operation);
                }
                else
                {
                    ch.EventLoop.Execute(ReadCallbackAction, this, operation);
                }
            } 

            static readonly Action<object, object> ReadCallbackAction = (u, e) => ((NativeChannelUnsafe)u).FinishRead((ReadOperation)e);

            void FinishRead(ReadOperation operation)
            {
                NativeChannel ch = this.Channel;
                IChannelPipeline pipeline = ch.Pipeline;
                IRecvByteBufAllocatorHandle allocHandle = this.RecvBufAllocHandle;

                IByteBuffer buffer = operation.Buffer;
                allocHandle.LastBytesRead = operation.Status;
                if (allocHandle.LastBytesRead <= 0)
                {
                    // nothing was read -> release the buffer.
                    buffer.SafeRelease();
                }
                else
                {
                    buffer.SetWriterIndex(buffer.WriterIndex + operation.Status);
                    pipeline.FireChannelRead(buffer);
                }

                allocHandle.IncMessagesRead(1);
                allocHandle.ReadComplete();
                pipeline.FireChannelReadComplete();

                if (operation.Error != null)
                {
                    pipeline.FireExceptionCaught(operation.Error);
                }

                if (operation.Error != null || operation.EndOfStream)
                {
                    ch.DoClose();
                }
            }

            void INativeUnsafe.FinishWrite(WriteRequest writeRequest)
            {
                AbstractChannel ch = this.channel;
                if (ch.EventLoop.InEventLoop)
                {
                    this.FinishWrite(writeRequest);
                }
                else
                {
                    ch.EventLoop.Execute(WriteCallbackAction, this, writeRequest);
                }
            } 

            static readonly Action<object, object> WriteCallbackAction = (u, e) => ((NativeChannelUnsafe)u).FinishWrite((WriteRequest)e);

            void FinishWrite(WriteRequest writeRequest)
            {
                try
                {
                    if (writeRequest.Error != null)
                    {
                        ChannelOutboundBuffer input = this.OutboundBuffer;
                        input?.FailFlushed(writeRequest.Error, true);
                        this.Channel.Pipeline.FireExceptionCaught(writeRequest.Error);
                    }
                }
                finally 
                {
                    writeRequest.Release();
                }
            }

            internal void ScheduleRead()
            {
                var ch = (NativeChannel)this.channel;
                if (ch.EventLoop.InEventLoop)
                {
                    ch.DoScheduleRead();
                }
                else
                {
                    ch.EventLoop.Execute(p => ((NativeChannel)p).DoScheduleRead(), ch);
                }
            }
        }
    }

    interface INativeUnsafe
    {
        IntPtr UnsafeHandle { get; }

        void FinishConnect(ConnectRequest request);

        ReadOperation PrepareRead();

        void FinishRead(ReadOperation readOperation);

        void FinishWrite(WriteRequest writeRequest);
    }
    
    interface IServerNativeUnsafe
    {
        void Accept(RemoteConnection connection);

        void Accept(NativeHandle handle);
    }
}
