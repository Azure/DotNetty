// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Runtime.InteropServices;

    sealed class Timer : NativeHandle
    {
        static readonly uv_work_cb WorkCallback = OnWorkCallback;

        readonly Action<object> callback;
        readonly object state;

        public unsafe Timer(Loop loop, Action<object> callback, object state) 
            : base(uv_handle_type.UV_TIMER)
        {
            Contract.Requires(loop != null);
            Contract.Requires(callback != null);

            int size = NativeMethods.uv_handle_size(uv_handle_type.UV_TIMER).ToInt32();
            IntPtr handle = Marshal.AllocHGlobal(size);

            int result;
            try
            {
                result = NativeMethods.uv_timer_init(loop.Handle, handle);
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
            this.callback = callback;
            this.state = state;
        }

        public Timer Start(long timeout, long repeat)
        {
            Contract.Requires(timeout >= 0);
            Contract.Requires(repeat >= 0);

            this.Validate();
            int result = NativeMethods.uv_timer_start(this.Handle, WorkCallback, timeout, repeat);
            if (result < 0)
            {
                throw NativeMethods.CreateError((uv_err_code)result);
            }

            return this;
        }

        public Timer SetRepeat(long repeat)
        {
            Contract.Requires(repeat >= 0);

            this.Validate();
            NativeMethods.uv_timer_set_repeat(this.Handle, repeat);

            return this;
        }

        public long GetRepeat()
        {
            this.Validate();
            return NativeMethods.uv_timer_get_repeat(this.Handle);
        }

        public Timer Again()
        {
            this.Validate();
            int result = NativeMethods.uv_timer_again(this.Handle);
            if (result < 0)
            {
                throw NativeMethods.CreateError((uv_err_code)result);
            }

            return this;
        }

        public void Stop()
        {
            if (!this.IsValid)
            {
                return;
            }

            int result = NativeMethods.uv_timer_stop(this.Handle);
            if (result < 0)
            {
                throw NativeMethods.CreateError((uv_err_code)result);
            }
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
            var workHandle = GetTarget<Timer>(handle);
            workHandle?.OnWorkCallback();
        }
    }
}
