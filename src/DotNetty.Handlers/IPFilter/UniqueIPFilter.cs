// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace DotNetty.Handlers.IPFilter
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// This class allows one to ensure that at all times for every IP address there is at most one
    /// <see cref="IChannel"/>  connected to the server.
    /// </summary>
    public class UniqueIPFilter : AbstractRemoteAddressFilter<IPEndPoint>
    {
        const byte Filler = 0;
        //using dictionary as set. value always equals Filler.
        readonly IDictionary<IPAddress, byte> connected = new ConcurrentDictionary<IPAddress, byte>();

        protected override bool Accept(IChannelHandlerContext ctx, IPEndPoint remoteAddress)
        {
            IPAddress remoteIp = remoteAddress.Address;
            if (this.connected.ContainsKey(remoteIp))
            {
                return false;
            }
            else
            {
                this.connected.Add(remoteIp, Filler);
                ctx.Channel.CloseCompletion.ContinueWith(_ =>
                                            {
                                                this.connected.Remove(remoteIp);
                                            });
            }
            return true;
        }

        public override bool IsSharable => true;
    }
}