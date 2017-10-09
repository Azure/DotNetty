// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Runtime.InteropServices;

    abstract unsafe class TcpHandle : NativeHandle
    {
        protected TcpHandle(Loop loop) : base(uv_handle_type.UV_TCP)
        {
            Contract.Requires(loop != null);

            int size = NativeMethods.uv_handle_size(uv_handle_type.UV_TCP).ToInt32();
            IntPtr handle = Marshal.AllocHGlobal(size);

            int result;
            try
            {
                result = NativeMethods.uv_tcp_init(loop.Handle, handle);
            }
            catch (Exception)
            {
                Marshal.FreeHGlobal(handle);
                throw;
            }
            if (result < 0)
            {
                Marshal.FreeHGlobal(handle);
                throw NativeMethods.CreateError((uv_err_code)result);
            }

            GCHandle gcHandle = GCHandle.Alloc(this, GCHandleType.Normal);
            ((uv_handle_t*)handle)->data = GCHandle.ToIntPtr(gcHandle);
            this.Handle = handle;
        }

        internal void Bind(IPEndPoint endPoint, bool dualStack = false)
        {
            Contract.Requires(endPoint != null);

            this.Validate();
            NativeMethods.GetSocketAddress(endPoint, out sockaddr addr);
            int result = NativeMethods.uv_tcp_bind(this.Handle, ref addr, (uint)(dualStack ? 1 : 0));
            if (result < 0)
            {
                throw NativeMethods.CreateError((uv_err_code)result);
            }
        }

        public IPEndPoint GetLocalEndPoint()
        {
            this.Validate();
            return NativeMethods.TcpGetSocketName(this.Handle);
        }

        public void NoDelay(bool value)
        {
            this.Validate();

            int result = NativeMethods.uv_tcp_nodelay(this.Handle, value ? 1 : 0);
            if (result < 0)
            {
                throw NativeMethods.CreateError((uv_err_code)result);
            }
        }

        public void SendBufferSize(int value)
        {
            Contract.Requires(value > 0);

            this.Validate();

            int bufferSize = 0; // 0 to get the current value
            var size = (IntPtr)bufferSize;
            int result = NativeMethods.uv_send_buffer_size(this.Handle, ref size);
            if (result == value)
            {
                return;
            }

            bufferSize = value;
            size = (IntPtr)bufferSize;
            result = NativeMethods.uv_send_buffer_size(this.Handle, ref size);
            if (result < 0)
            {
                throw NativeMethods.CreateError((uv_err_code)result);
            }
        }

        public void ReceiveBufferSize(int value)
        {
            Contract.Requires(value > 0);

            this.Validate();

            int bufferSize = 0; // 0 to get the current value
            var size = (IntPtr)bufferSize;
            int result = NativeMethods.uv_recv_buffer_size(this.Handle, ref size);
            if (result == value)
            {
                return;
            }

            bufferSize = value;
            size = (IntPtr)bufferSize;
            result = NativeMethods.uv_recv_buffer_size(this.Handle, ref size);
            if (result < 0)
            {
                throw NativeMethods.CreateError((uv_err_code)result);
            }
        }

        public void KeepAlive(bool value, int delay)
        {
            this.Validate();

            int result = NativeMethods.uv_tcp_keepalive(this.Handle, value ? 1 : 0, delay);
            if (result < 0)
            {
                throw NativeMethods.CreateError((uv_err_code)result);
            }
        }

        public void SimultaneousAccepts(bool value)
        {
            this.Validate();

            int result = NativeMethods.uv_tcp_simultaneous_accepts(this.Handle, value ? 1 : 0);
            if (result < 0)
            {
                throw NativeMethods.CreateError((uv_err_code)result);
            }
        }
    }
}
