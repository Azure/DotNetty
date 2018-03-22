// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;

    abstract unsafe class PipeHandle : NativeHandle
    {
        const int NameBufferSize = 512;

        protected PipeHandle(Loop loop, bool ipc) : base(uv_handle_type.UV_NAMED_PIPE)
        {
            Debug.Assert(loop != null);

            IntPtr handle = NativeMethods.Allocate(uv_handle_type.UV_NAMED_PIPE);
            try
            {
                int result = NativeMethods.uv_pipe_init(loop.Handle, handle, ipc ? 1 : 0);
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

        public void Bind(string name)
        {
            Debug.Assert(!string.IsNullOrEmpty(name));

            this.Validate();
            int result = NativeMethods.uv_pipe_bind(this.Handle, name);
            NativeMethods.ThrowIfError(result);
        }

        public string GetSocketName()
        {
            this.Validate();
            var buf = stackalloc byte[NameBufferSize];
            var length = (IntPtr)NameBufferSize;
            var ptr = (IntPtr)buf;

            int result = NativeMethods.uv_pipe_getsockname(this.Handle, ptr, ref length);
            NativeMethods.ThrowIfError(result);

            string socketName = Marshal.PtrToStringAnsi(ptr, length.ToInt32());
            return socketName;
        }

        public string GetPeerName()
        {
            this.Validate();

            var buf = stackalloc byte[NameBufferSize];
            var length = (IntPtr)NameBufferSize;
            var ptr = (IntPtr)buf;

            int result = NativeMethods.uv_pipe_getpeername(this.Handle, ptr, ref length);
            NativeMethods.ThrowIfError(result);

            string peerName = Marshal.PtrToStringAnsi(ptr, length.ToInt32());
            return peerName;
        }
    }
}
