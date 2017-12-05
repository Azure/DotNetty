// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    sealed class WorkerEventLoop : LoopExecutor, IEventLoop
    {
        readonly TaskCompletionSource connectCompletion;
        readonly string pipeName;
        Pipe pipe;

        public WorkerEventLoop(WorkerEventLoopGroup parent) : base(parent, null)
        {
            Contract.Requires(parent != null);

            string name = parent.PipeName;
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Pipe name is required for worker event loop", nameof(parent));
            }

            this.pipeName = name;
            this.connectCompletion = new TaskCompletionSource();
            this.Start();
        }

        /// <summary>
        /// Awaitable for connecting to the dispatcher pipe.
        /// </summary>
        internal Task ConnectTask => this.connectCompletion.Task;

        protected override void Initialize()
        {
            Debug.Assert(this.pipe == null);

            this.pipe = new Pipe(this.UnsafeLoop, true);
            PipeConnect request = null;
            try
            {
                request = new PipeConnect(this);
            }
            catch (Exception exception)
            {
                Logger.Warn($"{nameof(WorkerEventLoop)} failed to create connect request to dispatcher", exception);
                request?.Dispose();
                this.connectCompletion.TryUnwrap(exception);
            }
        }

        protected override void Release() => this.pipe.CloseHandle();

        void OnConnected(ConnectRequest request)
        {
            try
            {
                if (request.Error != null)
                {
                    Logger.Warn($"{nameof(WorkerEventLoop)} failed to connect to dispatcher", request.Error);
                    this.connectCompletion.TryUnwrap(request.Error);
                }
                else
                {
                    if (Logger.InfoEnabled)
                    {
                        Logger.Info($"{nameof(WorkerEventLoop)} ({this.LoopThreadId}) dispatcher pipe {this.pipeName} connected.");
                    }

                    this.pipe.ReadStart(this.OnRead);
                    this.connectCompletion.TryComplete();
                }
            }
            finally
            {
                request.Dispose();
            }
        }

        void OnRead(Pipe handle, int status)
        {
            if (status < 0)
            {
                handle.CloseHandle();
                if (status != NativeMethods.EOF)
                {
                    OperationException error = NativeMethods.CreateError((uv_err_code)status);
                    Logger.Warn("IPC Pipe read error", error);
                }
            }
            else
            {
                Tcp tcp = handle.GetPendingHandle();
                ((WorkerEventLoopGroup)this.Parent).Accept(tcp);
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
                Debug.Assert(workerEventLoop != null);

                this.workerEventLoop = workerEventLoop;
                this.Connect();
                this.retryCount = 0;
            }

            protected override void OnWatcherCallback()
            {
                if (this.Error != null && this.retryCount < MaximumRetryCount)
                {
                    Logger.Info($"{nameof(WorkerEventLoop)} failed to connect to dispatcher, Retry count = {this.retryCount}", this.Error);
                    this.Connect();
                    this.retryCount++;
                }
                else
                {
                    this.workerEventLoop.OnConnected(this);
                }
            }

            void Connect() =>  NativeMethods.uv_pipe_connect(
                this.Handle,
                this.workerEventLoop.pipe.Handle,
                this.workerEventLoop.pipeName,
                WatcherCallback);
        }
    }
}
