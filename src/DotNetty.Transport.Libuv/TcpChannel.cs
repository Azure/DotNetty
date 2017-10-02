// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    public sealed class TcpChannel : NativeChannel
    {
        const int DefaultWriteRequestPoolSize = 1024;

        static readonly ChannelMetadata TcpMetadata = new ChannelMetadata(false, 16);
        static readonly ThreadLocalPool<WriteRequest> Recycler = new ThreadLocalPool<WriteRequest>(handle => new WriteRequest(handle), DefaultWriteRequestPoolSize);

        readonly TcpChannelConfig config;
        Tcp tcp;

        public TcpChannel(): this(null, null)
        {
        }

        internal TcpChannel(IChannel parent, Tcp tcp) : base(parent)
        {
            this.config = new TcpChannelConfig(this);
            this.SetState(StateFlags.Open);

            this.tcp = tcp;
            if (this.tcp != null)
            {
                if (this.config.TcpNoDelay)
                {
                    tcp.NoDelay(true);
                }

                this.OnConnected();
            }
        }

        public override IChannelConfiguration Configuration => this.config;

        public override ChannelMetadata Metadata => TcpMetadata;

        protected override EndPoint LocalAddressInternal => this.tcp?.GetLocalEndPoint();

        protected override EndPoint RemoteAddressInternal => this.tcp?.GetPeerEndPoint();

        protected override IChannelUnsafe NewUnsafe() => new TcpChannelUnsafe(this);

        protected override void DoRegister()
        {
            if (this.tcp != null)
            {
                ((TcpChannelUnsafe)this.Unsafe).ScheduleRead();
            }
            else
            {
                var loopExecutor = (ILoopExecutor)this.EventLoop;
                Loop loop = loopExecutor.UnsafeLoop;
                this.CreateHandle(loop);
            }
        }

        internal void CreateHandle(Loop loop)
        {
            Debug.Assert(this.tcp == null);

            this.tcp = new Tcp(loop);
            this.config.SetOptions(this.tcp);
        }

        internal override unsafe IntPtr GetLoopHandle()
        {
            if (this.tcp == null)
            {
                throw new InvalidOperationException("Tcp handle not intialized");
            }

            return ((uv_stream_t*)this.tcp.Handle)->loop;
        }

        protected override void DoBind(EndPoint localAddress)
        {
            this.tcp.Bind((IPEndPoint)localAddress);
            this.CacheLocalAddress();
        }

        protected override void DoDisconnect() => this.DoClose();

        protected override void DoClose()
        {
            if (this.TryResetState(StateFlags.Open | StateFlags.Active))
            {
                this.tcp?.ReadStop();
                this.tcp?.CloseHandle();
                this.tcp = null;
            }
        }

        protected override void DoBeginRead()
        {
            if (!this.Open || this.IsInState(StateFlags.ReadScheduled))
            {
                return;
            }

            ((TcpChannelUnsafe)this.Unsafe).ScheduleRead();
        }

        protected override void DoScheduleRead()
        {
            if (!this.Open)
            {
                return;
            }

            if (!this.IsInState(StateFlags.ReadScheduled))
            {
                this.SetState(StateFlags.ReadScheduled);
                this.tcp.ReadStart((TcpChannelUnsafe)this.Unsafe);
            }
        }

        protected override void DoWrite(ChannelOutboundBuffer input)
        {
            if (this.EventLoop.InEventLoop)
            {
                this.Write(input);
            }
            else
            {
                this.EventLoop.Execute(WriteAction, this, input);
            }
        }

        static readonly Action<object, object> WriteAction = (u, e) => ((TcpChannel)u).Write((ChannelOutboundBuffer)e);

        void Write(ChannelOutboundBuffer input)
        {
            while (true)
            {
                int size = input.Count;
                if (size == 0)
                {
                    break;
                }

                List<ArraySegment<byte>> nioBuffers = input.GetSharedBufferList();
                int nioBufferCnt = nioBuffers.Count;
                long expectedWrittenBytes = input.NioBufferSize;
                if (nioBufferCnt == 0)
                {
                    this.WriteByteBuffers(input);
                    return;
                }
                else
                {
                    WriteRequest writeRequest = Recycler.Take();
                    writeRequest.Prepare((TcpChannelUnsafe)this.Unsafe, nioBuffers);
                    this.tcp.Write(writeRequest);
                    input.RemoveBytes(expectedWrittenBytes);
                }
            }
        }

        void WriteByteBuffers(ChannelOutboundBuffer input)
        {
            while (true)
            {
                object msg = input.Current;
                if (msg == null)
                {
                    // Wrote all messages.
                    break;
                }

                if (msg is IByteBuffer buf)
                {
                    int readableBytes = buf.ReadableBytes;
                    if (readableBytes == 0)
                    {
                        input.Remove();
                        continue;
                    }

                    var nioBuffers = new List<ArraySegment<byte>>();
                    ArraySegment<byte> nioBuffer = buf.GetIoBuffer();
                    nioBuffers.Add(nioBuffer);
                    WriteRequest writeRequest = Recycler.Take();
                    writeRequest.Prepare((TcpChannelUnsafe)this.Unsafe, nioBuffers);
                    this.tcp.Write(writeRequest);

                    input.Remove();
                }
                else
                {
                    // Should not reach here.
                    throw new InvalidOperationException();
                }
            }
        }

        sealed class TcpChannelUnsafe : NativeChannelUnsafe
        {
            public TcpChannelUnsafe(TcpChannel channel) : base(channel)
            {
            }

            public override IntPtr UnsafeHandle => ((TcpChannel)this.channel).tcp.Handle;
        }
    }
}
