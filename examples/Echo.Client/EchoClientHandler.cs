// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using DotNetty.Buffers;
using DotNetty.Transport.Channels;

namespace Echo.Client
{
    using System;
    using System.Text;
    
    public class EchoClientHandler : ChannelHandlerAdapter
    {
        readonly IByteBuffer initialMessage;
        readonly byte[] buffer;

        public EchoClientHandler()
        {
            this.buffer = new byte[EchoClientSettings.Size];
            this.initialMessage = Unpooled.Buffer(EchoClientSettings.Size);
            byte[] messageBytes = Encoding.UTF8.GetBytes("Hello world");
            this.initialMessage.WriteBytes(messageBytes);
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            context.WriteAndFlushAsync(this.initialMessage);
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            var byteBuffer = message as IByteBuffer;
            if (byteBuffer != null)
            {
                this.buffer.Initialize();
                byteBuffer.ReadBytes(this.buffer, 0, byteBuffer.ReadableBytes);
                string msg = Encoding.UTF8.GetString(this.buffer);
                Console.WriteLine("Received from server: " + msg);
            }
            context.WriteAsync(message);
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            context.Flush();
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            context.CloseAsync();
        }
    }
}
