// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Net.Security;
    using System.Runtime.ExceptionServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public sealed class TlsHandler : ByteToMessageDecoder
    {
        readonly TlsSettings settings;
        const int FallbackReadBufferSize = 256;
        const int UnencryptedWriteBatchSize = 14 * 1024;

        static readonly Exception ChannelClosedException = new IOException("Channel is closed");
        static readonly Action<Task, object> HandshakeCompletionCallback = new Action<Task, object>(HandleHandshakeCompleted);

        readonly SslStream sslStream;
        readonly MediationStream mediationStream;
        readonly TaskCompletionSource closeFuture;

        TlsHandlerState state;
        int packetLength;
        volatile IChannelHandlerContext capturedContext;
        BatchingPendingWriteQueue pendingUnencryptedWrites;
        Task lastContextWriteTask;
        bool firedChannelRead;
        IByteBuffer pendingSslStreamReadBuffer;
        Task<int> pendingSslStreamReadFuture;

        public TlsHandler(TlsSettings settings)
            : this(stream => new SslStream(stream, true), settings)
        {
        }

        public TlsHandler(Func<Stream, SslStream> sslStreamFactory, TlsSettings settings)
        {
            Contract.Requires(sslStreamFactory != null);
            Contract.Requires(settings != null);

            this.settings = settings;
            this.closeFuture = new TaskCompletionSource();
            this.mediationStream = new MediationStream(this);
            this.sslStream = sslStreamFactory(this.mediationStream);
        }

        public static TlsHandler Client(string targetHost) => new TlsHandler(new ClientTlsSettings(targetHost));

        public static TlsHandler Client(string targetHost, X509Certificate clientCertificate) => new TlsHandler(new ClientTlsSettings(targetHost, new List<X509Certificate>{ clientCertificate }));
 
        public static TlsHandler Server(X509Certificate certificate) => new TlsHandler(new ServerTlsSettings(certificate));

        // using workaround mentioned here: https://github.com/dotnet/corefx/issues/4510
        public X509Certificate2 LocalCertificate => this.sslStream.LocalCertificate as X509Certificate2 ?? new X509Certificate2(this.sslStream.LocalCertificate?.Export(X509ContentType.Cert));

        public X509Certificate2 RemoteCertificate => this.sslStream.RemoteCertificate as X509Certificate2 ?? new X509Certificate2(this.sslStream.RemoteCertificate?.Export(X509ContentType.Cert));

        bool IsServer => this.settings is ServerTlsSettings;

        public void Dispose() => this.sslStream?.Dispose();

        public override void ChannelActive(IChannelHandlerContext context)
        {
            base.ChannelActive(context);

            if (!this.IsServer)
            {
                this.EnsureAuthenticated();
            }
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            // Make sure to release SslStream,
            // and notify the handshake future if the connection has been closed during handshake.
            this.HandleFailure(ChannelClosedException);

            base.ChannelInactive(context);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            if (this.IgnoreException(exception))
            {
                // Close the connection explicitly just in case the transport
                // did not close the connection automatically.
                if (context.Channel.Active)
                {
                    context.CloseAsync();
                }
            }
            else
            {
                base.ExceptionCaught(context, exception);
            }
        }

        bool IgnoreException(Exception t)
        {
            if (t is ObjectDisposedException && this.closeFuture.Task.IsCompleted)
            {
                return true;
            }
            return false;
        }

        static void HandleHandshakeCompleted(Task task, object state)
        {
            var self = (TlsHandler)state;
            switch (task.Status)
            {
                case TaskStatus.RanToCompletion:
                    {
                        TlsHandlerState oldState = self.state;

                        Contract.Assert(!oldState.HasAny(TlsHandlerState.AuthenticationCompleted));
                        self.state = (oldState | TlsHandlerState.Authenticated) & ~(TlsHandlerState.Authenticating | TlsHandlerState.FlushedBeforeHandshake);

                        self.capturedContext.FireUserEventTriggered(TlsHandshakeCompletionEvent.Success);

                        if (oldState.Has(TlsHandlerState.ReadRequestedBeforeAuthenticated) && !self.capturedContext.Channel.Configuration.AutoRead)
                        {
                            self.capturedContext.Read();
                        }

                        if (oldState.Has(TlsHandlerState.FlushedBeforeHandshake))
                        {
                            self.Wrap(self.capturedContext);
                            self.capturedContext.Flush();
                        }
                        break;
                    }
                case TaskStatus.Canceled:
                case TaskStatus.Faulted:
                    {
                        // ReSharper disable once AssignNullToNotNullAttribute -- task.Exception will be present as task is faulted
                        TlsHandlerState oldState = self.state;
                        Contract.Assert(!oldState.HasAny(TlsHandlerState.AuthenticationCompleted));
                        self.HandleFailure(task.Exception);
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(task), "Unexpected task status: " + task.Status);
            }
        }

        public override void HandlerAdded(IChannelHandlerContext context)
        {
            base.HandlerAdded(context);
            this.capturedContext = context;
            this.pendingUnencryptedWrites = new BatchingPendingWriteQueue(context, UnencryptedWriteBatchSize);
            if (context.Channel.Active && !this.IsServer)
            {
                // todo: support delayed initialization on an existing/active channel if in client mode
                this.EnsureAuthenticated();
            }
        }

        protected override void HandlerRemovedInternal(IChannelHandlerContext context)
        {
            if (!this.pendingUnencryptedWrites.IsEmpty)
            {
                // Check if queue is not empty first because create a new ChannelException is expensive
                this.pendingUnencryptedWrites.RemoveAndFailAll(new ChannelException("Write has failed due to TlsHandler being removed from channel pipeline."));
            }
        }

        protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            int startOffset = input.ReaderIndex;
            int endOffset = input.WriterIndex;
            int offset = startOffset;
            int totalLength = 0;

            List<int> packetLengths;
            // if we calculated the length of the current SSL record before, use that information.
            if (this.packetLength > 0)
            {
                if (endOffset - startOffset < this.packetLength)
                {
                    // input does not contain a single complete SSL record
                    return;
                }
                else
                {
                    packetLengths = new List<int>(4);
                    packetLengths.Add(this.packetLength);
                    offset += this.packetLength;
                    totalLength = this.packetLength;
                    this.packetLength = 0;
                }
            }
            else
            {
                packetLengths = new List<int>(4);
            }

            bool nonSslRecord = false;

            while (totalLength < TlsUtils.MAX_ENCRYPTED_PACKET_LENGTH)
            {
                int readableBytes = endOffset - offset;
                if (readableBytes < TlsUtils.SSL_RECORD_HEADER_LENGTH)
                {
                    break;
                }

                int encryptedPacketLength = TlsUtils.GetEncryptedPacketLength(input, offset);
                if (encryptedPacketLength == -1)
                {
                    nonSslRecord = true;
                    break;
                }

                Contract.Assert(encryptedPacketLength > 0);

                if (encryptedPacketLength > readableBytes)
                {
                    // wait until the whole packet can be read
                    this.packetLength = encryptedPacketLength;
                    break;
                }

                int newTotalLength = totalLength + encryptedPacketLength;
                if (newTotalLength > TlsUtils.MAX_ENCRYPTED_PACKET_LENGTH)
                {
                    // Don't read too much.
                    break;
                }

                // 1. call unwrap with packet boundaries - call SslStream.ReadAsync only once.
                // 2. once we're through all the whole packets, switch to reading out using fallback sized buffer

                // We have a whole packet.
                // Increment the offset to handle the next packet.
                packetLengths.Add(encryptedPacketLength);
                offset += encryptedPacketLength;
                totalLength = newTotalLength;
            }

            if (totalLength > 0)
            {
                // The buffer contains one or more full SSL records.
                // Slice out the whole packet so unwrap will only be called with complete packets.
                // Also directly reset the packetLength. This is needed as unwrap(..) may trigger
                // decode(...) again via:
                // 1) unwrap(..) is called
                // 2) wrap(...) is called from within unwrap(...)
                // 3) wrap(...) calls unwrapLater(...)
                // 4) unwrapLater(...) calls decode(...)
                //
                // See https://github.com/netty/netty/issues/1534

                input.SkipBytes(totalLength);
                this.Unwrap(context, input, startOffset, totalLength, packetLengths, output);

                if (!this.firedChannelRead)
                {
                    // Check first if firedChannelRead is not set yet as it may have been set in a
                    // previous decode(...) call.
                    this.firedChannelRead = output.Count > 0;
                }
            }

            if (nonSslRecord)
            {
                // Not an SSL/TLS packet
                var ex = new NotSslRecordException(
                    "not an SSL/TLS record: " + ByteBufferUtil.HexDump(input));
                input.SkipBytes(input.ReadableBytes);
                context.FireExceptionCaught(ex);
                this.HandleFailure(ex);
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext ctx)
        {
            // Discard bytes of the cumulation buffer if needed.
            this.DiscardSomeReadBytes();

            this.ReadIfNeeded(ctx);

            this.firedChannelRead = false;
            ctx.FireChannelReadComplete();
        }

        void ReadIfNeeded(IChannelHandlerContext ctx)
        {
            // if handshake is not finished yet, we need more data
            if (!ctx.Channel.Configuration.AutoRead && (!this.firedChannelRead || !this.state.HasAny(TlsHandlerState.AuthenticationCompleted)))
            {
                // No auto-read used and no message was passed through the ChannelPipeline or the handshake was not completed
                // yet, which means we need to trigger the read to ensure we will not stall
                ctx.Read();
            }
        }

        /// <summary>Unwraps inbound SSL records.</summary>
        void Unwrap(IChannelHandlerContext ctx, IByteBuffer packet, int offset, int length, List<int> packetLengths, List<object> output)
        {
            Contract.Requires(packetLengths.Count > 0);

            //bool notifyClosure = false; // todo: netty/issues/137
            bool pending = false;

            IByteBuffer outputBuffer = null;

            try
            {
                ArraySegment<byte> inputIoBuffer = packet.GetIoBuffer(offset, length);
                this.mediationStream.SetSource(inputIoBuffer.Array, inputIoBuffer.Offset);

                int packetIndex = 0;

                while (!this.EnsureAuthenticated())
                {
                    this.mediationStream.ExpandSource(packetLengths[packetIndex]);
                    if (++packetIndex == packetLengths.Count)
                    {
                        return;
                    }
                }

                Task<int> currentReadFuture = this.pendingSslStreamReadFuture;

                int outputBufferLength;

                if (currentReadFuture != null)
                {
                    // restoring context from previous read
                    Contract.Assert(this.pendingSslStreamReadBuffer != null);

                    outputBuffer = this.pendingSslStreamReadBuffer;
                    outputBufferLength = outputBuffer.WritableBytes;
                }
                else
                {
                    outputBufferLength = 0;
                }

                // go through packets one by one (because SslStream does not consume more than 1 packet at a time)
                for (; packetIndex < packetLengths.Count; packetIndex++)
                {
                    int currentPacketLength = packetLengths[packetIndex];
                    this.mediationStream.ExpandSource(currentPacketLength);

                    if (currentReadFuture != null)
                    {
                        // there was a read pending already, so we make sure we completed that first

                        if (!currentReadFuture.IsCompleted)
                        {
                            // we did feed the whole current packet to SslStream yet it did not produce any result -> move to the next packet in input
                            Contract.Assert(this.mediationStream.SourceReadableBytes == 0);

                            continue;
                        }

                        int read = currentReadFuture.Result;

                        // Now output the result of previous read and decide whether to do an extra read on the same source or move forward
                        AddBufferToOutput(outputBuffer, read, output);

                        currentReadFuture = null;
                        if (this.mediationStream.SourceReadableBytes == 0)
                        {
                            // we just made a frame available for reading but there was already pending read so SslStream read it out to make further progress there

                            if (read < outputBufferLength)
                            {
                                // SslStream returned non-full buffer and there's no more input to go through ->
                                // typically it means SslStream is done reading current frame so we skip
                                continue;
                            }

                            // we've read out `read` bytes out of current packet to fulfil previously outstanding read
                            outputBufferLength = currentPacketLength - read;
                            if (outputBufferLength <= 0)
                            {
                                // after feeding to SslStream current frame it read out more bytes than current packet size
                                outputBufferLength = FallbackReadBufferSize;
                            }
                        }
                        else
                        {
                            // SslStream did not get to reading current frame so it completed previous read sync
                            // and the next read will likely read out the new frame
                            outputBufferLength = currentPacketLength;
                        }
                    }
                    else
                    {
                        // there was no pending read before so we estimate buffer of `currentPacketLength` bytes to be sufficient
                        outputBufferLength = currentPacketLength;
                    }

                    outputBuffer = ctx.Allocator.Buffer(outputBufferLength);
                    currentReadFuture = this.ReadFromSslStreamAsync(outputBuffer, outputBufferLength);
                }

                // read out the rest of SslStream's output (if any) at risk of going async
                // using FallbackReadBufferSize - buffer size we're ok to have pinned with the SslStream until it's done reading
                while (true)
                {
                    if (currentReadFuture != null)
                    {
                        if (!currentReadFuture.IsCompleted)
                        {
                            break;
                        }
                        int read = currentReadFuture.Result;
                        AddBufferToOutput(outputBuffer, read, output);
                    }
                    outputBuffer = ctx.Allocator.Buffer(FallbackReadBufferSize);
                    currentReadFuture = this.ReadFromSslStreamAsync(outputBuffer, FallbackReadBufferSize);
                }

                pending = true;
                this.pendingSslStreamReadBuffer = outputBuffer;
                this.pendingSslStreamReadFuture = currentReadFuture;
            }
            catch (Exception ex)
            {
                this.HandleFailure(ex);
                throw;
            }
            finally
            {
                this.mediationStream.ResetSource();
                if (!pending && outputBuffer != null)
                {
                    if (outputBuffer.IsReadable())
                    {
                        output.Add(outputBuffer);
                    }
                    else
                    {
                        outputBuffer.SafeRelease();
                    }
                }
            }
        }

        static void AddBufferToOutput(IByteBuffer outputBuffer, int length, List<object> output)
        {
            Contract.Assert(length > 0);
            output.Add(outputBuffer.SetWriterIndex(outputBuffer.WriterIndex + length));
        }

        Task<int> ReadFromSslStreamAsync(IByteBuffer outputBuffer, int outputBufferLength)
        {
            ArraySegment<byte> outlet = outputBuffer.GetIoBuffer(outputBuffer.WriterIndex, outputBufferLength);
            return this.sslStream.ReadAsync(outlet.Array, outlet.Offset, outlet.Count);
        }

        public override void Read(IChannelHandlerContext context)
        {
            TlsHandlerState oldState = this.state;
            if (!oldState.HasAny(TlsHandlerState.AuthenticationCompleted))
            {
                this.state = oldState | TlsHandlerState.ReadRequestedBeforeAuthenticated;
            }

            context.Read();
        }

        bool EnsureAuthenticated()
        {
            TlsHandlerState oldState = this.state;
            if (!oldState.HasAny(TlsHandlerState.AuthenticationStarted))
            {
                this.state = oldState | TlsHandlerState.Authenticating;
                if (this.IsServer)
                {
                    var serverSettings = (ServerTlsSettings)this.settings;
                    this.sslStream.AuthenticateAsServerAsync(serverSettings.Certificate, serverSettings.NegotiateClientCertificate, serverSettings.EnabledProtocols, serverSettings.CheckCertificateRevocation)
                        .ContinueWith(HandshakeCompletionCallback, this, TaskContinuationOptions.ExecuteSynchronously);
                }
                else
                {
                    var clientSettings = (ClientTlsSettings)this.settings;
                    this.sslStream.AuthenticateAsClientAsync(clientSettings.TargetHost, clientSettings.X509CertificateCollection, clientSettings.EnabledProtocols, clientSettings.CheckCertificateRevocation)
                        .ContinueWith(HandshakeCompletionCallback, this, TaskContinuationOptions.ExecuteSynchronously);
                }
                return false;
            }

            return oldState.Has(TlsHandlerState.Authenticated);
        }

        public override Task WriteAsync(IChannelHandlerContext context, object message)
        {
            if (!(message is IByteBuffer))
            {
                return TaskEx.FromException(new UnsupportedMessageTypeException(message, typeof(IByteBuffer)));
            }
            return this.pendingUnencryptedWrites.Add(message);
        }

        public override void Flush(IChannelHandlerContext context)
        {
            if (this.pendingUnencryptedWrites.IsEmpty)
            {
                this.pendingUnencryptedWrites.Add(Unpooled.Empty);
            }

            if (!this.EnsureAuthenticated())
            {
                this.state |= TlsHandlerState.FlushedBeforeHandshake;
                return;
            }

            try
            {
                this.Wrap(context);
            }
            finally
            {
                // We may have written some parts of data before an exception was thrown so ensure we always flush.
                context.Flush();
            }
        }

        void Wrap(IChannelHandlerContext context)
        {
            Contract.Assert(context == this.capturedContext);

            IByteBuffer buf = null;
            try
            {
                while (true)
                {
                    List<object> messages = this.pendingUnencryptedWrites.Current;
                    if (messages == null || messages.Count == 0)
                    {
                        break;
                    }

                    if (messages.Count == 1)
                    {
                        buf = (IByteBuffer)messages[0];
                    }
                    else
                    {
                        buf = context.Allocator.Buffer((int)this.pendingUnencryptedWrites.CurrentSize);
                        foreach (IByteBuffer buffer in messages)
                        {
                            buffer.ReadBytes(buf, buffer.ReadableBytes);
                            buffer.Release();
                        }
                    }
                    buf.ReadBytes(this.sslStream, buf.ReadableBytes); // this leads to FinishWrap being called 0+ times
                    buf.Release();

                    TaskCompletionSource promise = this.pendingUnencryptedWrites.Remove();
                    Task task = this.lastContextWriteTask;
                    if (task != null)
                    {
                        task.LinkOutcome(promise);
                        this.lastContextWriteTask = null;
                    }
                    else
                    {
                        promise.TryComplete();
                    }
                }
            }
            catch (Exception ex)
            {
                buf.SafeRelease();
                this.HandleFailure(ex);
                throw;
            }
        }

        void FinishWrap(byte[] buffer, int offset, int count)
        {
            IByteBuffer output;
            if (count == 0)
            {
                output = Unpooled.Empty;
            }
            else
            {
                output = this.capturedContext.Allocator.Buffer(count);
                output.WriteBytes(buffer, offset, count);
            }

            this.lastContextWriteTask = this.capturedContext.WriteAsync(output);
        }

        Task FinishWrapNonAppDataAsync(byte[] buffer, int offset, int count)
        {
            var future = this.capturedContext.WriteAndFlushAsync(Unpooled.WrappedBuffer(buffer, offset, count));
            this.ReadIfNeeded(this.capturedContext);
            return future;
        }

        public override Task CloseAsync(IChannelHandlerContext context)
        {
            this.closeFuture.TryComplete();
            this.sslStream.Dispose();
            return base.CloseAsync(context);
        }

        void HandleFailure(Exception cause)
        {
            // Release all resources such as internal buffers that SSLEngine
            // is managing.

            try
            {
                this.sslStream.Dispose();
            }
            catch (Exception)
            {
                // todo: evaluate following:
                // only log in Debug mode as it most likely harmless and latest chrome still trigger
                // this all the time.
                //
                // See https://github.com/netty/netty/issues/1340
                //string msg = ex.Message;
                //if (msg == null || !msg.contains("possible truncation attack"))
                //{
                //    //Logger.Debug("{} SSLEngine.closeInbound() raised an exception.", ctx.channel(), e);
                //}
            }
            this.NotifyHandshakeFailure(cause);
            this.pendingUnencryptedWrites.RemoveAndFailAll(cause);
        }

        void NotifyHandshakeFailure(Exception cause)
        {
            if (!this.state.HasAny(TlsHandlerState.AuthenticationCompleted))
            {
                // handshake was not completed yet => TlsHandler react to failure by closing the channel
                this.state = (this.state | TlsHandlerState.FailedAuthentication) & ~TlsHandlerState.Authenticating;
                this.capturedContext.FireUserEventTriggered(new TlsHandshakeCompletionEvent(cause));
                this.CloseAsync(this.capturedContext);
            }
        }

        sealed class MediationStream : Stream
        {
            readonly TlsHandler owner;
            byte[] input;
            int inputStartOffset;
            int inputOffset;
            int inputLength;
            TaskCompletionSource<int> readCompletionSource;
            ArraySegment<byte> sslOwnedBuffer;
#if NETSTANDARD1_3
            int readByteCount;
#else
            SynchronousAsyncResult<int> syncReadResult;
            AsyncCallback readCallback;
            TaskCompletionSource writeCompletion;
            AsyncCallback writeCallback;
#endif

            public MediationStream(TlsHandler owner)
            {
                this.owner = owner;
            }

            public int SourceReadableBytes => this.inputLength - this.inputOffset;

            public void SetSource(byte[] source, int offset)
            {
                this.input = source;
                this.inputStartOffset = offset;
                this.inputOffset = 0;
                this.inputLength = 0;
            }

            public void ResetSource()
            {
                this.input = null;
                this.inputLength = 0;
            }

            public void ExpandSource(int count)
            {
                Contract.Assert(this.input != null);

                this.inputLength += count;

                TaskCompletionSource<int> promise = this.readCompletionSource;
                if (promise == null)
                {
                    // there is no pending read operation - keep for future
                    return;
                }

                ArraySegment<byte> sslBuffer = this.sslOwnedBuffer;

#if NETSTANDARD1_3
                this.readByteCount = this.ReadFromInput(sslBuffer.Array, sslBuffer.Offset, sslBuffer.Count);
                // hack: this tricks SslStream's continuation to run synchronously instead of dispatching to TP. Remove once Begin/EndRead are available. 
                new Task(
                    ms =>
                    {
                        var self = (MediationStream)ms;
                        TaskCompletionSource<int> p = self.readCompletionSource;
                        this.readCompletionSource = null;
                        p.TrySetResult(self.readByteCount);
                    },
                    this)
                    .RunSynchronously(TaskScheduler.Default);
#else
                int read = this.ReadFromInput(sslBuffer.Array, sslBuffer.Offset, sslBuffer.Count);
                this.readCompletionSource = null;
                promise.TrySetResult(read);
                this.readCallback?.Invoke(promise.Task);
#endif
            }

#if NETSTANDARD1_3
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (this.inputLength - this.inputOffset > 0)
                {
                    // we have the bytes available upfront - write out synchronously
                    int read = this.ReadFromInput(buffer, offset, count);
                    return Task.FromResult(read);
                }

                // take note of buffer - we will pass bytes there once available
                this.sslOwnedBuffer = new ArraySegment<byte>(buffer, offset, count);
                this.readCompletionSource = new TaskCompletionSource<int>();
                return this.readCompletionSource.Task;
            }
#else
            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            {
                if (this.inputLength - this.inputOffset > 0)
                {
                    // we have the bytes available upfront - write out synchronously
                    int read = this.ReadFromInput(buffer, offset, count);
                    return this.PrepareSyncReadResult(read, state);
                }

                // take note of buffer - we will pass bytes there once available
                this.sslOwnedBuffer = new ArraySegment<byte>(buffer, offset, count);
                this.readCompletionSource = new TaskCompletionSource<int>(state);
                this.readCallback = callback;
                return this.readCompletionSource.Task;
            }

            public override int EndRead(IAsyncResult asyncResult)
            {
                SynchronousAsyncResult<int> syncResult = this.syncReadResult;
                if (ReferenceEquals(asyncResult, syncResult))
                {
                    return syncResult.Result;
                }

                Contract.Assert(!((Task<int>)asyncResult).IsCanceled);

                try
                {
                    return ((Task<int>)asyncResult).Result;
                }
                catch (AggregateException ex)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    throw; // unreachable
                }
                finally
                {
                    this.readCompletionSource = null;
                    this.readCallback = null;
                    this.sslOwnedBuffer = default(ArraySegment<byte>);
                }
            }

            IAsyncResult PrepareSyncReadResult(int readBytes, object state)
            {
                // it is safe to reuse sync result object as it can't lead to leak (no way to attach to it via handle)
                SynchronousAsyncResult<int> result = this.syncReadResult ?? (this.syncReadResult = new SynchronousAsyncResult<int>());
                result.Result = readBytes;
                result.AsyncState = state;
                return result;
            }
