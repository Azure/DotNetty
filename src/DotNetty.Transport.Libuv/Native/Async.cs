// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;

    sealed unsafe class Async : NativeHandle
    {
        static readonly uv_work_cb WorkCallback = OnWorkCallback;

        readonly Action<object> callback;
        readonly object state;

        public Async(Loop loop, Action<object> callback, object state)
            : base(uv_handle_type.UV_ASYNC)
        {
            Debug.Assert(loop != null);
            Debug.Assert(callback != null);

            IntPtr handle = NativeMethods.Allocate(uv_handle_type.UV_ASYNC);
            try
            {
                int result = NativeMethods.uv_async_init(loop.Handle, handle, WorkCallback);
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
            this.callback = callback;
            this.state = state;
        }

        public void Send()
        {
            if (!this.IsValid)
            {
                return;
            }

            int result = NativeMethods.uv_async_send(this.Handle);
            NativeMethods.ThrowIfError(result);
        }

        void OnWorkCallback()
        {
            try
            {
                this.callback(this.state);
            }
            catch (Exception exception)
            {
                Logger.Error($"{this.HandleType} {this.Handle} callback error.", exception);
            }
        }

        static void OnWorkCallback(IntPtr handle)
        {
            var workHandle = GetTarget<Async>(handle);
            workHandle?.OnWorkCallback();
        }
    }
}
