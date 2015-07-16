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

        protected override IChannelUnsafe NewUnsafe()
        {
            return new SocketMessageUnsafe(this);
        }

        protected class SocketMessageUnsafe : AbstractSocketUnsafe
        {
            readonly List<object> readBuf = new List<object>();

            public SocketMessageUnsafe(AbstractSocketMessageChannel channel)
                : base(channel)
            {
            }

            new AbstractSocketMessageChannel Channel
            {
                get { return (AbstractSocketMessageChannel)this.channel; }
            }

            public override void FinishRead(SocketChannelAsyncOperation operation)
            {
                Contract.Requires(this.channel.EventLoop.InEventLoop);
                AbstractSocketMessageChannel ch = this.Channel;
                ch.ResetState(StateFlags.ReadScheduled);
                IChannelConfiguration config = ch.Configuration;
                if (!config.AutoRead && !ch.ReadPending)
                {
                    // ChannelConfig.setAutoRead(false) was called in the meantime
                    //removeReadOp(); -- noop with IOCP, just don't schedule receive again
                    return;
                }

                int maxMessagesPerRead = config.MaxMessagesPerRead;
                IChannelPipeline pipeline = ch.Pipeline;
                bool closed = false;
                Exception exception = null;
                try
                {
                    try
                    {
                        while (true)
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

                            // stop reading and remove op
                            if (!config.AutoRead)
                            {
                                break;
                            }

                            if (this.readBuf.Count >= maxMessagesPerRead)
                            {
                                break;
                            }
                        }
                    }
                    catch (Exception t)
                    {
                        exception = t;
                    }
                    ch.ReadPending = false;
                    int size = this.readBuf.Count;
                    for (int i = 0; i < size; i++)
                    {
                        pipeline.FireChannelRead(this.readBuf[i]);
                    }

                    this.readBuf.Clear();
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
                    if (!closed && (config.AutoRead || ch.ReadPending))
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

        //protected override void ScheduleMessageWrite(object message)
        //{
        //    // todo: move this impl to datagram channel impl

        //    var buffer = message as IByteBuffer;
        //    if (buffer == null)
        //    {
        //        throw new InvalidOperationException("Message has an unexpected type: " + message.GetType().FullName);
        //    }

        //    var operation = TakeWriteEventFromPool(this, buffer);
        //    operation.EventArgs.RemoteEndPoint = this.RemoteAddress; // todo: get remote address the right way
        //    this.SetState(StateFlags.WriteScheduled);
        //    bool pending = this.Socket.SendToAsync(operation.EventArgs);
        //    if (!pending)
        //    {
        //        ((AbstractSocketChannel.ISocketChannelUnsafe)this.Unsafe).FinishWrite(operation);
        //    }
        //}

        /// <summary>
        /// Returns {@code true} if we should continue the write loop on a write error.
        /// </summary>
        protected virtual bool ContinueOnWriteError
        {
            get { return false; }
        }

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