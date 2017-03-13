// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Bootstrapping
{
    using System.Net;
    using System.Threading.Tasks;

    public class DefaultNameResolver : INameResolver
    {
        public bool IsResolved(EndPoint address) => !(address is DnsEndPoint);

        public async Task<EndPoint> ResolveAsync(EndPoint address)
        {
            var asDns = address as DnsEndPoint;
            if (asDns != null)
            {
                IPHostEntry resolved = await Dns.GetHostEntryAsync(asDns.Host);
                return new IPEndPoint(resolved.AddressList[0], asDns.Port);
            }
            else
            {
                return address;
            }
        }
    }
}