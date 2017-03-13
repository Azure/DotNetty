// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using DotNetty.Common.Utilities;

    public abstract class SimpleChannelInboundHandler<I> : ChannelHandlerAdapter
    {
        readonly bool autoRelease;

        protected SimpleChannelInboundHandler() : this(true)
        {
        }

        protected SimpleChannelInboundHandler(bool autoRelease)
        {
            this.autoRelease = autoRelease;
        }

        public bool AcceptInboundMessage(object msg) => msg is I;

        public override void ChannelRead(IChannelHandlerContext ctx, object msg)
        {
            bool release = true;
            try
            {
                if (this.AcceptInboundMessage(msg))
                {
                    I imsg = (I)msg;
                    this.ChannelRead0(ctx, imsg);
                }
                else
                {
                    release = false;
                    ctx.FireChannelRead(msg);
                }
            }
            finally
            {
                if (autoRelease && release)
                {
                    ReferenceCountUtil.Release(msg);
                }
            }
        }

        protected abstract void ChannelRead0(IChannelHandlerContext ctx, I msg);
    }
}
