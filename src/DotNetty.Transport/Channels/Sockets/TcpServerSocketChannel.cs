// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Net.Sockets;
    using DotNetty.Common.Internal.Logging;

    /// <summary>
    /// A {@link io.netty.channel.socket.ServerSocketChannel} implementation which uses
    /// NIO selector based implementation to accept new connections.
    /// </summary>
    public class TcpServerSocketChannel : AbstractSocketChannel, IServerSocketChannel
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<TcpServerSocketChannel>();

        static readonly Action<object, object> ReadCompletedSyncCallback = OnReadCompletedSync;

        readonly IServerSocketChannelConfiguration config;

        SocketChannelAsyncOperation acceptOperation;

        /// <summary>
        /// Create a new instance
        /// </summary>
        public TcpServerSocketChannel()
            : this(new Socket(SocketType.Stream, ProtocolType.Tcp))
        {
        }

        /// <summary>
        /// Create a new instance
        /// </summary>
        public TcpServerSocketChannel(AddressFamily addressFamily)
            : this(new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp))
        {
        }

        /// <summary>
        /// Create a new instance using the given {@link Socket}.
        /// </summary>
        public TcpServerSocketChannel(Socket socket)
            : base(null, socket)
        {
            this.config = new TcpServerSocketChannelConfig(this, socket);
        }

        public override IChannelConfiguration Configuration
        {
            get { return this.config; }
        }

        public override bool Active
        {
            get { return this.Socket.IsBound; }
        }

        protected override EndPoint RemoteAddressInternal
        {
            get { return null; }
        }

        protected override EndPoint LocalAddressInternal
        {
            get { return this.Socket.LocalEndPoint; }
        }

        public override bool DisconnectSupported
        {
            get { return false; }
        }

        SocketChannelAsyncOperation AcceptOperation
        {
            get { return this.acceptOperation ?? (this.acceptOperation = new SocketChannelAsyncOperation(this, false)); }
        }

        protected override IChannelUnsafe NewUnsafe()
        {
            return new TcpServerSocketChannelUnsafe(this);
        }

        protected override void DoBind(EndPoint localAddress)
        {
            this.Socket.Bind(localAddress);
            this.Socket.Listen(this.config.Backlog);

            this.CacheLocalAddress();
        }

        protected override void DoClose()
        {
            if (this.ResetState(StateFlags.Open))
            {
                this.Socket.Close(0);
            }
        }

        protected override void ScheduleSocketRead()
        {
            SocketChannelAsyncOperation operation = this.AcceptOperation;
            bool pending = this.Socket.AcceptAsync(operation);
            if (!pending)
            {
                this.EventLoop.Execute(ReadCompletedSyncCallback, this.Unsafe, operation);
            }
        }

        static void OnReadCompletedSync(object u, object p)
        {
            ((ISocketChannelUnsafe)u).FinishRead((SocketChannelAsyncOperation)p);
        }

        protected override bool DoConnect(EndPoint remoteAddress, EndPoint localAddress)
        {
            throw new NotSupportedException();
        }

        protected override void DoFinishConnect(SocketChannelAsyncOperation operation)
        {
            throw new NotSupportedException();
        }

        protected override void DoDisconnect()
        {
            throw new NotSupportedException();
        }

        protected override void DoWrite(ChannelOutboundBuffer input)
        {
            throw new NotSupportedException();
        }

        protected override sealed object FilterOutboundMessage(object msg)
        {
            throw new NotSupportedException();
        }

        sealed class TcpServerSocketChannelUnsafe : AbstractSocketUnsafe
        {
            public TcpServerSocketChannelUnsafe(TcpServerSocketChannel channel)
                : base(channel)
            {
            }

            new TcpServerSocketChannel Channel
            {
                get { return (TcpServerSocketChannel)this.channel; }
            }

            public override void FinishRead(SocketChannelAsyncOperation operation)
            {
                Contract.Requires(this.channel.EventLoop.InEventLoop);

                TcpServerSocketChannel ch = this.Channel;
                ch.ResetState(StateFlags.ReadScheduled);
                IChannelConfiguration config = ch.Configuration;

                int maxMessagesPerRead = config.MaxMessagesPerRead;
                IChannelPipeline pipeline = ch.Pipeline;
                bool closed = false;
                Exception exception = null;

                int messageCount = 0;

                try
                {
                    Socket connectedSocket = null;
                    try
                    {
                        connectedSocket = operation.AcceptSocket;
                        operation.Validate();
                        operation.AcceptSocket = null;

                        var message = new TcpSocketChannel(ch, connectedSocket, true);
                        ch.ReadPending = false;
                        pipeline.FireChannelRead(message);
                        messageCount++;

                        if (!config.AutoRead && !ch.ReadPending)
                        {
                            // ChannelConfig.setAutoRead(false) was called in the meantime.
                            // Completed Accept has to be processed though.
                            return;
                        }

                        while (messageCount < maxMessagesPerRead)
                        {
                            connectedSocket = null;
                            connectedSocket = ch.Socket.Accept();
                            message = new TcpSocketChannel(ch, connectedSocket, true);
                            pipeline.FireChannelRead(message);

                            // stop reading and remove op
                            if (!config.AutoRead)
                            {
                                break;
                            }
                            messageCount++;
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        closed = true;
                    }
                    catch (Exception ex)
                    {
                        var asSocketException = ex as SocketException;
                        if (asSocketException == null || asSocketException.SocketErrorCode != SocketError.WouldBlock)
                        {
                            Logger.Warn("Failed to create a new channel from an accepted socket.", ex);
                            if (connectedSocket != null)
                            {
                                try
                                {
                                    connectedSocket.Close();
                                }
                                catch (Exception ex2)
                                {
                                    Logger.Warn("Failed to close a socket.", ex2);
                                }
                            }
                            exception = ex;
                        }
                    }

                    pipeline.FireChannelReadComplete();

                    if (exception != null)
                    {
                        // ServerChannel should not be closed even on SocketException because it can often continue
                        // accepting incoming connections. (e.g. too many open files)

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
                    if (!closed && (config.AutoRead || ch.ReadPending))
                    {
                        ch.DoBeginRead();
                    }
                }
            }
        }

        sealed class TcpServerSocketChannelConfig : DefaultServerSocketChannelConfig
        {
            public TcpServerSocketChannelConfig(TcpServerSocketChannel channel, Socket javaSocket)
                : base(channel, javaSocket)
            {
            }

            protected override void AutoReadCleared()
            {
                ((TcpServerSocketChannel)this.Channel).ReadPending = false;
            }
        }
    }
}