// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Runtime.InteropServices;

    abstract unsafe class TcpHandle : NativeHandle
    {
        protected TcpHandle(Loop loop, uint flags) : base(uv_handle_type.UV_TCP)
        {
            Debug.Assert(loop != null);

            IntPtr handle = NativeMethods.Allocate(uv_handle_type.UV_TCP);

            try
            {
                // if flags is specified as AF_INET or AF_INET6, Libuv 
                // creates the socket when tcp handle is created.
                // Otherwise the socket is created when bind to an address.
                //  
                // This is for TcpListener to create socket early before bind
                int result = flags == 0 
                    ? NativeMethods.uv_tcp_init(loop.Handle, handle) 
                    : NativeMethods.uv_tcp_init_ex(loop.Handle, handle, flags);
                NativeMethods.ThrowIfError(result);
            }
            catch
            {
                NativeMethods.FreeMemory(handle);
                throw;
            }

            GCHandle gcHandle = GCHandle.Alloc(this, GCHandleType.Normal);
            ((uv_handle_t*)handle)->data = GCHandle.ToIntPtr(gcHandle);
            this.Handle = handle;
        }

        internal void Bind(IPEndPoint endPoint, bool dualStack = false)
        {
            Debug.Assert(endPoint != null);

            this.Validate();
            NativeMethods.GetSocketAddress(endPoint, out sockaddr addr);
            int result = NativeMethods.uv_tcp_bind(this.Handle, ref addr, (uint)(dualStack ? 1 : 0));
            NativeMethods.ThrowIfError(result);
        }

        public IPEndPoint GetLocalEndPoint()
        {
            this.Validate();
            return NativeMethods.TcpGetSocketName(this.Handle);
        }

        public void NoDelay(int value)
        {
            this.Validate();
            int result = NativeMethods.uv_tcp_nodelay(this.Handle, value);
            NativeMethods.ThrowIfError(result);
        }

        public int SendBufferSize(int value)
        {
            Contract.Requires(value >= 0);

            this.Validate();
            var size = (IntPtr)value;
            int result = NativeMethods.uv_send_buffer_size(this.Handle, ref size);
            NativeMethods.ThrowIfError(result);

            return size.ToInt32();
        }

        public int ReceiveBufferSize(int value)
        {
            Contract.Requires(value >= 0);

            this.Validate();
            var size = (IntPtr)value;

            int result = NativeMethods.uv_recv_buffer_size(this.Handle, ref size);
            NativeMethods.ThrowIfError(result);

            return size.ToInt32();
        }

        public void KeepAlive(int value, int delay)
        {
            this.Validate();
            int result = NativeMethods.uv_tcp_keepalive(this.Handle, value, delay);
            NativeMethods.ThrowIfError(result);
        }

        public void SimultaneousAccepts(bool value)
        {
            this.Validate();
            int result = NativeMethods.uv_tcp_simultaneous_accepts(this.Handle, value ? 1 : 0);
            NativeMethods.ThrowIfError(result);
        }
    }
}
