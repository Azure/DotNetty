// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SecureChat.Client
{
    using System;
    using DotNetty.Transport.Channels;

    public class SecureChatClientHandler : SimpleChannelInboundHandler<string>
    {
        protected override void ChannelRead0(IChannelHandlerContext contex, string msg) => Console.WriteLine(msg);

        public override void ExceptionCaught(IChannelHandlerContext contex, Exception e)
        {
            Console.WriteLine(DateTime.Now.Millisecond);
            Console.WriteLine(e.StackTrace);
            contex.CloseAsync();
        }
    }
}