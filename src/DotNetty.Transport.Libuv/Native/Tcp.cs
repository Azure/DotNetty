// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Diagnostics;
    using System.Net;

    sealed class Tcp : TcpHandle
    {
        static readonly uv_alloc_cb AllocateCallback = OnAllocateCallback;
        static readonly uv_read_cb ReadCallback = OnReadCallback;

        readonly ReadOperation pendingRead;
        NativeChannel.INativeUnsafe nativeUnsafe;

        public Tcp(Loop loop, uint flags = 0 /* AF_UNSPEC */ ) : base(loop, flags)
        {
            this.pendingRead = new ReadOperation();
        }

        public void ReadStart(NativeChannel.INativeUnsafe channel)
        {
            Debug.Assert(channel != null);

            this.Validate();
            int result = NativeMethods.uv_read_start(this.Handle, AllocateCallback, ReadCallback);
            NativeMethods.ThrowIfError(result);
            this.nativeUnsafe = channel;
        }

        public void ReadStop()
        {
            if (this.Handle == IntPtr.Zero)
            {
                return;
            }

            // This function is idempotent and may be safely called on a stopped stream.
            NativeMethods.uv_read_stop(this.Handle);
        }

        void OnReadCallback(int statusCode, OperationException error)
        {
            try
            {
                this.pendingRead.Complete(statusCode, error);
                this.nativeUnsafe.FinishRead(this.pendingRead);
            }
            catch (Exception exception)
            {
                Logger.Warn($"Tcp {this.Handle} read callbcak error.", exception);
            }
            finally
            {
                this.pendingRead.Reset();
            }
        }

        static void OnReadCallback(IntPtr handle, IntPtr nread, ref uv_buf_t buf)
        {
            var tcp = GetTarget<Tcp>(handle);
            int status = (int)nread.ToInt64();

            OperationException error = null;
            if (status < 0 && status != NativeMethods.EOF)
            {
                error = NativeMethods.CreateError((uv_err_code)status);
            }

            tcp.OnReadCallback(status, error);
        }

        protected override void OnClosed()
        {
            base.OnClosed();

            this.pendingRead.Dispose();
            this.nativeUnsafe = null;
        }

        void OnAllocateCallback(out uv_buf_t buf)
        {
            buf = this.nativeUnsafe.PrepareRead(this.pendingRead);
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
