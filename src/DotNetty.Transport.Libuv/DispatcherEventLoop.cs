// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    sealed class DispatcherEventLoop : LoopExecutor, IEventLoop
    {
        PipeListener pipeListener;
        TcpServerChannel.IServerNativeUnsafe nativeUnsafe;

        internal DispatcherEventLoop(IEventLoopGroup parent, string threadName = null)
            : base(parent, threadName)
        {
            Contract.Requires(parent != null);

            string pipeName = "DotNetty_" + Guid.NewGuid().ToString("n");
            this.PipeName = (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? @"\\.\pipe\"
                : "/tmp/") + pipeName;
            this.Start();
        }

        internal string PipeName { get; }

        internal void Register(TcpServerChannel.IServerNativeUnsafe serverChannel)
        {
            Debug.Assert(serverChannel != null);
            this.nativeUnsafe = serverChannel;
        }

        protected override void Initialize()
        {
            this.pipeListener = new PipeListener(this.UnsafeLoop, false);
            this.pipeListener.Listen(this.PipeName);

            if (Logger.InfoEnabled)
            {
                Logger.Info("{} ({}) listening on pipe {}.", nameof(DispatcherEventLoop), this.LoopThreadId, this.PipeName);
            }
        }

        protected override void Release() => this.pipeListener.Shutdown();

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

        internal void Accept(NativeHandle handle) => this.nativeUnsafe.Accept(handle);

        public new IEventLoop GetNext() => (IEventLoop)base.GetNext();

        public Task RegisterAsync(IChannel channel) => channel.Unsafe.RegisterAsync(this);

        public new IEventLoopGroup Parent => (IEventLoopGroup)base.Parent;

        public new IEnumerable<IEventLoop> Items => new[] { this };
    }
}