// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Tests.End2End
{
    using System;
    using System.Diagnostics.Contracts;
    using DotNetty.Transport.Channels;

    class ExceptionCatchHandler : ChannelHandlerAdapter
    {
        readonly Action<Exception> exceptionCaughtAction;

        public ExceptionCatchHandler(Action<Exception> exceptionCaughtAction)
        {
            Contract.Requires(exceptionCaughtAction != null);
            this.exceptionCaughtAction = exceptionCaughtAction;
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) => this.exceptionCaughtAction(exception);
    }
}