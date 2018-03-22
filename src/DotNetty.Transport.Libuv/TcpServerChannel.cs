// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    using TcpListener = Native.TcpListener;

    public sealed class TcpServerChannel : NativeChannel, IServerChannel
    {
        static readonly ChannelMetadata TcpServerMetadata = new ChannelMetadata(false);

        readonly TcpServerChannelConfig config;
        TcpListener tcpListener;
        bool isBound;

        public TcpServerChannel() : base(null)
        {
            this.config = new TcpServerChannelConfig(this);
        }

        public override IChannelConfiguration Configuration => this.config;

        public override ChannelMetadata Metadata => TcpServerMetadata;

        protected override EndPoint LocalAddressInternal => this.tcpListener?.GetLocalEndPoint();

        protected override EndPoint RemoteAddressInternal => null;

        internal bool IsBound => this.isBound;

        protected override void DoBind(EndPoint localAddress)
        {
            if (!this.Open)
            {
                return;
            }

            Debug.Assert(this.EventLoop.InEventLoop);
            if (!this.IsInState(StateFlags.Active))
            {
                var address = (IPEndPoint)localAddress;
                var loopExecutor = (LoopExecutor)this.EventLoop;

                uint flags = PlatformApi.GetAddressFamily(address.AddressFamily);
                this.tcpListener = new TcpListener(loopExecutor.UnsafeLoop, flags);

                // Apply the configuration right after the tcp handle is created
                // because SO_REUSEPORT cannot be configured after bind
                this.config.Apply();

                this.tcpListener.Bind(address);
                this.isBound = true;

                this.tcpListener.Listen((TcpServerChannelUnsafe)this.Unsafe, this.config.Backlog);

                this.CacheLocalAddress();
                this.SetState(StateFlags.Active);
            }
        }

        protected override IChannelUnsafe NewUnsafe() => new TcpServerChannelUnsafe(this);

        internal override NativeHandle GetHandle()
        {
            if (this.tcpListener == null)
            {
                throw new InvalidOperationException("tcpListener handle not intialized");
            }

            return this.tcpListener;
        }

        protected override void DoClose()
        {
            if (this.TryResetState(StateFlags.Open | StateFlags.Active))
            {
                this.tcpListener?.CloseHandle();
                this.tcpListener = null;
            }
        }

        protected override void DoBeginRead()
        {
            if (!this.Open)
            {
                return;
            }

            if (!this.IsInState(StateFlags.ReadScheduled))
            {
                if (this.EventLoop is DispatcherEventLoop dispatcher)
                {
                    // Set up the dispatcher callback, all dispatched handles 
                    // need to call Accept on this channel to setup pipeline
                    dispatcher.Register((TcpServerChannelUnsafe)this.Unsafe);
                }
                this.SetState(StateFlags.ReadScheduled);
            }
        }

        internal interface IServerNativeUnsafe
        {
            void Accept(RemoteConnection connection);

            void Accept(NativeHandle handle);
        }

        sealed class TcpServerChannelUnsafe : NativeChannelUnsafe, IServerNativeUnsafe
        {
            static readonly Action<object, object> AcceptAction = (u, e) => ((TcpServerChannelUnsafe)u).Accept((Tcp)e);

            public TcpServerChannelUnsafe(TcpServerChannel channel) : base(channel)
            {
            }

            public override IntPtr UnsafeHandle => ((TcpServerChannel)this.channel).tcpListener.Handle;

            // Connection callback from Libuv thread
            void IServerNativeUnsafe.Accept(RemoteConnection connection)
            {
                var ch = (TcpServerChannel)this.channel;
                NativeHandle client = connection.Client;

                // If the AutoRead is false, reject the connection
                if (!ch.config.AutoRead || connection.Error != null)
                {
                    if (connection.Error != null)
                    {
                        Logger.Info("Accept client connection failed.", connection.Error);
                    }
                    try
                    {
                        client?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Failed to dispose a client connection.", ex);
                    }
                    finally
                    {
                        client = null;
                    }
                }
                if (client == null)
                {
                    return;
                }

                if (ch.EventLoop is DispatcherEventLoop dispatcher)
                {
                    // Dispatch handle to other Libuv loop/thread
                    dispatcher.Dispatch(client);
                }
                else
                {
                    this.Accept((Tcp)client);
                }
            }

            // Called from other Libuv loop/thread received tcp handle from pipe
            void IServerNativeUnsafe.Accept(NativeHandle handle)
            {
                var ch = (TcpServerChannel)this.channel;
                if (ch.EventLoop.InEventLoop)
                {
                    this.Accept((Tcp)handle);
                }
                else
                {
                    this.channel.EventLoop.Execute(AcceptAction, this, handle);
                }
            }

            void Accept(Tcp tcp)
            {
                var ch = (TcpServerChannel)this.channel;
                IChannelPipeline pipeline = ch.Pipeline;
                IRecvByteBufAllocatorHandle allocHandle = this.RecvBufAllocHandle;

                bool closed = false;
                Exception exception = null;
                try
                {
                    var tcpChannel = new TcpChannel(ch, tcp);
                    ch.Pipeline.FireChannelRead(tcpChannel);
                    allocHandle.IncMessagesRead(1);
                }
                catch (ObjectDisposedException)
                {
                    closed = true;
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                allocHandle.ReadComplete();
                pipeline.FireChannelReadComplete();

                if (exception != null)
                {
                    pipeline.FireExceptionCaught(exception);
                }

                if (closed && ch.Open)
                {
                    this.CloseSafe();
                }
            }
        }

        protected override void DoDisconnect() => throw new NotSupportedException($"{nameof(TcpServerChannel)}");

        protected override void DoStopRead() => throw new NotSupportedException($"{nameof(TcpServerChannel)}");

        protected override void DoWrite(ChannelOutboundBuffer input) => throw new NotSupportedException($"{nameof(TcpServerChannel)}");
    }
}
