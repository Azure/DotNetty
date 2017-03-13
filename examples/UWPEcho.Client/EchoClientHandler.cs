// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace UWPEcho.Client
{
    using System;
    using System.Diagnostics;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    public class EchoClientHandler : ChannelHandlerAdapter
    {
        readonly IByteBuffer initialMessage;
        readonly Action<object> logger;

        public EchoClientHandler(Action<object> logger)
        {
            this.logger = logger;
            this.initialMessage = Unpooled.Buffer(256);
            byte[] messageBytes = Encoding.UTF8.GetBytes("Hello from UWP!");
            this.initialMessage.WriteBytes(messageBytes);
        }

        public override void ChannelActive(IChannelHandlerContext context) => context.WriteAndFlushAsync(this.initialMessage);

        public override async void ChannelRead(IChannelHandlerContext context, object message)
        {
            try
            {
                var byteBuffer = message as IByteBuffer;
                if (byteBuffer != null)
                {
                    string str = byteBuffer.ToString(Encoding.UTF8);
                    Debug.WriteLine("Received from server: " + str);
                    this.logger(str);
                }
                await context.WriteAsync(message);
                // Throttle the client:
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                string str = string.Format("Error reading from channel: {0}", ex.Message);
                this.logger(str);
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) => context.CloseAsync();
    }
}