#endif

            public override void Write(byte[] buffer, int offset, int count) => this.owner.FinishWrap(buffer, offset, count);

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => this.owner.FinishWrapNonAppDataAsync(buffer, offset, count);

#if !NETSTANDARD1_3
            static readonly Action<Task, object> WriteCompleteCallback = HandleChannelWriteComplete;

            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            {
                Task task = this.WriteAsync(buffer, offset, count);
                switch (task.Status)
                {
                    case TaskStatus.RanToCompletion:
                        // write+flush completed synchronously (and successfully)
                        var result = new SynchronousAsyncResult<int>();
                        result.AsyncState = state;
                        callback(result);
                        return result;
                    default:
                        this.writeCallback = callback;
                        var tcs = new TaskCompletionSource(state);
                        this.writeCompletion = tcs;
                        task.ContinueWith(WriteCompleteCallback, this, TaskContinuationOptions.ExecuteSynchronously);
                        return tcs.Task;
                }
            }

            static void HandleChannelWriteComplete(Task writeTask, object state)
            {
                var self = (MediationStream)state;
                switch (writeTask.Status)
                {
                    case TaskStatus.RanToCompletion:
                        self.writeCompletion.TryComplete();
                        break;
                    case TaskStatus.Canceled:
                        self.writeCompletion.TrySetCanceled();
                        break;
                    case TaskStatus.Faulted:
                        self.writeCompletion.TrySetException(writeTask.Exception);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("Unexpected task status: " + writeTask.Status);
                }

                self.writeCallback?.Invoke(self.writeCompletion.Task);
            }

            public override void EndWrite(IAsyncResult asyncResult)
            {
                this.writeCallback = null;
                this.writeCompletion = null;

                if (asyncResult is SynchronousAsyncResult<int>)
                {
                    return;
                }

                try
                {
                    ((Task<int>)asyncResult).Wait();
                }
                catch (AggregateException ex)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    throw;
                }
            }
