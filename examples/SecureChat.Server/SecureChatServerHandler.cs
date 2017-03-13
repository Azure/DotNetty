// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SecureChat.Server
{
    using System;
    using System.Net;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Groups;

    public class SecureChatServerHandler : SimpleChannelInboundHandler<string>
    {
        static volatile IChannelGroup group;

        public override void ChannelActive(IChannelHandlerContext contex)
        {
            IChannelGroup g = group;
            if (g == null)
            {
                lock (this)
                {
                    if (group == null)
                    {
                        g = group = new DefaultChannelGroup(contex.Executor);
                    }
                }
            }

            contex.WriteAndFlushAsync(string.Format("Welcome to {0} secure chat server!\n", Dns.GetHostName()));
            g.Add(contex.Channel);
        }

        class EveryOneBut : IChannelMatcher
        {
            readonly IChannelId id;

            public EveryOneBut(IChannelId id)
            {
                this.id = id;
            }

            public bool Matches(IChannel channel) => channel.Id != this.id;
        }

        protected override void ChannelRead0(IChannelHandlerContext contex, string msg)
        {
            //send message to all but this one
            string broadcast = string.Format("[{0}] {1}\n", contex.Channel.RemoteAddress, msg);
            string response = string.Format("[you] {0}\n", msg);
            group.WriteAndFlushAsync(broadcast, new EveryOneBut(contex.Channel.Id));
            contex.WriteAndFlushAsync(response);

            if (string.Equals("bye", msg, StringComparison.OrdinalIgnoreCase))
            {
                contex.CloseAsync();
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext ctx) => ctx.Flush();

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception e)
        {
            Console.WriteLine("{0}", e.StackTrace);
            ctx.CloseAsync();
        }

        public override bool IsSharable => true;
    }
}