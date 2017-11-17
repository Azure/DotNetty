// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Local
{
    using System;
    using System.Net;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;

    /**
 * A {@link ServerChannel} for the local transport which allows in VM communication.
 */
    public class LocalServerChannel : AbstractServerChannel
    {
        readonly IQueue<object> inboundBuffer = PlatformDependent.NewMpscQueue<object>();

        volatile int state; // 0 - open, 1 - active, 2 - closed
        volatile LocalAddress localAddress;
        volatile bool acceptInProgress;

        readonly Action shutdownHook;

        public LocalServerChannel()
        {
            this.shutdownHook = () => this.Unsafe.CloseAsync();
            //this.Configuration.Allocator(new PreferHeapByteBufAllocator(config.getAllocator()));
            this.Configuration = new DefaultChannelConfiguration(this);
        }

        public override IChannelConfiguration Configuration { get; }

        public override bool Open => this.state < 2;

        public override bool Active => this.state == 1;

        protected override EndPoint LocalAddressInternal => this.localAddress;

        protected override bool IsCompatible(IEventLoop eventLoop) => eventLoop is SingleThreadEventLoop;

        public new LocalAddress LocalAddress => (LocalAddress)base.LocalAddress;

        public new LocalAddress RemoteAddress => (LocalAddress)base.RemoteAddress;

        protected override void DoRegister() =>
            ((SingleThreadEventExecutor)this.EventLoop).AddShutdownHook(this.shutdownHook);

        protected override void DoBind(EndPoint localAddress)
        {
            this.localAddress = LocalChannelRegistry.Register(this, this.localAddress, localAddress);
            this.state = 1;
        }

        protected override void DoClose()
        {
            if (this.state <= 1)
            {
                // Update all internal state before the closeFuture is notified.
                if (this.localAddress != null)
                {
                    LocalChannelRegistry.Unregister(this.localAddress);
                    this.localAddress = null;
                }
                this.state = 2;
            }
        }

        protected override void DoDeregister()
            => ((SingleThreadEventExecutor)this.EventLoop).RemoveShutdownHook(this.shutdownHook);

        protected override void DoBeginRead()
        {
            if (this.acceptInProgress)
            {
                return;
            }

            IQueue<object> inboundBuffer = this.inboundBuffer;

            if (inboundBuffer.IsEmpty)
            {
                this.acceptInProgress = true;
                return;
            }

            IChannelPipeline pipeline = this.Pipeline;
            for (;;)
            {
                if (!inboundBuffer.TryDequeue(out object m))
                {
                    break;
                }

                pipeline.FireChannelRead(m);
            }

            pipeline.FireChannelReadComplete();
        }

        public LocalChannel Serve(LocalChannel peer)
        {
            LocalChannel child = this.NewLocalChannel(peer);
            if (this.EventLoop.InEventLoop)
            {
                this.Serve0(child);
            }
            else
            {
                this.EventLoop.Execute(() => this.Serve0(child));
            }
            return child;
        }

        /**
         * A factory method for {@link LocalChannel}s. Users may override it
         * to create custom instances of {@link LocalChannel}s.
         */
        protected LocalChannel NewLocalChannel(LocalChannel peer) => new LocalChannel(this, peer);

        void Serve0(LocalChannel child)
        {
            this.inboundBuffer.TryEnqueue(child);

            if (this.acceptInProgress)
            {
                this.acceptInProgress = false;
                IChannelPipeline pipeline = this.Pipeline;

                for (;;)
                {
                    if (!this.inboundBuffer.TryDequeue(out object m))
                    {
                        break;
                    }

                    pipeline.FireChannelRead(m);
                }

                pipeline.FireChannelReadComplete();
            }
        }
    }
}