#endif

            int ReadFromInput(byte[] destination, int destinationOffset, int destinationCapacity)
            {
                Contract.Assert(destination != null);

                byte[] source = this.input;
                int readableBytes = this.inputLength - this.inputOffset;
                int length = Math.Min(readableBytes, destinationCapacity);
                Buffer.BlockCopy(source, this.inputStartOffset + this.inputOffset, destination, destinationOffset, length);
                this.inputOffset += length;
                return length;
            }

            public override void Flush()
            {
                // NOOP: called on SslStream.Close
            }

#region plumbing

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length
            {
                get { throw new NotSupportedException(); }
            }

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

#endregion

#region sync result
            sealed class SynchronousAsyncResult<T> : IAsyncResult
            {
                public T Result { get; set; }

                public bool IsCompleted => true;

                public WaitHandle AsyncWaitHandle
                {
                    get { throw new InvalidOperationException("Cannot wait on a synchronous result."); }
                }

                public object AsyncState { get; set; }

                public bool CompletedSynchronously => true;
            }

#endregion
        }
    }

    [Flags]
    enum TlsHandlerState
    {
        Authenticating = 1,
        Authenticated = 1 << 1,
        FailedAuthentication = 1 << 2,
        ReadRequestedBeforeAuthenticated = 1 << 3,
        FlushedBeforeHandshake = 1 << 4,
        AuthenticationStarted = Authenticating | Authenticated | FailedAuthentication,
        AuthenticationCompleted = Authenticated | FailedAuthentication
    }

    static class TlsHandlerStateExtensions
    {
        public static bool Has(this TlsHandlerState value, TlsHandlerState testValue) => (value & testValue) == testValue;

        public static bool HasAny(this TlsHandlerState value, TlsHandlerState testValue) => (value & testValue) != 0;
    }
}