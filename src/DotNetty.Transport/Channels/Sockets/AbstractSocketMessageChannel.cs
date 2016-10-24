// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Net.Sockets;

    /// <summary>
    /// {@link AbstractNioChannel} base class for {@link Channel}s that operate on messages.
    /// </summary>
    public abstract class AbstractSocketMessageChannel : AbstractSocketChannel
    {
        /// <summary>
        /// @see {@link AbstractNioChannel#AbstractNioChannel(Channel, SelectableChannel, int)}
        /// </summary>
        protected AbstractSocketMessageChannel(IChannel parent, Socket socket)
            : base(parent, socket)
        {
        }

        protected override IChannelUnsafe NewUnsafe() => new SocketMessageUnsafe(this);

        protected class SocketMessageUnsafe : AbstractSocketUnsafe
        {
            readonly List<object> readBuf = new List<object>();

            public SocketMessageUnsafe(AbstractSocketMessageChannel channel)
                : base(channel)
            {
            }

            new AbstractSocketMessageChannel Channel => (AbstractSocketMessageChannel)this.channel;

            public override void FinishRead(SocketChannelAsyncOperation operation)
            {
                Contract.Assert(this.channel.EventLoop.InEventLoop);

                AbstractSocketMessageChannel ch = this.Channel;
                if ((ch.ResetState(StateFlags.ReadScheduled) & StateFlags.Active) == 0)
                {
                    return; // read was signaled as a result of channel closure
                }
                IChannelConfiguration config = ch.Configuration;

                IChannelPipeline pipeline = ch.Pipeline;
                IRecvByteBufAllocatorHandle allocHandle = this.Channel.Unsafe.RecvBufAllocHandle;
                allocHandle.Reset(config);

                bool closed = false;
                Exception exception = null;
                try
                {
                    try
                    {
                        do
                        {
                            int localRead = ch.DoReadMessages(this.readBuf);
                            if (localRead == 0)
                            {
                                break;
                            }
                            if (localRead < 0)
                            {
                                closed = true;
                                break;
                            }

                            allocHandle.IncMessagesRead(localRead);
                        }
                        while (allocHandle.ContinueReading());
                    }
                    catch (Exception t)
                    {
                        exception = t;
                    }
                    int size = this.readBuf.Count;
                    for (int i = 0; i < size; i++)
                    {
                        ch.ReadPending = false;
                        pipeline.FireChannelRead(this.readBuf[i]);
                    }

                    this.readBuf.Clear();
                    allocHandle.ReadComplete();
                    pipeline.FireChannelReadComplete();

                    if (exception != null)
                    {
                        var asSocketException = exception as SocketException;
                        if (asSocketException != null && asSocketException.SocketErrorCode != SocketError.TryAgain) // todo: other conditions for not closing message-based socket?
                        {
                            // ServerChannel should not be closed even on SocketException because it can often continue
                            // accepting incoming connections. (e.g. too many open files)
                            closed = !(ch is IServerChannel);
                        }

                        pipeline.FireExceptionCaught(exception);
                    }

                    if (closed)
                    {
                        if (ch.Open)
                        {
                            this.CloseAsync();
                        }
                    }
                }
                finally
                {
                    // Check if there is a readPending which was not processed yet.
                    // This could be for two reasons:
                    // /// The user called Channel.read() or ChannelHandlerContext.read() in channelRead(...) method
                    // /// The user called Channel.read() or ChannelHandlerContext.read() in channelReadComplete(...) method
                    //
                    // See https://github.com/netty/netty/issues/2254
                    if (!closed && (ch.ReadPending || config.AutoRead))
                    {
                        ch.DoBeginRead();
                    }
                }
            }
        }

        protected override void DoWrite(ChannelOutboundBuffer input)
        {
            while (true)
            {
                object msg = input.Current;
                if (msg == null)
                {
                    break;
                }
                try
                {
                    bool done = false;
                    for (int i = this.Configuration.WriteSpinCount - 1; i >= 0; i--)
                    {
                        if (this.DoWriteMessage(msg, input))
                        {
                            done = true;
                            break;
                        }
                    }

                    if (done)
                    {
                        input.Remove();
                    }
                    else
                    {
                        // Did not write all messages.
                        this.ScheduleMessageWrite(msg);
                        break;
                    }
                }
                catch (SocketException e)
                {
                    if (this.ContinueOnWriteError)
                    {
                        input.Remove(e);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        protected abstract void ScheduleMessageWrite(object message);

        /// <summary>
        /// Returns {@code true} if we should continue the write loop on a write error.
        /// </summary>
        protected virtual bool ContinueOnWriteError => false;

        /// <summary>
        /// Read messages into the given array and return the amount which was read.
        /// </summary>
        protected abstract int DoReadMessages(List<object> buf);

        /// <summary>
        /// Write a message to the underlying {@link java.nio.channels.Channel}.
        ///
        /// @return {@code true} if and only if the message has been written
        /// </summary>
        protected abstract bool DoWriteMessage(object msg, ChannelOutboundBuffer input);
    }
}