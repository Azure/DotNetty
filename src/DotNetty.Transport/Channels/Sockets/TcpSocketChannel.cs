// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;

    /// <summary>
    ///     <see cref="ISocketChannel" /> which uses Socket-based implementation.
    /// </summary>
    public class TcpSocketChannel : AbstractSocketByteChannel, ISocketChannel
    {
        static readonly ChannelMetadata METADATA = new ChannelMetadata(false, 16);

        readonly ISocketChannelConfiguration config;

        /// <summary>Create a new instance</summary>
        public TcpSocketChannel()
            : this(new Socket(SocketType.Stream, ProtocolType.Tcp))
        {
        }

        /// <summary>Create a new instance</summary>
        public TcpSocketChannel(AddressFamily addressFamily)
            : this(new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp))
        {
        }

        /// <summary>Create a new instance using the given <see cref="ISocketChannel" />.</summary>
        public TcpSocketChannel(Socket socket)
            : this(null, socket)
        {
        }

        /// <summary>Create a new instance</summary>
        /// <param name="parent">
        ///     the <see cref="IChannel" /> which created this instance or <c>null</c> if it was created by the
        ///     user
        /// </param>
        /// <param name="socket">the <see cref="ISocketChannel" /> which will be used</param>
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

        public override ChannelMetadata Metadata => METADATA;

        public override IChannelConfiguration Configuration => this.config;

        protected override EndPoint LocalAddressInternal => this.Socket.LocalEndPoint;

        protected override EndPoint RemoteAddressInternal => this.Socket.RemoteEndPoint;

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

        protected override void DoBind(EndPoint localAddress) => this.Socket.Bind(localAddress);

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
                if (connected)
                {
                    this.DoFinishConnect(eventPayload);
                }
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

        protected override void DoDisconnect() => this.DoClose();

        protected override void DoClose()
        {
            try
            {
                if (this.TryResetState(StateFlags.Open | StateFlags.Active))
                {
                    this.Socket.Shutdown(SocketShutdown.Both);
                    this.Socket.Dispose();
                }
            }
            finally
            {
                base.DoClose();
            }
        }

        protected override int DoReadBytes(IByteBuffer byteBuf)
        {
            if (!byteBuf.HasArray)
            {
                throw new NotImplementedException("Only IByteBuffer implementations backed by array are supported.");
            }

            if (!this.Socket.Connected)
            {
                return -1; // prevents ObjectDisposedException from being thrown in case connection has been lost in the meantime
            }

            int received = this.Socket.Receive(byteBuf.Array, byteBuf.ArrayOffset + byteBuf.WriterIndex, byteBuf.WritableBytes, SocketFlags.None, out SocketError errorCode);

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

            int sent = this.Socket.Send(buf.Array, buf.ArrayOffset + buf.ReaderIndex, buf.ReadableBytes, SocketFlags.None, out SocketError errorCode);

            if (errorCode != SocketError.Success && errorCode != SocketError.WouldBlock)
            {
                throw new SocketException((int)errorCode);
            }

            if (sent > 0)
            {
                buf.SetReaderIndex(buf.ReaderIndex + sent);
            }

            return sent;
        }

        //protected long doWriteFileRegion(FileRegion region)
        //{
        //    long position = region.transfered();
        //    return region.transferTo(javaChannel(), position);
        //}

        protected override void DoWrite(ChannelOutboundBuffer input)
        {
            List<ArraySegment<byte>> sharedBufferList = null;
            try
            {
                while (true)
                {
                    int size = input.Size;
                    if (size == 0)
                    {
                        // All written
                        break;
                    }
                    long writtenBytes = 0;
                    bool done = false;

                    // Ensure the pending writes are made of ByteBufs only.
                    int maxBytesPerGatheringWrite = ((TcpSocketChannelConfig)this.config).GetMaxBytesPerGatheringWrite();
                    sharedBufferList = input.GetSharedBufferList(1024, maxBytesPerGatheringWrite);
                    int nioBufferCnt = sharedBufferList.Count;
                    long expectedWrittenBytes = input.NioBufferSize;
                    Socket socket = this.Socket;

                    List<ArraySegment<byte>> bufferList = sharedBufferList;
                    // Always us nioBuffers() to workaround data-corruption.
                    // See https://github.com/netty/netty/issues/2761
                    switch (nioBufferCnt)
                    {
                        case 0:
                            // We have something else beside ByteBuffers to write so fallback to normal writes.
                            base.DoWrite(input);
                            return;
                        default:
                            for (int i = this.Configuration.WriteSpinCount - 1; i >= 0; i--)
                            {
                                long localWrittenBytes = socket.Send(bufferList, SocketFlags.None, out SocketError errorCode);
                                if (errorCode != SocketError.Success && errorCode != SocketError.WouldBlock)
                                {
                                    throw new SocketException((int)errorCode);
                                }

                                if (localWrittenBytes == 0)
                                {
                                    break;
                                }

                                expectedWrittenBytes -= localWrittenBytes;
                                writtenBytes += localWrittenBytes;
                                if (expectedWrittenBytes == 0)
                                {
                                    done = true;
                                    break;
                                }
                                else
                                {
                                    bufferList = this.AdjustBufferList(localWrittenBytes, bufferList);
                                }
                            }
                            break;
                    }

                    if (writtenBytes > 0)
                    {
                        // Release the fully written buffers, and update the indexes of the partially written buffer
                        input.RemoveBytes(writtenBytes);
                    }

                    if (!done)
                    {
                        IList<ArraySegment<byte>> asyncBufferList = bufferList;
                        if (object.ReferenceEquals(sharedBufferList, asyncBufferList))
                        {
                            asyncBufferList = sharedBufferList.ToArray(); // move out of shared list that will be reused which could corrupt buffers still pending update
                        }
                        SocketChannelAsyncOperation asyncOperation = this.PrepareWriteOperation(asyncBufferList);

                        // Not all buffers were written out completely
                        if (this.IncompleteWrite(true, asyncOperation))
                        {
                            break;
                        }
                    }
                }
            }
            finally
            {
                // Prepare the list for reuse
                sharedBufferList?.Clear();
            }
        }

        List<ArraySegment<byte>> AdjustBufferList(long localWrittenBytes, List<ArraySegment<byte>> bufferList)
        {
            var adjusted = new List<ArraySegment<byte>>(bufferList.Count);
            foreach (ArraySegment<byte> buffer in bufferList)
            {
                if (localWrittenBytes > 0)
                {
                    long leftBytes = localWrittenBytes - buffer.Count;
                    if (leftBytes < 0)
                    {
                        int offset = buffer.Offset + (int)localWrittenBytes;
                        int count = -(int)leftBytes;
                        adjusted.Add(new ArraySegment<byte>(buffer.Array, offset, count));
                        localWrittenBytes = 0;
                    }
                    else
                    {
                        localWrittenBytes = leftBytes;
                    }
                }
                else
                {
                    adjusted.Add(buffer);
                }
            }
            return adjusted;
        }

        protected override IChannelUnsafe NewUnsafe() => new TcpSocketChannelUnsafe(this);

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
            volatile int maxBytesPerGatheringWrite = int.MaxValue;

            public TcpSocketChannelConfig(TcpSocketChannel channel, Socket javaSocket)
                : base(channel, javaSocket)
            {
                this.CalculateMaxBytesPerGatheringWrite();
            }

            public int GetMaxBytesPerGatheringWrite() => this.maxBytesPerGatheringWrite;

            public override int SendBufferSize
            {
                get => base.SendBufferSize;
                set
                {
                    base.SendBufferSize = value;
                    this.CalculateMaxBytesPerGatheringWrite();
                }
            }

            void CalculateMaxBytesPerGatheringWrite()
            {
                // Multiply by 2 to give some extra space in case the OS can process write data faster than we can provide.
                int newSendBufferSize = this.SendBufferSize << 1;
                if (newSendBufferSize > 0)
                {
                    this.maxBytesPerGatheringWrite = newSendBufferSize;
                }
            }

            protected override void AutoReadCleared() => ((TcpSocketChannel)this.Channel).ClearReadPending();
        }
    }
}