// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace QuoteOfTheMoment.Client
{
    using System;
    using System.Text;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;

    public class QuoteOfTheMomentClientHandler : SimpleChannelInboundHandler<DatagramPacket>
    {
        protected override void ChannelRead0(IChannelHandlerContext ctx, DatagramPacket packet)
        {
            Console.WriteLine($"Client Received => {packet}");

            if (!packet.Content.IsReadable())
            {
                return;
            }

            string message = packet.Content.ToString(Encoding.UTF8);
            if (!message.StartsWith("QOTM: "))
            {
                return;
            }

            Console.WriteLine($"Quote of the Moment: {message.Substring(6)}");
            ctx.CloseAsync();
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine("Exception: " + exception);
            context.CloseAsync();
        }
    }
}