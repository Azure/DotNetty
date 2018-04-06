// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Net.Sockets;
    using System.Threading;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// <see cref="AbstractSocketChannel"/> base class for <see cref="IChannel"/>s that operate on bytes.
    /// </summary>
    public abstract class AbstractSocketByteChannel : AbstractSocketChannel
    {
        static readonly string ExpectedTypes =
            $" (expected: {StringUtil.SimpleClassName<IByteBuffer>()})"; //+ ", " +

        // todo: FileRegion support        
        //typeof(FileRegion).Name + ')';

        static readonly Action<object> FlushAction = _ => ((AbstractSocketByteChannel)_).Flush();
        static readonly Action<object, object> ReadCompletedSyncCallback = OnReadCompletedSync;

        /// <summary>Create a new instance</summary>
        /// <param name="parent">the parent <see cref="IChannel"/> by which this instance was created. May be <c>null</c></param>
        /// <param name="socket">the underlying <see cref="Socket"/> on which it operates</param>
        protected AbstractSocketByteChannel(IChannel parent, Socket socket)
            : base(parent, socket)
        {
        }

        protected override IChannelUnsafe NewUnsafe() => new SocketByteChannelUnsafe(this);

        protected class SocketByteChannelUnsafe : AbstractSocketUnsafe
        {
            public SocketByteChannelUnsafe(AbstractSocketByteChannel channel)
                : base(channel)
            {
            }

            new AbstractSocketByteChannel Channel => (AbstractSocketByteChannel)this.channel;

            void CloseOnRead()
            {
                this.Channel.ShutdownInput();
                if (this.channel.Open)
                {
                    // todo: support half-closure
                    //if (bool.TrueString.Equals(this.channel.Configuration.getOption(ChannelOption.ALLOW_HALF_CLOSURE))) {
                    //    key.interestOps(key.interestOps() & ~readInterestOp);
                    //    this.channel.Pipeline.FireUserEventTriggered(ChannelInputShutdownEvent.INSTANCE);
                    //} else {
                    this.CloseSafe();
                    //}
                }
            }

            void HandleReadException(IChannelPipeline pipeline, IByteBuffer byteBuf, Exception cause, bool close,
                IRecvByteBufAllocatorHandle allocHandle)
            {
                if (byteBuf != null)
                {
                    if (byteBuf.IsReadable())
                    {
                        this.Channel.ReadPending = false;
                        pipeline.FireChannelRead(byteBuf);
                    }
                    else
                    {
                        byteBuf.Release();
                    }
                }
                allocHandle.ReadComplete();
                pipeline.FireChannelReadComplete();
                pipeline.FireExceptionCaught(cause);
                if (close || cause is SocketException)
                {
                    this.CloseOnRead();
                }
            }

            public override void FinishRead(SocketChannelAsyncOperation operation)
            {
                AbstractSocketByteChannel ch = this.Channel;
                if ((ch.ResetState(StateFlags.ReadScheduled) & StateFlags.Active) == 0)
                {
                    return; // read was signaled as a result of channel closure
                }
                IChannelConfiguration config = ch.Configuration;
                IChannelPipeline pipeline = ch.Pipeline;
                IByteBufferAllocator allocator = config.Allocator;
                IRecvByteBufAllocatorHandle allocHandle = this.RecvBufAllocHandle;
                allocHandle.Reset(config);

                IByteBuffer byteBuf = null;
                bool close = false;
                try
                {
                    operation.Validate();

                    do
                    {
                        byteBuf = allocHandle.Allocate(allocator);
                        //int writable = byteBuf.WritableBytes;
                        allocHandle.LastBytesRead = ch.DoReadBytes(byteBuf);
                        if (allocHandle.LastBytesRead <= 0)
                        {
                            // nothing was read -> release the buffer.
                            byteBuf.Release();
                            byteBuf = null;
                            close = allocHandle.LastBytesRead < 0;
                            break;
                        }

                        allocHandle.IncMessagesRead(1);
                        this.Channel.ReadPending = false;

                        pipeline.FireChannelRead(byteBuf);
                        byteBuf = null;
                    }
                    while (allocHandle.ContinueReading());

                    allocHandle.ReadComplete();
                    pipeline.FireChannelReadComplete();

                    if (close)
                    {
                        this.CloseOnRead();
                    }
                }
                catch (Exception t)
                {
                    this.HandleReadException(pipeline, byteBuf, t, close, allocHandle);
                }
                finally
                {
                    // Check if there is a readPending which was not processed yet.
                    // This could be for two reasons:
                    // /// The user called Channel.read() or ChannelHandlerContext.read() input channelRead(...) method
                    // /// The user called Channel.read() or ChannelHandlerContext.read() input channelReadComplete(...) method
                    //
                    // See https://github.com/netty/netty/issues/2254
                    if (!close && (ch.ReadPending || config.AutoRead))
                    {
                        ch.DoBeginRead();
                    }
                }
            }
        }

        protected override void ScheduleSocketRead()
        {
            SocketChannelAsyncOperation operation = this.ReadOperation;
            bool pending;
#if NETSTANDARD1_3
            pending = this.Socket.ReceiveAsync(operation);
#else
            if (ExecutionContext.IsFlowSuppressed())
            {
                pending = this.Socket.ReceiveAsync(operation);
            }
            else
            {
                using (ExecutionContext.SuppressFlow())
                {
                    pending = this.Socket.ReceiveAsync(operation);
                }
            }
#endif
            if (!pending)
            {
                // todo: potential allocation / non-static field?
                this.EventLoop.Execute(ReadCompletedSyncCallback, this.Unsafe, operation);
            }
        }

        static void OnReadCompletedSync(object u, object e) => ((ISocketChannelUnsafe)u).FinishRead((SocketChannelAsyncOperation)e);

        protected override void DoWrite(ChannelOutboundBuffer input)
        {
            int writeSpinCount = -1;

            while (true)
            {
                object msg = input.Current;
                if (msg == null)
                {
                    // Wrote all messages.
                    break;
                }

                if (msg is IByteBuffer)
                {
                    var buf = (IByteBuffer)msg;
                    int readableBytes = buf.ReadableBytes;
                    if (readableBytes == 0)
                    {
                        input.Remove();
                        continue;
                    }

                    bool scheduleAsync = false;
                    bool done = false;
                    long flushedAmount = 0;
                    if (writeSpinCount == -1)
                    {
                        writeSpinCount = this.Configuration.WriteSpinCount;
                    }
                    for (int i = writeSpinCount - 1; i >= 0; i--)
                    {
                        int localFlushedAmount = this.DoWriteBytes(buf);
                        if (localFlushedAmount == 0) // todo: check for "sent less than attempted bytes" to avoid unnecessary extra doWriteBytes call?
                        {
                            scheduleAsync = true;
                            break;
                        }

                        flushedAmount += localFlushedAmount;
                        if (!buf.IsReadable())
                        {
                            done = true;
                            break;
                        }
                    }

                    input.Progress(flushedAmount);

                    if (done)
                    {
                        input.Remove();
                    }
                    else
                    {
                        this.IncompleteWrite(scheduleAsync, this.PrepareWriteOperation(buf.GetIoBuffer()));
                        break;
                    }
                } /*else if (msg is FileRegion) { todo: FileRegion support
                FileRegion region = (FileRegion) msg;
                bool done = region.transfered() >= region.count();
                bool scheduleAsync = false;

                if (!done) {
                    long flushedAmount = 0;
                    if (writeSpinCount == -1) {
                        writeSpinCount = config().getWriteSpinCount();
                    }

                    for (int i = writeSpinCount - 1; i >= 0; i--) {
                        long localFlushedAmount = doWriteFileRegion(region);
                        if (localFlushedAmount == 0) {
                            scheduleAsync = true;
                            break;
                        }

                        flushedAmount += localFlushedAmount;
                        if (region.transfered() >= region.count()) {
                            done = true;
                            break;
                        }
                    }

                    input.progress(flushedAmount);
                }

                if (done) {
                    input.remove();
                } else {
                    incompleteWrite(scheduleAsync);
                    break;
                }
            }*/
                else
                {
                    // Should not reach here.
                    throw new InvalidOperationException();
                }
            }
        }

        protected override object FilterOutboundMessage(object msg)
        {
            if (msg is IByteBuffer)
            {
                return msg;
                //IByteBuffer buf = (IByteBuffer) msg;
                //if (buf.isDirect()) {
                //    return msg;
                //}

                //return newDirectBuffer(buf);
            }

            // todo: FileRegion support
            //if (msg is FileRegion) {
            //    return msg;
            //}

            throw new NotSupportedException(
                "unsupported message type: " + msg.GetType().Name + ExpectedTypes);
        }

        protected bool IncompleteWrite(bool scheduleAsync, SocketChannelAsyncOperation operation)
        {
            // Did not write completely.
            if (scheduleAsync)
            {
                this.SetState(StateFlags.WriteScheduled);
                bool pending;

#if NETSTANDARD1_3
                pending = this.Socket.SendAsync(operation);
#else
                if (ExecutionContext.IsFlowSuppressed())
                {
                    pending = this.Socket.SendAsync(operation);
                }
                else
                {
                    using (ExecutionContext.SuppressFlow())
                    {
                        pending = this.Socket.SendAsync(operation);
                    }
                }
#endif

                if (!pending)
                {
                    ((ISocketChannelUnsafe)this.Unsafe).FinishWrite(operation);
                }

                return pending;
            }
            else
            {
                // Schedule flush again later so other tasks can be picked up input the meantime
                this.EventLoop.Execute(FlushAction, this);

                return true;
            }
        }

        // todo: support FileRegion
        ///// <summary>
        // /// Write a {@link FileRegion}
        // *
        // /// @param region        the {@link FileRegion} from which the bytes should be written
        // /// @return amount       the amount of written bytes
        // /// </summary>
        //protected abstract long doWriteFileRegion(FileRegion region);

        /// <summary>
        /// Reads bytes into the given <see cref="IByteBuffer"/> and returns the number of bytes that were read.
        /// </summary>
        /// <param name="buf">The <see cref="IByteBuffer"/> to read bytes into.</param>
        /// <returns>The number of bytes that were read into the buffer.</returns>
        protected abstract int DoReadBytes(IByteBuffer buf);

        /// <summary>
        /// Writes bytes from the given <see cref="IByteBuffer"/> to the underlying <see cref="IChannel"/>.
        /// </summary>
        /// <param name="buf">The <see cref="IByteBuffer"/> from which the bytes should be written.</param>
        /// <returns>The number of bytes that were written from the buffer.</returns>
        protected abstract int DoWriteBytes(IByteBuffer buf);
    }
}