// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Discard.Client
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using Examples.Common;

    public class DiscardClientHandler : SimpleChannelInboundHandler<object>
    {
        IChannelHandlerContext ctx;
        byte[] array;

        public override void ChannelActive(IChannelHandlerContext ctx)
        {
            this.array = new byte[ClientSettings.Size];
            this.ctx = ctx;

            // Send the initial messages.
            this.GenerateTraffic();
        }

        protected override void ChannelRead0(IChannelHandlerContext context, object message)
        {
            // Server is supposed to send nothing, but if it sends something, discard it.
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception e)
        {
            Console.WriteLine("{0}", e.ToString());
            this.ctx.CloseAsync();
        }

        async void GenerateTraffic()
        {
            try
            {
                IByteBuffer buffer = Unpooled.WrappedBuffer(this.array);
                // Flush the outbound buffer to the socket.
                // Once flushed, generate the same amount of traffic again.
                await this.ctx.WriteAndFlushAsync(buffer);
                this.GenerateTraffic();
            }
            catch
            {
                await this.ctx.CloseAsync();
            }
        }
    }
}