// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Text;
    using System.Collections.Generic;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    public static class MacAddressUtil
    {
        /// Length of a valid MAC address.
        public const int MacAddressLength = 8;

        static readonly byte[] NotFound = { byte.MaxValue };

        static readonly IInternalLogger logger = InternalLoggerFactory.GetInstance(typeof(MacAddressUtil));

        /// Obtains the best MAC address found on local network interfaces.
        /// Generally speaking, an active network interface used on public
        /// networks is better than a local network interface.
        /// <returns>byte array containing a MAC. null if no MAC can be found.</returns>
        public static byte[] GetBestAvailableMac()
        {
            // Find the best MAC address available.
            byte[] bestMacAddr = NotFound;
            IPAddress bestInetAddr = IPAddress.Loopback;

            // Retrieve the list of available network interfaces.
            Dictionary<NetworkInterface, IPAddress> ifaces = new Dictionary<NetworkInterface, IPAddress>();
            try
            {
                foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    // Use the interface with proper INET addresses only.
                    var addrs = iface.GetIPProperties().UnicastAddresses;
                    if (addrs.Count > 0)
                    {
                        var addressInfo = addrs.First();
                        if (!IPAddress.IsLoopback(addressInfo.Address))
                        {
                            ifaces.Add(iface, addressInfo.Address);
                        }
                    }
                }
            }
            catch (SocketException e)
            {
                logger.Warn("Failed to retrieve the list of available network interfaces", e);
            }

            foreach (var entry in ifaces)
            {
                NetworkInterface iface = entry.Key;
                IPAddress inetAddr = entry.Value;
                // todo: netty has a check for whether interface is virtual but it always returns false. There is no equivalent in .NET
                byte[] macAddr = iface.GetPhysicalAddress()?.GetAddressBytes();
                bool replace = false;
                int res = CompareAddresses(bestMacAddr, macAddr);
                if (res < 0)
                {
                    // Found a better MAC address.
                    replace = true;
                }
                else if (res == 0)
                {
                    // Two MAC addresses are of pretty much same quality.
                    res = CompareAddresses(bestInetAddr, inetAddr);
                    if (res < 0)
                    {
                        // Found a MAC address with better INET address.
                        replace = true;
                    }
                    else if (res == 0)
                    {
                        // Cannot tell the difference.  Choose the longer one.
                        if (bestMacAddr.Length < macAddr.Length)
                        {
                            replace = true;
                        }
                    }
                }

                if (replace)
                {
                    bestMacAddr = macAddr;
                    bestInetAddr = inetAddr;
                }
            }

            if (bestMacAddr == NotFound)
            {
                return null;
            }

            switch (bestMacAddr.Length)
            {
                case 6: // EUI-48 - convert to EUI-64
                    var newAddr = new byte[MacAddressLength];
                    Array.Copy(bestMacAddr, 0, newAddr, 0, 3);
                    newAddr[3] = 0xFF;
                    newAddr[4] = 0xFE;
                    Array.Copy(bestMacAddr, 3, newAddr, 5, 3);
                    bestMacAddr = newAddr;
                    break;
                default: // Unknown
                    bestMacAddr = bestMacAddr.Slice(0, Math.Min(bestMacAddr.Length, MacAddressLength));
                    break;
            }

            return bestMacAddr;
        }

        /// <param name="addr">byte array of a MAC address.</param>
        /// <returns>hex formatted MAC address.</returns>
        public static string FormatAddress(byte[] addr)
        {
            StringBuilder buf = new StringBuilder(24);
            foreach (byte b in addr)
            {
                buf.Append((b & 0xFF).ToString("X2")).Append(":");
            }
            return buf.ToString(0, buf.Length - 1);
        }

        /// <returns>positive - current is better, 0 - cannot tell from MAC addr, negative - candidate is better.</returns>
        static int CompareAddresses(byte[] current, byte[] candidate)
        {
            if (candidate == null)
            {
                return 1;
            }

            // Must be EUI-48 or longer.
            if (candidate.Length < 6)
            {
                return 1;
            }

            // Must not be filled with only 0 and 1.
            bool onlyZeroAndOne = true;
            foreach (byte b in candidate)
            {
                if (b != 0 && b != 1)
                {
                    onlyZeroAndOne = false;
                    break;
                }
            }

            if (onlyZeroAndOne)
            {
                return 1;
            }

            // Must not be a multicast address
            if ((candidate[0] & 1) != 0)
            {
                return 1;
            }

            // Prefer globally unique address.
            if ((current[0] & 2) == 0)
            {
                if ((candidate[0] & 2) == 0)
                {
                    // Both current and candidate are globally unique addresses.
                    return 0;
                }
                else
                {
                    // Only current is globally unique.
                    return 1;
                }
            }
            else
            {
                if ((candidate[0] & 2) == 0)
                {
                    // Only candidate is globally unique.
                    return -1;
                }
                else
                {
                    // Both current and candidate are non-unique.
                    return 0;
                }
            }
        }

        /// <returns>positive - current is better, 0 - cannot tell, negative - candidate is better</returns>
        static int CompareAddresses(IPAddress current, IPAddress candidate) => ScoreAddress(current) - ScoreAddress(candidate);

        static int ScoreAddress(IPAddress addr)
        {
            if (IPAddress.IsLoopback(addr))
            {
                return 0;
            }
            if (addr.IsIPv6Multicast)
            {
                return 1;
            }
            if (addr.IsIPv6LinkLocal)
            {
                return 2;
            }
            if (addr.IsIPv6SiteLocal)
            {
                return 3;
            }

            return 4;
        }
    }
}