// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    public sealed class TcpChannel : NativeChannel
    {
        static readonly ThreadLocalPool<WriteRequest> Pool = new ThreadLocalPool<WriteRequest>(handle => new WriteRequest(handle));
        static readonly ChannelMetadata TcpMetadata = new ChannelMetadata(false);

        static readonly Action<object> FlushAction = c => ((TcpChannel)c).Flush();

        readonly TcpChannelConfig config;
        Tcp tcp;
        bool isBound;

        public TcpChannel() : this(null, null)
        {
        }

        internal TcpChannel(IChannel parent, Tcp tcp) : base(parent)
        {
            this.config = new TcpChannelConfig(this);
            this.SetState(StateFlags.Open);
            this.tcp = tcp;
        }

        public override IChannelConfiguration Configuration => this.config;

        public override ChannelMetadata Metadata => TcpMetadata;

        protected override EndPoint LocalAddressInternal => this.tcp?.GetLocalEndPoint();

        protected override EndPoint RemoteAddressInternal => this.tcp?.GetPeerEndPoint();

        protected override IChannelUnsafe NewUnsafe() => new TcpChannelUnsafe(this);

        protected override void DoRegister()
        {
            if (this.tcp == null)
            {
                var loopExecutor = (LoopExecutor)this.EventLoop;
                this.tcp = new Tcp(loopExecutor.UnsafeLoop);
            }
            else
            {
                this.OnConnected();
            }
        }

        internal override NativeHandle GetHandle()
        {
            if (this.tcp == null)
            {
                throw new InvalidOperationException("Tcp handle not intialized");
            }
            return this.tcp;
        }

        protected override void DoBind(EndPoint localAddress)
        {
            this.tcp.Bind((IPEndPoint)localAddress);
            this.config.Apply();
            this.isBound = true;
            this.CacheLocalAddress();
        }

        internal bool IsBound => this.isBound;

        protected override void OnConnected()
        {
            if (!this.isBound)
            {
                // Either channel is created by tcp server channel
                // or connect to remote without bind first
                this.config.Apply();
                this.isBound = true;
            }

            base.OnConnected();
        }

        protected override void DoDisconnect() => this.DoClose();

        protected override void DoClose()
        {
            try
            {
                if (this.TryResetState(StateFlags.Open | StateFlags.Active))
                {
                    if (this.tcp != null)
                    {
                        this.tcp.ReadStop();
                        this.tcp.CloseHandle();
                    }
                    this.tcp = null;
                }
            }
            finally
            {
                base.DoClose();
            }
        }

        protected override void DoBeginRead()
        {
            if (!this.Open)
            {
                return;
            }

            this.ReadPending = true;
            if (!this.IsInState(StateFlags.ReadScheduled))
            {
                this.SetState(StateFlags.ReadScheduled);
                this.tcp.ReadStart((TcpChannelUnsafe)this.Unsafe);
            }
        }

        protected override void DoStopRead()
        {
            if (!this.Open)
            {
                return;
            }

            if (this.IsInState(StateFlags.ReadScheduled))
            {
                this.ResetState(StateFlags.ReadScheduled);
                this.tcp.ReadStop();
            }
        }

        protected override void DoWrite(ChannelOutboundBuffer input)
        {
            List<ArraySegment<byte>> sharedBufferList = null;
            try
            {
                while (true)
                {
                    int size = input.Count;
                    if (size == 0)
                    {
                        // All written
                        break;
                    }
                    long writtenBytes = 0;
                    bool done = false;

                    // Ensure the pending writes are made of ByteBufs only.
                    sharedBufferList = input.GetSharedBufferList();
                    int nioBufferCnt = sharedBufferList.Count;
                    long expectedWrittenBytes = input.NioBufferSize;
                    switch (nioBufferCnt)
                    {
                        case 0:
                            this.DoWrite0(input);
                            return;
                        case 1:
                            {
                                ArraySegment<byte> nioBuffer = sharedBufferList[0];
                                WriteRequest request = Pool.Take();
                                int localWrittenBytes = request.Prepare((TcpChannelUnsafe)this.Unsafe, nioBuffer);
                                this.tcp.Write(request);

                                expectedWrittenBytes -= localWrittenBytes;
                                writtenBytes += localWrittenBytes;
                                if (expectedWrittenBytes == 0)
                                {
                                    done = true;
                                }
                            }
                            break;
                        default:
                            for (int i = this.Configuration.WriteSpinCount - 1; i >= 0; i--)
                            {
                                WriteRequest request = Pool.Take();
                                int localWrittenBytes = request.Prepare((TcpChannelUnsafe)this.Unsafe, sharedBufferList);
                                this.tcp.Write(request);

                                expectedWrittenBytes -= localWrittenBytes;
                                writtenBytes += localWrittenBytes;
                                if (expectedWrittenBytes == 0)
                                {
                                    done = true;
                                    break;
                                }
                            }
                            break;
                    }

                    input.RemoveBytes(writtenBytes);
                    if (!done)
                    {
                        // Flush later
                        this.EventLoop.Execute(FlushAction, this);
                        break;
                    }
                }
            }
            finally
            {
                sharedBufferList?.Clear();
            }
        }

        // Non gathering writes
        void DoWrite0(ChannelOutboundBuffer input)
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

                if (msg is IByteBuffer buf)
                {
                    int readableBytes = buf.ReadableBytes;
                    if (readableBytes == 0)
                    {
                        input.Remove();
                        continue;
                    }

                    bool done = false;
                    long flushedAmount = 0;
                    if (writeSpinCount == -1)
                    {
                        writeSpinCount = this.Configuration.WriteSpinCount;
                    }

                    for (int i = writeSpinCount - 1; i >= 0; i--)
                    {
                        flushedAmount += this.WriteBytes(buf);
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
                        this.EventLoop.Execute(FlushAction, this);
                        break;
                    }
                }
                else
                {
                    // Should not reach here.
                    throw new InvalidOperationException();
                }
            }
        }

        int WriteBytes(IByteBuffer buf)
        {
            WriteRequest writeRequest = Pool.Take();
            int totalBytes = writeRequest.Prepare((TcpChannelUnsafe)this.Unsafe, buf);
            this.tcp.Write(writeRequest);

            buf.SetReaderIndex(buf.ReaderIndex + totalBytes);
            return totalBytes;
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
