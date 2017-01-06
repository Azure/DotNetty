// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNettyTestApp
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using Windows.Networking.Sockets;
    using DotNetty.Transport.Channels;
    using System.Runtime.InteropServices.WindowsRuntime;
    using Windows.Networking;
    using Windows.Storage.Streams;
    using Windows.Security.Cryptography.Certificates;
    using DotNetty.Codecs;
    using DotNetty.Handlers.Tls;

    public class StreamSocketDecoder : ByteToMessageDecoder
    {
        protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            IByteBuffer buff = input.Slice(input.ReaderIndex, input.ReadableBytes);
            buff.Retain();

            input.SetReaderIndex(input.ReaderIndex + input.ReadableBytes);
            output.Add(buff);
        }
    }

    public class StreamSocketChannel : AbstractChannel
    {
        readonly StreamSocket streamSocket;
        readonly HostName remoteHostName;
        readonly string remoteServiceName;
        readonly HostName validationHostName;

        bool open;
        bool active;

        internal bool ReadPending { get; set; }

        internal bool WriteInProgress { get; set; }

        public StreamSocketChannel(HostName remoteHostName, string remoteServiceName, HostName validationHostName) : base(null)
        {
            this.remoteHostName = remoteHostName;
            this.remoteServiceName = remoteServiceName;
            this.validationHostName = validationHostName;

            this.streamSocket = new StreamSocket();

            // Fire-and-forget is by design here: when connection completes (successfully or not), ConnectAsync sets connected event.
#pragma warning disable 4014
            this.ConnectAsync();
#pragma warning restore 4014

            this.active = true;
            this.open = true;
            this.Metadata = new ChannelMetadata(false, 16);
            this.Configuration = new DefaultChannelConfiguration(this);
        }

        private TaskCompletionSource connected = new TaskCompletionSource();
        public async Task ConnectAsync()
        {
            try
            {
                await this.StreamSocket.ConnectAsync(remoteHostName, remoteServiceName, SocketProtectionLevel.PlainSocket);

                streamSocket.Control.IgnorableServerCertificateErrors.Add(ChainValidationResult.Untrusted);

                await streamSocket.UpgradeToSslAsync(SocketProtectionLevel.Tls12, validationHostName);

                connected.Complete();
            }
            catch (Exception ex)
            {
                connected.SetException(ex);
            }
        }

        public Task EnsureConnected()
        {
            return connected.Task;
        }

        public StreamSocket StreamSocket => this.streamSocket;

        public override bool Active => this.active;

        public override IChannelConfiguration Configuration { get; }

        public override ChannelMetadata Metadata { get; }

        public override bool Open => this.open;

        protected override EndPoint LocalAddressInternal
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        protected override EndPoint RemoteAddressInternal
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        protected override async void DoBeginRead()
        {
            IByteBuffer byteBuffer = null;
            IRecvByteBufAllocatorHandle allocHandle = null;
            try
            {
                await EnsureConnected();

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

                    byte[] data = new byte[byteBuffer.Capacity];
                    var buffer = data.AsBuffer();

                    var completion = await this.streamSocket.InputStream.ReadAsync(buffer, (uint)byteBuffer.WritableBytes, InputStreamOptions.Partial);

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
            this.streamSocket.Dispose();
        }

        protected override void DoDisconnect()
        {
            this.streamSocket.Dispose();
        }

        protected override async void DoWrite(ChannelOutboundBuffer channelOutboundBuffer)
        {
            try
            {
                await EnsureConnected();

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

                    if (byteBuffer.ReadableBytes > 0)
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

                var result = await this.streamSocket.OutputStream.WriteAsync(allbytes.AsBuffer());

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
                return ((StreamSocketChannel)this.channel).EnsureConnected();
            }

#if false // This seems unnecessary
            protected override void Flush0()
            {
                // Flush immediately only when there's no pending flush.
                // If there's a pending flush operation, event loop will call FinishWrite() later,
                // and thus there's no need to call it now.

                if (((StreamSocketChannel)this.channel).WriteInProgress)
                {
                    return;
                }

                base.Flush0();
            }
#endif
        }
    }
}