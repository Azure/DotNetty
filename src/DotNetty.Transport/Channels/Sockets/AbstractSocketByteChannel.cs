// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Net.Sockets;
    using DotNetty.Buffers;

    /// <summary>
    /// {@link AbstractNioChannel} base class for {@link Channel}s that operate on bytes.
    /// </summary>
    public abstract class AbstractSocketByteChannel : AbstractSocketChannel
    {
        static readonly string ExpectedTypes =
            string.Format(" (expected: {0})", typeof(IByteBuffer).Name); //+ ", " +

        // todo: FileRegion support        
        //typeof(FileRegion).Name + ')';

        static readonly Action<object> FlushAction = _ => ((AbstractSocketByteChannel)_).Flush();
        static readonly Action<object, object> ReadCompletedSyncCallback = OnReadCompletedSync;

        /// <summary>
        /// Create a new instance
        ///
        /// @param parent            the parent {@link Channel} by which this instance was created. May be {@code null}
        /// @param ch                the underlying {@link SelectableChannel} on which it operates
        /// </summary>
        protected AbstractSocketByteChannel(IChannel parent, Socket socket)
            : base(parent, socket)
        {
        }

        protected override IChannelUnsafe NewUnsafe()
        {
            return new SocketByteChannelUnsafe(this);
        }

        protected class SocketByteChannelUnsafe : AbstractSocketUnsafe
        {
            public SocketByteChannelUnsafe(AbstractSocketByteChannel channel)
                : base(channel)
            {
            }

            new AbstractSocketByteChannel Channel
            {
                get { return (AbstractSocketByteChannel)this.channel; }
            }

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
                    this.CloseAsync();
                    //}
                }
            }

            void HandleReadException(IChannelPipeline pipeline, IByteBuffer byteBuf, Exception cause, bool close)
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
                ch.ResetState(StateFlags.ReadScheduled);
                IChannelConfiguration config = ch.Configuration;
                if (!config.AutoRead && !ch.ReadPending)
                {
                    // ChannelConfig.setAutoRead(false) was called in the meantime
                    //removeReadOp(); -- noop with IOCP, just don't schedule receive again
                    return;
                }

                IChannelPipeline pipeline = ch.Pipeline;
                IByteBufferAllocator allocator = config.Allocator;
                int maxMessagesPerRead = config.MaxMessagesPerRead;
                IRecvByteBufAllocatorHandle allocHandle = this.RecvBufAllocHandle;

                IByteBuffer byteBuf = null;
                int messages = 0;
                bool close = false;
                try
                {
                    operation.Validate();

                    int totalReadAmount = 0;
                    bool readPendingReset = false;
                    do
                    {
                        byteBuf = allocHandle.Allocate(allocator);
                        int writable = byteBuf.WritableBytes;
                        int localReadAmount = ch.DoReadBytes(byteBuf);
                        if (localReadAmount <= 0)
                        {
                            // not was read release the buffer
                            byteBuf.Release();
                            byteBuf = null;
                            close = localReadAmount < 0;
                            break;
                        }
                        if (!readPendingReset)
                        {
                            readPendingReset = true;
                            ch.ReadPending = false;
                        }
                        pipeline.FireChannelRead(byteBuf);
                        byteBuf = null;

                        if (totalReadAmount >= int.MaxValue - localReadAmount)
                        {
                            // Avoid overflow.
                            totalReadAmount = int.MaxValue;
                            break;
                        }

                        totalReadAmount += localReadAmount;

                        // stop reading
                        if (!config.AutoRead)
                        {
                            break;
                        }

                        if (localReadAmount < writable)
                        {
                            // Read less than what the buffer can hold,
                            // which might mean we drained the recv buffer completely.
                            break;
                        }
                    }
                    while (++messages < maxMessagesPerRead);

                    pipeline.FireChannelReadComplete();
                    allocHandle.Record(totalReadAmount);

                    if (close)
                    {
                        this.CloseOnRead();
                        close = false;
                    }
                }
                catch (Exception t)
                {
                    this.HandleReadException(pipeline, byteBuf, t, close);
                }
                finally
                {
                    // Check if there is a readPending which was not processed yet.
                    // This could be for two reasons:
                    // /// The user called Channel.read() or ChannelHandlerContext.read() input channelRead(...) method
                    // /// The user called Channel.read() or ChannelHandlerContext.read() input channelReadComplete(...) method
                    //
                    // See https://github.com/netty/netty/issues/2254
                    if (!close && (config.AutoRead || ch.ReadPending))
                    {
                        ch.DoBeginRead();
                    }
                }
            }
        }

        protected override void ScheduleSocketRead()
        {
            SocketChannelAsyncOperation operation = this.ReadOperation;
            bool pending = this.Socket.ReceiveAsync(operation);
            if (!pending)
            {
                // todo: potential allocation / non-static field?
                this.EventLoop.Execute(ReadCompletedSyncCallback, this.Unsafe, operation);
            }
        }

        static void OnReadCompletedSync(object u, object e)
        {
            ((ISocketChannelUnsafe)u).FinishRead((SocketChannelAsyncOperation)e);
        }

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
                        this.IncompleteWrite(scheduleAsync, buf);
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

        protected void IncompleteWrite(bool scheduleAsync, IByteBuffer buffer)
        {
            // Did not write completely.
            if (scheduleAsync)
            {
                SocketChannelAsyncOperation operation = this.PrepareWriteOperation(buffer);

                this.SetState(StateFlags.WriteScheduled);
                bool pending = this.Socket.SendAsync(operation);
                if (!pending)
                {
                    ((ISocketChannelUnsafe)this.Unsafe).FinishWrite(operation);
                }
            }
            else
            {
                // Schedule flush again later so other tasks can be picked up input the meantime
                this.EventLoop.Execute(FlushAction, this);
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
        /// Read bytes into the given {@link ByteBuf} and return the amount.
        /// </summary>
        protected abstract int DoReadBytes(IByteBuffer buf);

        /// <summary>
        /// Write bytes form the given {@link ByteBuf} to the underlying {@link java.nio.channels.Channel}.
        /// @param buf           the {@link ByteBuf} from which the bytes should be written
        /// @return amount       the amount of written bytes
        /// </summary>
        protected abstract int DoWriteBytes(IByteBuffer buf);
    }
}