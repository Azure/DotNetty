// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;

    /// <summary>
    ///  {@link io.netty.channel.socket.SocketChannel} which uses NIO selector based implementation.
    /// </summary>
    public class TcpSocketChannel : AbstractSocketByteChannel, ISocketChannel
    {
        readonly ISocketChannelConfiguration config;

        /// <summary>
        ///  Create a new instance
        /// </summary>
        public TcpSocketChannel()
            : this(new Socket(SocketType.Stream, ProtocolType.Tcp))
        {
        }

        /// <summary>
        ///  Create a new instance
        /// </summary>
        public TcpSocketChannel(AddressFamily addressFamily)
            : this(new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp))
        {
        }

        /// <summary>
        ///  Create a new instance using the given {@link SocketChannel}.
        /// </summary>
        public TcpSocketChannel(Socket socket)
            : this(null, socket)
        {
        }

        /// <summary>
        ///  Create a new instance
        /// 
        ///  @param parent    the {@link Channel} which created this instance or {@code null} if it was created by the user
        ///  @param socket    the {@link SocketChannel} which will be used
        /// </summary>
        public TcpSocketChannel(IChannel parent, Socket socket)
            : this(parent, socket, false)
        {
        }

        internal TcpSocketChannel(IChannel parent, Socket socket, bool connected)
            : base(parent, socket)
        {
            this.config = new TcpSocketChannelConfig(this, socket);
            if (connected)
            {
                this.OnConnected();
            }
        }

        //public new IServerSocketChannel Parent
        //{
        //    get { return (IServerSocketChannel)base.Parent; }
        //}

        public override bool DisconnectSupported
        {
            get { return false; }
        }

        public override IChannelConfiguration Configuration
        {
            get { return this.config; }
        }

        protected override EndPoint LocalAddressInternal
        {
            get { return this.Socket.LocalEndPoint; }
        }

        protected override EndPoint RemoteAddressInternal
        {
            get { return this.Socket.RemoteEndPoint; }
        }

        public bool IsOutputShutdown
        {
            get { throw new NotImplementedException(); } // todo: impl with stateflags
        }

        public Task ShutdownOutputAsync()
        {
            var tcs = new TaskCompletionSource();
            // todo: use closeExecutor if available
            //Executor closeExecutor = ((TcpSocketChannelUnsafe) unsafe()).closeExecutor();
            //if (closeExecutor != null) {
            //    closeExecutor.execute(new OneTimeTask() {

            //        public void run() {
            //            shutdownOutput0(promise);
            //        }
            //    });
            //} else {
            IEventLoop loop = this.EventLoop;
            if (loop.InEventLoop)
            {
                this.ShutdownOutput0(tcs);
            }
            else
            {
                loop.Execute(promise => this.ShutdownOutput0((TaskCompletionSource)promise), tcs);
            }
            //}
            return tcs.Task;
        }

        void ShutdownOutput0(TaskCompletionSource promise)
        {
            try
            {
                this.Socket.Shutdown(SocketShutdown.Send);
                promise.Complete();
            }
            catch (Exception ex)
            {
                promise.SetException(ex);
            }
        }

        protected override void DoBind(EndPoint localAddress)
        {
            this.Socket.Bind(localAddress);
        }

        protected override bool DoConnect(EndPoint remoteAddress, EndPoint localAddress)
        {
            if (localAddress != null)
            {
                this.Socket.Bind(localAddress);
            }

            bool success = false;
            try
            {
                var eventPayload = new SocketChannelAsyncOperation(this, false);
                eventPayload.RemoteEndPoint = remoteAddress;
                bool connected = !this.Socket.ConnectAsync(eventPayload);
                success = true;
                return connected;
            }
            finally
            {
                if (!success)
                {
                    this.DoClose();
                }
            }
        }

        protected override void DoFinishConnect(SocketChannelAsyncOperation operation)
        {
            try
            {
                operation.Validate();
            }
            finally
            {
                operation.Dispose();
            }
            this.OnConnected();
        }

        void OnConnected()
        {
            this.SetState(StateFlags.Active);

            // preserve local and remote addresses for later availability even if Socket fails
            this.CacheLocalAddress();
            this.CacheRemoteAddress();
        }

        protected override void DoDisconnect()
        {
            this.DoClose();
        }

        protected override void DoClose()
        {
            base.DoClose();
            if (this.ResetState(StateFlags.Open | StateFlags.Active))
            {
                this.Socket.Shutdown(SocketShutdown.Both);
                this.Socket.Close(0);
            }
        }

        protected override int DoReadBytes(IByteBuffer byteBuf)
        {
            if (!byteBuf.HasArray)
            {
                throw new NotImplementedException("Only IByteBuffer implementations backed by array are supported.");
            }

            SocketError errorCode;
            int received = this.Socket.Receive(byteBuf.Array, byteBuf.ArrayOffset + byteBuf.WriterIndex, byteBuf.WritableBytes, SocketFlags.None, out errorCode);

            switch (errorCode)
            {
                case SocketError.Success:
                    if (received == 0)
                    {
                        return -1; // indicate that socket was closed
                    }
                    break;
                case SocketError.WouldBlock:
                    if (received == 0)
                    {
                        return 0;
                    }
                    break;
                default:
                    throw new SocketException((int)errorCode);
            }

            byteBuf.SetWriterIndex(byteBuf.WriterIndex + received);

            return received;
        }

        protected override int DoWriteBytes(IByteBuffer buf)
        {
            if (!buf.HasArray)
            {
                throw new NotImplementedException("Only IByteBuffer implementations backed by array are supported.");
            }

            SocketError errorCode;
            int sent = this.Socket.Send(buf.Array, buf.ArrayOffset + buf.ReaderIndex, buf.ReadableBytes, SocketFlags.None, out errorCode);

            if (errorCode != SocketError.Success && errorCode != SocketError.WouldBlock)
            {
                throw new SocketException((int)errorCode);
            }

            if (sent > 0)
            {
                buf.SetReaderIndex(buf.ReaderIndex + sent);
            }

            return sent;
            //return buf.readBytes(javaChannel(), expectedWrittenBytes);
        }

        //protected long doWriteFileRegion(FileRegion region)
        //{
        //    long position = region.transfered();
        //    return region.transferTo(javaChannel(), position);
        //}

        //protected void doWrite(ChannelOutboundBuffer input)
        //{
        //    for (;;)
        //    {
        //        int size = input.size();
        //        if (size == 0)
        //        {
        //            // All written so clear OP_WRITE
        //            this.clearOpWrite();
        //            break;
        //        }
        //        long writtenBytes = 0;
        //        bool done = false;
        //        bool setOpWrite = false;

        //        // Ensure the pending writes are made of ByteBufs only.
        //        IByteBuffer[] nioBuffers = input.nioBuffers();
        //        int nioBufferCnt = input.nioBufferCount();
        //        long expectedWrittenBytes = input.nioBufferSize();
        //        SocketChannel ch = javaChannel();

        //        // Always us nioBuffers() to workaround data-corruption.
        //        // See https://github.com/netty/netty/issues/2761
        //        switch (nioBufferCnt)
        //        {
        //            case 0:
        //                // We have something else beside ByteBuffers to write so fallback to normal writes.
        //                base.doWrite(input);
        //                return;
        //            case 1:
        //                // Only one ByteBuf so use non-gathering write
        //                ByteBuffer nioBuffer = nioBuffers[0];
        //                for (int i = config().getWriteSpinCount() - 1; i >= 0; i --)
        //                {
        //                    int localWrittenBytes = ch.write(nioBuffer);
        //                    if (localWrittenBytes == 0)
        //                    {
        //                        setOpWrite = true;
        //                        break;
        //                    }
        //                    expectedWrittenBytes -= localWrittenBytes;
        //                    writtenBytes += localWrittenBytes;
        //                    if (expectedWrittenBytes == 0)
        //                    {
        //                        done = true;
        //                        break;
        //                    }
        //                }
        //                break;
        //            default:
        //                for (int i = config().getWriteSpinCount() - 1; i >= 0; i --)
        //                {
        //                    long localWrittenBytes = ch.write(nioBuffers, 0, nioBufferCnt);
        //                    if (localWrittenBytes == 0)
        //                    {
        //                        setOpWrite = true;
        //                        break;
        //                    }
        //                    expectedWrittenBytes -= localWrittenBytes;
        //                    writtenBytes += localWrittenBytes;
        //                    if (expectedWrittenBytes == 0)
        //                    {
        //                        done = true;
        //                        break;
        //                    }
        //                }
        //                break;
        //        }

        //        // Release the fully written buffers, and update the indexes of the partially written buffer.
        //        input.removeBytes(writtenBytes);

        //        if (!done)
        //        {
        //            // Did not write all buffers completely.
        //            incompleteWrite(setOpWrite);
        //            break;
        //        }
        //    }
        //}

        protected override IChannelUnsafe NewUnsafe()
        {
            return new TcpSocketChannelUnsafe(this);
        }

        sealed class TcpSocketChannelUnsafe : SocketByteChannelUnsafe
        {
            public TcpSocketChannelUnsafe(TcpSocketChannel channel)
                : base(channel)
            {
            }

            // todo: review
            //protected Executor closeExecutor()
            //{
            //    if (javaChannel().isOpen() && config().getSoLinger() > 0)
            //    {
            //        return GlobalEventExecutor.INSTANCE;
            //    }
            //    return null;
            //}
        }

        sealed class TcpSocketChannelConfig : DefaultSocketChannelConfiguration
        {
            public TcpSocketChannelConfig(TcpSocketChannel channel, Socket javaSocket)
                : base(channel, javaSocket)
            {
            }

            protected override void AutoReadCleared()
            {
                ((TcpSocketChannel)this.Channel).ReadPending = false;
            }
        }
    }
}