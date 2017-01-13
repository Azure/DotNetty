// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNettyTestApp
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Threading.Tasks;
    using Windows.Networking.Sockets;
    using Windows.Storage.Streams;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// StreamSocketChannel is a sample implementation of AbstractChannel that is based on WinRT 
    /// Windows.Networking.Sockets.StreamSocket class. The channel receives a connected StreamSocket
    /// object that may or may not be upgraded to SSL.
    /// This a reference implementation only.
    /// Possible extensions are:
    /// - Exposing server side certificates and their validation
    /// - Exposing additional configuration options (such as SSL)
    /// </summary>
    public class StreamSocketChannel : AbstractChannel
    {
        readonly StreamSocket streamSocket;
        static readonly ChannelMetadata metadata = new ChannelMetadata(false, 16);

        bool open;
        bool active;

        bool ReadPending { get; set; }

        bool WriteInProgress { get; set; }

        public StreamSocketChannel(StreamSocket streamSocket)
            : base(null)
        {
            this.streamSocket = streamSocket;

            this.active = true;
            this.open = true;
            this.Configuration = new DefaultChannelConfiguration(this);
        }

        public StreamSocket StreamSocket => this.streamSocket;

        public override bool Active => this.active;

        public override IChannelConfiguration Configuration { get; }

        public override ChannelMetadata Metadata => metadata;

        public override bool Open => this.open;

        protected override EndPoint LocalAddressInternal
        {
            get { throw new NotImplementedException(); }
        }

        protected override EndPoint RemoteAddressInternal
        {
            get { throw new NotImplementedException(); }
        }

        protected override async void DoBeginRead()
        {
            IByteBuffer byteBuffer = null;
            IRecvByteBufAllocatorHandle allocHandle = null;
            try
            {
                if (!this.Open || this.ReadPending)
                {
                    return;
                }

                this.ReadPending = true;
                IByteBufferAllocator allocator = this.Configuration.Allocator;
                allocHandle = this.Configuration.RecvByteBufAllocator.NewHandle();
                allocHandle.Reset(this.Configuration);
                do
                {
                    byteBuffer = allocHandle.Allocate(allocator);

                    var data = new byte[byteBuffer.Capacity];
                    IBuffer buffer = data.AsBuffer();

                    IBuffer completion = await this.streamSocket.InputStream.ReadAsync(buffer, (uint)byteBuffer.WritableBytes, InputStreamOptions.Partial);

                    byteBuffer.WriteBytes(data, 0, (int)completion.Length);
                    allocHandle.LastBytesRead = (int)completion.Length;

                    if (allocHandle.LastBytesRead <= 0)
                    {
                        // nothing was read -> release the buffer.
                        byteBuffer.Release();
                        byteBuffer = null;
                        break;
                    }

                    this.Pipeline.FireChannelRead(byteBuffer);
                    allocHandle.IncMessagesRead(1);
                }
                while (allocHandle.ContinueReading());

                allocHandle.ReadComplete();
                this.ReadPending = false;
                this.Pipeline.FireChannelReadComplete();
            }
            catch (Exception e)
            {
                // Since this method returns void, all exceptions must be handled here.
                byteBuffer?.Release();
                allocHandle?.ReadComplete();
                this.ReadPending = false;
                this.Pipeline.FireChannelReadComplete();
                this.Pipeline.FireExceptionCaught(e);
                if (this.Active)
                {
                    await this.CloseAsync();
                }
            }
        }

        protected override void DoBind(EndPoint localAddress)
        {
            throw new NotImplementedException();
        }

        protected override void DoClose()
        {
            this.active = false;
            this.open = false;
            this.streamSocket.Dispose();
        }

        protected override void DoDisconnect() => this.streamSocket.Dispose();

        protected override async void DoWrite(ChannelOutboundBuffer channelOutboundBuffer)
        {
            try
            {
                //
                // All data is collected into one array before being written out
                //
                byte[] allbytes = null;
                this.WriteInProgress = true;
                while (true)
                {
                    object currentMessage = channelOutboundBuffer.Current;
                    if (currentMessage == null)
                    {
                        // Wrote all messages
                        break;
                    }

                    var byteBuffer = currentMessage as IByteBuffer;

                    Debug.Assert(byteBuffer != null);

                    if (byteBuffer.IsReadable())
                    {
                        if (allbytes == null)
                        {
                            allbytes = new byte[byteBuffer.ReadableBytes];
                            byteBuffer.GetBytes(0, allbytes);
                        }
                        else
                        {
                            int oldLen = allbytes.Length;
                            Array.Resize(ref allbytes, allbytes.Length + byteBuffer.ReadableBytes);
                            byteBuffer.GetBytes(0, allbytes, oldLen, byteBuffer.ReadableBytes);
                        }
                    }

                    channelOutboundBuffer.Remove();
                }

                uint result = await this.streamSocket.OutputStream.WriteAsync(allbytes.AsBuffer());

                this.WriteInProgress = false;
            }
            catch (Exception e)
            {
                // Since this method returns void, all exceptions must be handled here.

                this.WriteInProgress = false;
                this.Pipeline.FireExceptionCaught(e);
                await this.CloseAsync();
            }
        }

        protected override bool IsCompatible(IEventLoop eventLoop) => true;

        protected override IChannelUnsafe NewUnsafe() => new StreamSocketChannelUnsafe(this);

        protected class StreamSocketChannelUnsafe : AbstractUnsafe
        {
            public StreamSocketChannelUnsafe(AbstractChannel channel)
                : base(channel)
            {
            }

            public override Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
            {
                throw new NotImplementedException(); // Not supported, should not get here
            }
        }
    }
}