// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    using System.Net;
    using System.Net.Sockets;

    public class SocketUtils
    {
        public static IPAddress AddressByName(string hostname)
        {
            if (string.IsNullOrEmpty(hostname))
            {
                bool isIPv6Supported = Socket.OSSupportsIPv6;
                if (isIPv6Supported)
                {
                    return IPAddress.IPv6Loopback;
                }
                else
                {
                    return IPAddress.Loopback;
                }
            }
            if (hostname == "0.0.0.0")
            {
                return IPAddress.Any;
            }
            if (hostname == "::0" || hostname == "::")
            {
                return IPAddress.IPv6Any;
            }
            if (IPAddress.TryParse(hostname, out IPAddress parseResult))
            {
                return parseResult;
            }
            IPHostEntry hostEntry = Dns.GetHostEntryAsync(hostname).Result;
            return hostEntry.AddressList[0];
        }
    }
}