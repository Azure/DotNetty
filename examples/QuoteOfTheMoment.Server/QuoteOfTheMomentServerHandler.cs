// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace QuoteOfTheMoment.Server
{
    using System;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;

    public class QuoteOfTheMomentServerHandler : SimpleChannelInboundHandler<DatagramPacket>
    {
        static readonly Random Random = new Random();

        // Quotes from Mohandas K. Gandhi:
        static readonly string[] Quotes =
        {
            "Where there is love there is life.",
            "First they ignore you, then they laugh at you, then they fight you, then you win.",
            "Be the change you want to see in the world.",
            "The weak can never forgive. Forgiveness is the attribute of the strong.",
        };

        static string NextQuote()
        {
            int quoteId = Random.Next(Quotes.Length);
            return Quotes[quoteId];
        }

        protected override void ChannelRead0(IChannelHandlerContext ctx, DatagramPacket packet)
        {
            Console.WriteLine($"Server Received => {packet}");

            if (!packet.Content.IsReadable())
            {
                return;
            }

            string message = packet.Content.ToString(Encoding.UTF8);
            if (message != "QOTM?")
            {
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes("QOTM: " + NextQuote());
            IByteBuffer buffer = Unpooled.WrappedBuffer(bytes);
            ctx.WriteAsync(new DatagramPacket(buffer, packet.Sender));
        }

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine("Exception: " + exception);
            context.CloseAsync();
        }
    }
}