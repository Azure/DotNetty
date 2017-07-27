// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Runtime.InteropServices;

    abstract unsafe class PipeHandle : NativeHandle
    {
        const int NameBufferSize = 512;

        protected PipeHandle(Loop loop, bool ipc) : base(uv_handle_type.UV_NAMED_PIPE)
        {
            Contract.Requires(loop != null);

            int size = NativeMethods.uv_handle_size(uv_handle_type.UV_NAMED_PIPE).ToInt32();
            IntPtr handle = Marshal.AllocHGlobal(size);

            int result;
            try
            {
                result = NativeMethods.uv_pipe_init(loop.Handle, handle, ipc ? 1 : 0);
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

        public void Bind(string name)
        {
            Contract.Requires(!string.IsNullOrEmpty(name));

            this.Validate();
            int result = NativeMethods.uv_pipe_bind(this.Handle, name);
            if (result < 0)
            {
                throw NativeMethods.CreateError((uv_err_code)result);
            }
        }

        public string GetSocketName()
        {
            this.Validate();
            string socketName;
            IntPtr buf = IntPtr.Zero;
            try
            {
                buf = Marshal.AllocHGlobal(NameBufferSize);
                var length = (IntPtr)NameBufferSize;

                int result = NativeMethods.uv_pipe_getsockname(this.Handle, buf, ref length);
                if (result < 0)
                {
                    throw NativeMethods.CreateError((uv_err_code)result);
                }

                socketName = Marshal.PtrToStringAnsi(buf, length.ToInt32());
            }
            finally
            {
                if (buf != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buf);
                }
            }

            return socketName;
        }

        public string GetPeerName()
        {
            this.Validate();
            string peerName;
            IntPtr buf = IntPtr.Zero;
            try
            {
                buf = Marshal.AllocHGlobal(NameBufferSize);
                var length = (IntPtr)NameBufferSize;

                int result = NativeMethods.uv_pipe_getpeername(this.Handle, buf, ref length);
                if (result < 0)
                {
                    throw NativeMethods.CreateError((uv_err_code)result);
                }

                peerName = Marshal.PtrToStringAnsi(buf, length.ToInt32());
            }
            finally
            {
                if (buf != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buf);
                }
            }

            return peerName;
        }
    }
}
