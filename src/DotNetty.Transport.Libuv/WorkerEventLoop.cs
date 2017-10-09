// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    sealed class WorkerEventLoop : LoopExecutor, IEventLoop
    {
        readonly TaskCompletionSource connectCompletion;

        public WorkerEventLoop(WorkerEventLoopGroup parent) 
            : base(parent, null)
        {
            Contract.Requires(parent != null);

            string pipeName = parent.PipeName;
            if (string.IsNullOrEmpty(pipeName))
            {
                throw new ArgumentException("Pipe name is required for worker loops");
            }

            this.PipeName = pipeName;
            this.connectCompletion = new TaskCompletionSource();
        }

        internal string PipeName { get; }

        internal Task StartAsync()
        {
            this.Start();
            return this.connectCompletion.Task;
        }

        internal Pipe PipeHandle { get; private set; }

        protected override void Initialize()
        {
            Loop loop = ((ILoopExecutor)this).UnsafeLoop;
            this.PipeHandle = new Pipe(loop, true);
            PipeConnect request = null;

            try
            {
                request = new PipeConnect(this);
            }
            catch (Exception exception)
            {
                Logger.Warn($"{nameof(WorkerEventLoop)} failed to create connect request to dispatcher", exception);
                request?.Dispose();
                this.connectCompletion.TrySetException(exception);
            }
        }

        protected override void Shutdown()
        {
            this.PipeHandle.CloseHandle();
            base.Shutdown();
        } 

        void OnConnected(ConnectRequest request)
        {
            try
            {
                if (request.Error != null)
                {
                    Logger.Warn($"{nameof(WorkerEventLoop)} failed to connect to dispatcher", request.Error);
                    this.connectCompletion.TrySetException(request.Error);
                }
                else
                {
                    if (Logger.InfoEnabled)
                    {
                        Logger.Info($"{nameof(WorkerEventLoop)} ({this.LoopThreadId}) dispatcher pipe {this.PipeName} connected.");
                    }

                    this.PipeHandle.ReadStart(this.OnRead);
                    this.connectCompletion.TryComplete();
                }
            }
            finally
            {
                request.Dispose();
            }
        }

        void OnRead(Pipe pipe, int status)
        {
            if (status < 0)
            {
                pipe.CloseHandle();
                if (status != (int)uv_err_code.UV_EOF)
                {
                    OperationException error = NativeMethods.CreateError((uv_err_code)status);
                    Logger.Warn("IPC Pipe read error", error);
                }
            }
            else
            {
                Tcp handle = pipe.GetPendingHandle();
                ((WorkerEventLoopGroup)this.Parent).Accept(handle);
            }
        }

        public Task RegisterAsync(IChannel channel) => channel.Unsafe.RegisterAsync(this);

        public new IEventLoopGroup Parent => (IEventLoopGroup)base.Parent;

        sealed class PipeConnect : ConnectRequest
        {
            const int MaximumRetryCount = 10;

            readonly WorkerEventLoop workerEventLoop;
            int retryCount;

            public PipeConnect(WorkerEventLoop workerEventLoop)
            {
                Contract.Requires(workerEventLoop != null);

                this.workerEventLoop = workerEventLoop;
                this.DoConnect();
                this.retryCount = 0;
            }

            protected override void OnWatcherCallback()
            {
                if (this.Error != null && this.retryCount < MaximumRetryCount)
                {
                    Logger.Info($"{nameof(WorkerEventLoop)} failed to connect to dispatcher Retry count = {this.retryCount}", this.Error);

                    this.DoConnect();
                    this.retryCount++;

                    return;
                }

                this.workerEventLoop.OnConnected(this);
            }

            void DoConnect()
            {
                NativeMethods.uv_pipe_connect(
                    this.Handle,
                    this.workerEventLoop.PipeHandle.Handle,
                    this.workerEventLoop.PipeName,
                    WatcherCallback);
            }
        }
    }
}
