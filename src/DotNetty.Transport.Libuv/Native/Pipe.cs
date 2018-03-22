// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// IPC pipe for recieving handles from different libuv loops
    /// </summary>
    sealed unsafe class Pipe : PipeHandle
    {
        static readonly uv_alloc_cb AllocateCallback = OnAllocateCallback;
        static readonly uv_read_cb ReadCallback = OnReadCallback;

        readonly Scratch scratch;

        Action<Pipe, int> readCallback;

        internal Pipe(Loop loop, bool ipc) : base(loop, ipc)
        {
            this.scratch = new Scratch();
        }

        public void ReadStart(Action<Pipe, int> readAction)
        {
            this.Validate();

            int result = NativeMethods.uv_read_start(this.Handle, AllocateCallback, ReadCallback);
            NativeMethods.ThrowIfError(result);
            this.readCallback = readAction;
        }

        public void ReadStop()
        {
            if (this.Handle == IntPtr.Zero)
            {
                return;
            }

            // This function is idempotent and may be safely called on a stopped stream.
            int result = NativeMethods.uv_read_stop(this.Handle);
            NativeMethods.ThrowIfError(result);
        }

        void OnReadCallback(int status) => this.readCallback(this, status);

        internal Tcp GetPendingHandle()
        {
            Tcp client = null;

            IntPtr loopHandle = ((uv_stream_t*)this.Handle)->loop;
            var loop = GetTarget<Loop>(loopHandle);
            int count = NativeMethods.uv_pipe_pending_count(this.Handle);

            if (count > 0)
            {
                var type = (uv_handle_type)NativeMethods.uv_pipe_pending_type(this.Handle);
                if (type == uv_handle_type.UV_TCP)
                {
                    client = new Tcp(loop);
                }
                else
                {
                    throw new InvalidOperationException($"Expecting tcp handle, {type} not supported");
                }

                int result = NativeMethods.uv_accept(this.Handle, client.Handle);
                NativeMethods.ThrowIfError(result);
            }

            return client;
        }

        internal void Send(NativeHandle serverHandle)
        {
            Debug.Assert(serverHandle != null);

            var ping = new Ping(serverHandle);
            uv_buf_t[] bufs = ping.Bufs;
            int result = NativeMethods.uv_write2(
                ping.Handle,
                this.Handle,
                bufs,
                bufs.Length,
                serverHandle.Handle,
                Ping.WriteCallback);

            if (result < 0)
            {
                ping.Dispose();
                NativeMethods.ThrowOperationException((uv_err_code)result);
            }
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            this.scratch.Dispose();
        }

        static void OnReadCallback(IntPtr handle, IntPtr nread, ref uv_buf_t buf)
        {
            var pipe = GetTarget<Pipe>(handle);
            pipe.OnReadCallback((int)nread.ToInt64());
        }

        static void OnAllocateCallback(IntPtr handle, IntPtr suggestedSize, out uv_buf_t buf)
        {
            var pipe = GetTarget<Pipe>(handle);
            pipe.OnAllocateCallback(out buf);
        }

        void OnAllocateCallback(out uv_buf_t buf)
        {
            buf = this.scratch.Buf;
        }

        sealed class Scratch : IDisposable
        {
            static readonly byte[] ScratchBuf = new byte[64];
            GCHandle gcHandle;

            public Scratch()
            {
                byte[] scratch = ScratchBuf;
                this.gcHandle = GCHandle.Alloc(scratch, GCHandleType.Pinned);
                IntPtr arrayHandle = this.gcHandle.AddrOfPinnedObject();
                this.Buf = new uv_buf_t(arrayHandle, scratch.Length);
            }

            internal readonly uv_buf_t Buf;

            public void Dispose()
            {
                if (this.gcHandle.IsAllocated)
                {
                    this.gcHandle.Free();
                }
            }
        }

        sealed class Ping : NativeRequest
        {
            internal static readonly uv_watcher_cb WriteCallback = OnWriteCallback;
            static readonly byte[] PingBuf = Encoding.UTF8.GetBytes("PING");

            readonly NativeHandle sentHandle;
            GCHandle gcHandle;

            public Ping(NativeHandle sentHandle) : base(uv_req_type.UV_WRITE, 0)
            {
                this.sentHandle = sentHandle;
                byte[] array = PingBuf;
                this.Bufs = new uv_buf_t[1];

                GCHandle handle = GCHandle.Alloc(array, GCHandleType.Pinned);
                IntPtr arrayHandle = handle.AddrOfPinnedObject();
                this.gcHandle = handle;

                this.Bufs[0] = new uv_buf_t(arrayHandle, array.Length);
            }

            internal readonly uv_buf_t[] Bufs;

            void OnWriteCallback(int status)
            {
                if (status < 0)
                {
                    OperationException error = NativeMethods.CreateError((uv_err_code)status);
                    Logger.Warn($"{nameof(PipeListener)} failed to write server handle to client", error);
                }

                this.sentHandle.CloseHandle();
                this.Dispose();
            }

            static void OnWriteCallback(IntPtr handle, int status)
            {
                var request = GetTarget<Ping>(handle);
                request.OnWriteCallback(status);
            }

            protected override void Dispose(bool disposing)
            {
                if (this.gcHandle.IsAllocated)
                {
                    this.gcHandle.Free();
                }
                base.Dispose(disposing);
            }
        }
    }
}
