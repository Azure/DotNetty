// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using DotNetty.Common.Internal.Logging;
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    abstract unsafe class NativeHandle : IDisposable
    {
        protected static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<NativeHandle>();
        static readonly uv_close_cb CloseCallback = OnCloseHandle;
        protected readonly uv_handle_type HandleType;
        internal IntPtr Handle;

        protected NativeHandle(uv_handle_type handleType)
        {
            this.HandleType = handleType;
        }

        internal IntPtr LoopHandle()
        {
            this.Validate();
            return ((uv_handle_t*)this.Handle)->loop;
        }

        protected bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.Handle != IntPtr.Zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void Validate()
        {
            if (!this.IsValid)
            {
                NativeMethods.ThrowObjectDisposedException($"{this.GetType()}");
            }
        }

        internal void RemoveReference()
        {
            this.Validate();
            NativeMethods.uv_unref(this.Handle);
        }

        internal bool IsActive => this.IsValid && NativeMethods.uv_is_active(this.Handle) > 0;

        internal void CloseHandle()
        {
            IntPtr handle = this.Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            int result = NativeMethods.uv_is_closing(handle);
            if (result == 0)
            {
                NativeMethods.uv_close(handle, CloseCallback);
            }
        }

        protected virtual void OnClosed()
        {
            this.Handle = IntPtr.Zero;
        }

        static void OnCloseHandle(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            NativeHandle nativeHandle = null;

            // Get gc handle first
            IntPtr pHandle = ((uv_handle_t*)handle)->data;
            if (pHandle != IntPtr.Zero)
            {
                GCHandle gcHandle = GCHandle.FromIntPtr(pHandle);
                if (gcHandle.IsAllocated)
                {
                    nativeHandle = gcHandle.Target as NativeHandle;
                    gcHandle.Free();

                    ((uv_handle_t*)handle)->data = IntPtr.Zero;
                }
            }

            // Release memory
            NativeMethods.FreeMemory(handle);
            nativeHandle?.OnClosed();
        }

        void Dispose(bool disposing)
        {
            try
            {
                if (this.IsValid)
                {
                    this.CloseHandle();
                }
            }
            catch (Exception exception)
            {
                Logger.Error($"{nameof(NativeHandle)} {this.Handle} error whilst closing handle.", exception);
                // For finalizer, we cannot allow this to escape.
                if (disposing) throw;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~NativeHandle() => this.Dispose(false);

        internal static T GetTarget<T>(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            IntPtr inernalHandle = ((uv_handle_t*)handle)->data;
            if (inernalHandle != IntPtr.Zero)
            {
                GCHandle gcHandle = GCHandle.FromIntPtr(inernalHandle);
                if (gcHandle.IsAllocated)
                {
                    return (T)gcHandle.Target;
                }
            }
            return default(T);
        }
    }
}
