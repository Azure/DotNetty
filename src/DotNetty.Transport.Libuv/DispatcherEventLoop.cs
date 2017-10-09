// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    public sealed class DispatcherEventLoop : LoopExecutor, IEventLoop
    {
        readonly TaskCompletionSource pipeStartTaskCompletionSource;

        PipeListener pipeListener;
        IServerNativeUnsafe nativeUnsafe;

        public DispatcherEventLoop(IEventLoopGroup parent = null, string threadName = null)
            : base(parent, threadName)
        {
            string pipeName = "DotNetty_" + Guid.NewGuid().ToString("n");
            this.PipeName = (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                ? @"\\.\pipe\" : "/tmp/") + pipeName;
            this.pipeStartTaskCompletionSource = new TaskCompletionSource();
            this.Start();
        }

        internal string PipeName { get; }

        internal Task PipeStartTask => this.pipeStartTaskCompletionSource.Task;

        internal void Register(IServerNativeUnsafe serverChannel)
        {
            Contract.Requires(serverChannel != null);

            this.nativeUnsafe = serverChannel;
        }

        protected override void Initialize()
        {
            try
            {
                Loop loop = ((ILoopExecutor)this).UnsafeLoop;
                this.pipeListener = new PipeListener(loop, false);
                this.pipeListener.Listen(this.PipeName);

                if (Logger.InfoEnabled)
                {
                    Logger.Info("{} ({}) listening on pipe {}.", nameof(DispatcherEventLoop), this.LoopThreadId, this.PipeName);
                }

                this.pipeStartTaskCompletionSource.TryComplete();
            }
            catch (Exception error)
            {
                this.pipeStartTaskCompletionSource.TrySetException(error);
                throw;
            }
        }

        protected override void Shutdown()
        {
            this.pipeListener.Shutdown();
            base.Shutdown();
        }

        internal void Dispatch(NativeHandle handle)
        {
            try
            {
                this.pipeListener.DispatchHandle(handle);
            }
            catch
            {
                handle.CloseHandle();
                throw;
            }
        }

        internal void Accept(NativeHandle handle)
        {
            this.nativeUnsafe.Accept(handle);
        }

        public Task RegisterAsync(IChannel channel) => channel.Unsafe.RegisterAsync(this);

        public new IEventLoopGroup Parent => (IEventLoopGroup)base.Parent;
    }
}
