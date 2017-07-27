// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    public sealed class TcpServerChannel : NativeChannel, IServerChannel
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<TcpServerChannel>();
        static readonly ChannelMetadata TcpServerMetadata = new ChannelMetadata(false, 16);

        readonly TcpServerChannelConfig config;
        TcpListener tcpListener;

        public TcpServerChannel() : base(null)
        {
            this.config = new TcpServerChannelConfig(this);
        }

        public override IChannelConfiguration Configuration => this.config;

        public override ChannelMetadata Metadata => TcpServerMetadata;

        protected override EndPoint LocalAddressInternal => this.tcpListener?.GetLocalEndPoint();

        protected override EndPoint RemoteAddressInternal => null;

        protected override void DoBind(EndPoint localAddress)
        {
            if (!this.Open)
            {
                return;
            }

            if (!this.IsInState(StateFlags.Active))
            {
                this.tcpListener.Listen((IPEndPoint)localAddress, (TcpServerChannelUnsafe)this.Unsafe, this.config.Backlog);
                this.CacheLocalAddress();
                this.SetState(StateFlags.Active);
            }
        }

        protected override IChannelUnsafe NewUnsafe() => new TcpServerChannelUnsafe(this);

        protected override void DoRegister()
        {
            Debug.Assert(this.tcpListener == null);

            var loopExecutor = (ILoopExecutor)this.EventLoop;
            Loop loop = loopExecutor.UnsafeLoop;

            this.tcpListener = new TcpListener(loop);
            this.config.SetOptions(this.tcpListener);

            var dispatcher = loopExecutor as DispatcherEventLoop;
            dispatcher?.Register((TcpServerChannelUnsafe)this.Unsafe);
        }

        internal override unsafe IntPtr GetLoopHandle()
        {
            if (this.tcpListener == null)
            {
                throw new InvalidOperationException("tcpListener handle not intialized");
            }

            return ((uv_stream_t*)this.tcpListener.Handle)->loop;
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
                this.SetState(StateFlags.ReadScheduled);
            }
        }

        sealed class TcpServerChannelUnsafe : NativeChannelUnsafe, IServerNativeUnsafe
        {
            public TcpServerChannelUnsafe(TcpServerChannel channel) : base(channel)
            {
            }

            public override IntPtr UnsafeHandle => ((TcpServerChannel)this.channel).tcpListener.Handle;

            void IServerNativeUnsafe.Accept(RemoteConnection connection)
            {
                AbstractChannel ch = this.channel;
                if (ch.EventLoop.InEventLoop)
                {
                    this.Accept(connection);
                }
                else
                {
                    ch.EventLoop.Execute(AcceptCallbackAction, this, connection);
                }
            }

            static readonly Action<object, object> AcceptCallbackAction = (u, e) => ((TcpServerChannelUnsafe)u).Accept((RemoteConnection)e);

            void Accept(RemoteConnection connection)
            {
                var ch = (TcpServerChannel)this.channel;
                NativeHandle client = connection.Client;

                if (connection.Error != null)
                {
                    Logger.Warn("Client connection failed.", connection.Error);
                    try
                    {
                        client?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Failed to dispose a client connection.", ex);
                    }

                    return;
                }

                if (client == null)
                {
                    return;
                }

                if (ch.EventLoop is DispatcherEventLoop dispatcher)
                {
                    dispatcher.Dispatch(client);
                }
                else
                {
                    this.Accept((Tcp)client);
                }
            }

            void IServerNativeUnsafe.Accept(NativeHandle handle)
            {
                this.Accept((Tcp)handle);
            }

            void Accept(Tcp tcp)
            {
                var ch = (TcpServerChannel)this.channel;
                var tcpChannel = new TcpChannel(ch, tcp);
                ch.Pipeline.FireChannelRead(tcpChannel);
                ch.Pipeline.FireChannelReadComplete();
            }
        }

        protected override void DoDisconnect()
        {
            throw new NotSupportedException();
        }

        protected override void DoScheduleRead()
        {
            throw new NotSupportedException();
        }

        protected override void DoWrite(ChannelOutboundBuffer input)
        {
            throw new NotSupportedException();
        }
    }
}
