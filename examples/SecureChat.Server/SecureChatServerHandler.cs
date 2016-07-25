// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SecureChat.Server
{
    using System;
    using System.Net;
    using DotNetty.Transport.Channels.Groups;
    using DotNetty.Transport.Channels;

    public class SecureChatServerHandler : SimpleChannelInboundHandler<String>
    {
        static IChannelGroup group = null;

        public override void ChannelActive(IChannelHandlerContext contex)
        {
            lock (this)
            {
                if (group == null)
                {
                    group = new DefaultChannelGroup(contex.Executor);
                }
            }

            contex.WriteAndFlushAsync(String.Format("Welcome to {0} secure chat server!\n", Dns.GetHostName()));
            group.Add(contex.Channel);
        }

        class EveryOneBut : IChannelMatcher
        {
            IChannelId id;
            public EveryOneBut(IChannelId id)
            {
                this.id = id;
            }

            public bool Matches(IChannel channel)
            {
                return channel.Id != this.id;
            }
        }

        protected override void ChannelRead0(IChannelHandlerContext contex, string msg)
        {
            //send message to all but this one
            string broadcast = String.Format("[{0}] {1}\n", contex.Channel.RemoteAddress, msg);
            string response = String.Format("[you] {0}\n", msg);
            group.WriteAndFlushAsync(broadcast, new EveryOneBut(contex.Channel.Id));
            contex.WriteAndFlushAsync(response);

            if ("bye" == msg.ToLower())
            {
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
