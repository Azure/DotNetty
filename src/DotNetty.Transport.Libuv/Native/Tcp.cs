// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Net;
    using DotNetty.Buffers;
    using DotNetty.Common.Internal;

    sealed class Tcp : TcpHandle
    {
        static readonly uv_alloc_cb AllocateCallback = OnAllocateCallback;
        static readonly uv_read_cb ReadCallback = OnReadCallback;

        INativeUnsafe unsafeChannel;
        readonly ILinkedQueue<ReadOperation> pendingReads;

        public Tcp(Loop loop) : base(loop)
        {
            this.pendingReads = PlatformDependent.NewSpscLinkedQueue<ReadOperation>();
        }

        public void ReadStart(INativeUnsafe channel)
        {
            Contract.Requires(channel != null);

            this.Validate();
            int result = NativeMethods.uv_read_start(this.Handle, AllocateCallback, ReadCallback);
            if (result < 0)
            {
                throw NativeMethods.CreateError((uv_err_code)result);
            }

            this.unsafeChannel = channel;
        }

        public void ReadStop()
        {
            if (this.Handle == IntPtr.Zero)
            {
                return;
            }

            // This function is idempotent and may be safely called on a stopped stream.
            int result = NativeMethods.uv_read_stop(this.Handle);
            if (result < 0)
            {
                throw NativeMethods.CreateError((uv_err_code)result);
            }
        }

        static void OnReadCallback(IntPtr handle, IntPtr nread, ref uv_buf_t buf)
        {
            var tcp = GetTarget<Tcp>(handle);
            int status = (int)nread.ToInt64();

            OperationException error = null;
            if (status < 0 && status != (int)uv_err_code.UV_EOF)
            {
                error = NativeMethods.CreateError((uv_err_code)status);
            }

            ReadOperation operation = tcp.pendingReads.Poll();
            if (operation == null)
            {
                if (error != null)
                {
                    // It is possible if the client connection resets  
                    // causing errors where there are no pending read
                    // operations, in this case we just notify the channel
                    // for errors
                    operation = new ReadOperation(tcp.unsafeChannel, Unpooled.Empty);
                }
                else
                {
                    Logger.Warn("Channel read operation completed prematurely.");
                    return;
                }
            }

            try
            {
                operation.Complete(status, error);
            }
            catch (Exception exception)
            {
                Logger.Warn("Channel read callbcak failed.", exception);
            }
        }

        public void Write(WriteRequest request)
        {
            this.Validate();
            int result;
            try
            {
                result = NativeMethods.uv_write(
                    request.Handle,
                    this.Handle,
                    request.Bufs,
                    request.BufferCount,
                    WriteRequest.WriteCallback);
            }
            catch
            {
                request?.Release();
                throw;
            }

            if (result < 0)
            {
                throw NativeMethods.CreateError((uv_err_code)result);
            }
        }

        protected override void OnClosed()
        {
            base.OnClosed();

            while (true)
            {
                ReadOperation operation = this.pendingReads.Poll();
                if (operation == null)
                {
                    break;
                }

                operation.Dispose();
            }

            this.unsafeChannel = null;
        }

        void OnAllocateCallback(out uv_buf_t buf)
        {
            ReadOperation operation = this.unsafeChannel.PrepareRead();
            this.pendingReads.Offer(operation);
            buf = operation.GetBuffer();
        }

        static void OnAllocateCallback(IntPtr handle, IntPtr suggestedSize, out uv_buf_t buf)
        {
            var tcp = GetTarget<Tcp>(handle);
            tcp.OnAllocateCallback(out buf);
        }

        public IPEndPoint GetPeerEndPoint()
        {
            this.Validate();
            return NativeMethods.TcpGetPeerName(this.Handle);
        }
    }
}
