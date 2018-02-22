// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Telnet.Server
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;

    public class TelnetServerHandler : SimpleChannelInboundHandler<string>
    {
        public override void ChannelActive(IChannelHandlerContext contex)
        {
            contex.WriteAsync(string.Format("Welcome to {0} !\r\n", Dns.GetHostName()));
            contex.WriteAndFlushAsync(string.Format("It is {0} now !\r\n", DateTime.Now));
        }

        protected override async void ChannelRead0(IChannelHandlerContext contex, string msg)
        {
            // Generate and write a response.
            string response;
            bool close = false;
            if (string.IsNullOrEmpty(msg))
            {
                response = "Please type something.\r\n";
            }
            else if (string.Equals("bye", msg, StringComparison.OrdinalIgnoreCase))
            {
                response = "Have a good day!\r\n";
                close = true;
            }
            else
            {
                response = "Did you say '" + msg + "'?\r\n";
            }

            ChannelFuture wait_close = contex.WriteAndFlushAsync(response);
            if (close)
            {
                await wait_close;
                contex.CloseAsync();
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext contex)
        {
            contex.Flush();
        }

        public override void ExceptionCaught(IChannelHandlerContext contex, Exception e)
        {
            Console.WriteLine("{0}", e.StackTrace);
            contex.CloseAsync();
        }

        public override bool IsSharable => true;
    }
}