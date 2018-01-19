// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;

    /// <summary>
    /// IPC pipe server to hand out handles to different libuv loops.
    /// </summary>
    sealed class PipeListener : PipeHandle
    {
        const int DefaultPipeBacklog = 128;
        static readonly uv_watcher_cb ConnectionCallback = OnConnectionCallback;

        readonly List<Pipe> pipes;
        readonly WindowsApi windowsApi;
        int requestId;

        public PipeListener(Loop loop, bool ipc) : base(loop, ipc)
        {
            this.pipes = new List<Pipe>();
            this.windowsApi = new WindowsApi();
            this.requestId = 0;
        }

        public void Listen(string name, int backlog = DefaultPipeBacklog)
        {
            Debug.Assert(backlog > 0);

            this.Validate();
            int result = NativeMethods.uv_pipe_bind(this.Handle, name);
            NativeMethods.ThrowIfError(result);

            result = NativeMethods.uv_listen(this.Handle, backlog, ConnectionCallback);
            NativeMethods.ThrowIfError(result);
        }

        internal void Shutdown()
        {
            Pipe[] handles = this.pipes.ToArray();
            this.pipes.Clear();

            foreach (Pipe pipe in handles)
            {
                pipe.CloseHandle();
            }

            this.CloseHandle();
        }

        internal void DispatchHandle(NativeHandle handle)
        {
            if (this.pipes.Count == 0)
            {
                throw new InvalidOperationException("No pipe connections to dispatch handles.");
            }

            int id = Interlocked.Increment(ref this.requestId);
            Pipe pipe = this.pipes[Math.Abs(id % this.pipes.Count)];

            this.windowsApi.DetachFromIOCP(handle);
            pipe.Send(handle);
        }

        unsafe void OnConnectionCallback(int status)
        {
            Pipe client = null;
            try
            {
                if (status < 0)
                {
                    throw NativeMethods.CreateError((uv_err_code)status);
                }
                else
                {
                    IntPtr loopHandle = ((uv_stream_t*)this.Handle)->loop;
                    var loop = GetTarget<Loop>(loopHandle);

                    client = new Pipe(loop, true); // IPC pipe
                    int result = NativeMethods.uv_accept(this.Handle, client.Handle);
                    NativeMethods.ThrowIfError(result);

                    this.pipes.Add(client);
                    client.ReadStart(this.OnRead);
                }
            }
            catch (Exception exception)
            {
                client?.CloseHandle();
                Logger.Warn($"{nameof(PipeListener)} failed to send server handle to client", exception);
            }
        }

        void OnRead(Pipe pipe, int status)
        {
            // The server connection is never meant to read anything back
            // it is only used for passing handles over to different loops
            // Therefore the only message should come back is EOF
            if (status >= 0)
            {
                return;
            }

            this.windowsApi.Dispose();
            this.pipes.Remove(pipe);
            pipe.CloseHandle();

            if (status != NativeMethods.EOF)
            {
                OperationException error = NativeMethods.CreateError((uv_err_code)status);
                Logger.Warn($"{nameof(PipeListener)} read error", error);
            }
        }

        static void OnConnectionCallback(IntPtr handle, int status)
        {
            var server = GetTarget<PipeListener>(handle);
            server.OnConnectionCallback(status);
        }
    }
}
