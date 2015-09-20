// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System.Diagnostics.Contracts;

    sealed class DefaultChannelHandlerContext : AbstractChannelHandlerContext
    {
        readonly IChannelHandler handler;

        public DefaultChannelHandlerContext(
            DefaultChannelPipeline pipeline, IChannelHandlerInvoker invoker, string name, IChannelHandler handler)
            : base(pipeline, invoker, name, GetSkipPropagationFlags(handler))
        {
            Contract.Requires(handler != null);

            this.handler = handler;
        }

        public override IChannelHandler Handler
        {
            get { return this.handler; }
        }
    }
}