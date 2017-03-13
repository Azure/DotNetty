// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Factorial.Server
{
    using System;
    using System.Numerics;
    using DotNetty.Transport.Channels;

    public class FactorialServerHandler : SimpleChannelInboundHandler<BigInteger>
    {
        BigInteger lastMultiplier = new BigInteger(1);
        BigInteger factorial = new BigInteger(1);

        protected override void ChannelRead0(IChannelHandlerContext ctx, BigInteger msg)
        {
            this.lastMultiplier = msg;
            this.factorial *= msg;
            ctx.WriteAndFlushAsync(this.factorial);
        }

        public override void ChannelInactive(IChannelHandlerContext ctx) => Console.WriteLine("Factorial of {0} is: {1}", this.lastMultiplier, this.factorial);

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception e) => ctx.CloseAsync();
    }
}