// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Echo.Server
{
    using System;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    public class EchoServerHandler : ChannelHandlerAdapter
    {
        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            var buffer = message as IByteBuffer;
            if (buffer != null)
            {
                Console.WriteLine("Received from client: " + buffer.ToString(Encoding.UTF8));
            }
            var bf = Unpooled.Buffer(256);
            byte[] messageBytes = Encoding.UTF8.GetBytes("Server Say Hi");
            bf.WriteBytes(messageBytes);
            // var Unpooled.Buffer(ClientSettings.Size);

            context.WriteAsync(bf);
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            //Console.WriteLine("ChannelReadComplete 读完事件！");
            context.Flush();
        }
        //=> context.Flush();

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine("Exception: " + exception);
            context.CloseAsync();
        }
    }
}