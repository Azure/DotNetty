// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace DotNetty.Transport.Libuv.Native
{
    using DotNetty.Common.Internal.Logging;
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    sealed unsafe class Loop : IDisposable
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<Loop>();
        static readonly uv_walk_cb WalkCallback = OnWalkCallback;

        IntPtr handle;

        public Loop()
        {
            IntPtr loopHandle = NativeMethods.Allocate(NativeMethods.uv_loop_size().ToInt32());
            try
            {
                int result = NativeMethods.uv_loop_init(loopHandle);
                NativeMethods.ThrowIfError(result);
            }
            catch
            {
                NativeMethods.FreeMemory(loopHandle);
                throw;
            }

            GCHandle gcHandle = GCHandle.Alloc(this, GCHandleType.Normal);
            ((uv_loop_t*)loopHandle)->data = GCHandle.ToIntPtr(gcHandle);
            this.handle = loopHandle;
            if (Logger.InfoEnabled)
            {
                Logger.Info($"Loop {this.handle} allocated.");
            }
        }

        internal IntPtr Handle => this.handle;

        public bool IsAlive => this.handle != IntPtr.Zero && NativeMethods.uv_loop_alive(this.handle) != 0;

        public void UpdateTime()
        {
            this.Validate();
            NativeMethods.uv_update_time(this.Handle);
        }

        public long Now
        {
            get
            {
                this.Validate();
                return NativeMethods.uv_now(this.handle);
            }
        }

        public long NowInHighResolution
        {
            get
            {
                this.Validate();
                return NativeMethods.uv_hrtime(this.handle);
            }
        }

        public int GetBackendTimeout()
        {
            this.Validate();
            return NativeMethods.uv_backend_timeout(this.handle);
        }

        public int ActiveHandleCount() => 
            this.handle != IntPtr.Zero
            ? (int)((uv_loop_t*)this.handle)->active_handles 
            : 0;

        public int Run(uv_run_mode mode)
        {
            this.Validate();
            return NativeMethods.uv_run(this.handle, mode);
        }

        public void Stop()
        {
            if (this.handle != IntPtr.Zero)
            {
                NativeMethods.uv_stop(this.handle);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Validate()
        {
            if (this.handle == IntPtr.Zero)
            {
                NativeMethods.ThrowObjectDisposedException($"{this.GetType()}");
            }
        }

        public void Dispose()
        {
            this.Close();
            GC.SuppressFinalize(this);
        }

        void Close()
        {
            IntPtr loopHandle = this.handle;
            Close(loopHandle);
            this.handle = IntPtr.Zero;
        }

        static void Close(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            // Get gc handle before close loop
            IntPtr pHandle = ((uv_loop_t*)handle)->data;

            // Fully close the loop, similar to 
            //https://github.com/libuv/libuv/blob/v1.x/test/task.h#L190

            int count = 0;
            while (true)
            {
                Logger.Debug($"Loop {handle} walking handles, count = {count}.");
                NativeMethods.uv_walk(handle, WalkCallback, handle);

                Logger.Debug($"Loop {handle} running default to call close callbacks, count = {count}.");
                NativeMethods.uv_run(handle, uv_run_mode.UV_RUN_DEFAULT);

                int result = NativeMethods.uv_loop_close(handle);
                Logger.Debug($"Loop {handle} close result = {result}, count = {count}.");
                if (result == 0)
                {
                    break;
                }

                count++;
                if (count >= 20)
                {
                    Logger.Warn($"Loop {handle} close all handles limit 20 times exceeded.");
                    break;
                }
            }
            Logger.Info($"Loop {handle} closed, count = {count}.");

            // Free GCHandle
            if (pHandle != IntPtr.Zero)
            {
                GCHandle nativeHandle = GCHandle.FromIntPtr(pHandle);
                if (nativeHandle.IsAllocated)
                {
                    nativeHandle.Free();
                    ((uv_loop_t*)handle)->data = IntPtr.Zero;
                    Logger.Info($"Loop {handle} GCHandle released.");
                }
            }

            // Release memory
            NativeMethods.FreeMemory(handle);
            Logger.Info($"Loop {handle} memory released.");
        }

        static void OnWalkCallback(IntPtr handle, IntPtr loopHandle)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // All handles must implement IDisposable
                var target = NativeHandle.GetTarget<IDisposable>(handle);
                target?.Dispose();
                Logger.Debug($"Loop {loopHandle} walk callback disposed {handle} {target?.GetType()}");
            }
            catch (Exception exception)
            {
                Logger.Warn($"Loop {loopHandle} Walk callback attempt to close handle {handle} failed. {exception}");
            }
        }

        ~Loop() => this.Close();
    }
}
