// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Echo.Server
{
    using System;
    using System.Diagnostics;
    using System.Text;
    using System.Threading;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    public class EchoServerHandler : ChannelHandlerAdapter
    {
        int i = 0;
        readonly Stopwatch sw = Stopwatch.StartNew();

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            var buffer = message as IByteBuffer;
            if (buffer != null)
            {
                //Console.WriteLine("Received from client: " + buffer.ToString(Encoding.UTF8));
                if (Interlocked.Increment(ref this.i) % 10000 == 0)
                {
                    this.sw.Stop();

                    Console.WriteLine(this.sw.ElapsedMilliseconds);

                    this.sw.Restart();
                }
            }
            context.WriteAsync(message);
        }

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine("Exception: " + exception);
            context.CloseAsync();
        }
    }
}