// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.IPFilter
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Numerics;
    using DotNetty.Common.Internal;

    /// <summary>
    /// Use this class to create rules for <see cref="RuleBasedIPFilter"/> that group IP addresses into subnets.
    /// Supports both, IPv4 and IPv6.
    /// </summary>
    public class IPSubnetFilterRule : IIPFilterRule
    {
        readonly IIPFilterRule filterRule;

        public IPSubnetFilterRule(string ipAddress, int cidrPrefix, IPFilterRuleType ruleType)
        {
            this.filterRule = SelectFilterRule(SocketUtils.AddressByName(ipAddress), cidrPrefix, ruleType);
        }

        public IPSubnetFilterRule(IPAddress ipAddress, int cidrPrefix, IPFilterRuleType ruleType)
        {
            this.filterRule = SelectFilterRule(ipAddress, cidrPrefix, ruleType);
        }

        public IPFilterRuleType RuleType => this.filterRule.RuleType;

        public bool Matches(IPEndPoint remoteAddress)
        {
            return this.filterRule.Matches(remoteAddress);
        }

        static IIPFilterRule SelectFilterRule(IPAddress ipAddress, int cidrPrefix, IPFilterRuleType ruleType)
        {
            if (ipAddress == null)
            {
                throw new ArgumentNullException(nameof(ipAddress));
            }

            if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
            {
                return new IP4SubnetFilterRule(ipAddress, cidrPrefix, ruleType);
            }
            else if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return new IP6SubnetFilterRule(ipAddress, cidrPrefix, ruleType);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(ipAddress), "Only IPv4 and IPv6 addresses are supported");
            }
        }

        private class IP4SubnetFilterRule : IIPFilterRule
        {
            readonly int networkAddress;
            readonly int subnetMask;

            public IP4SubnetFilterRule(IPAddress ipAddress, int cidrPrefix, IPFilterRuleType ruleType)
            {
                if (cidrPrefix < 0 || cidrPrefix > 32)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(cidrPrefix),
                        string.Format(
                            "IPv4 requires the subnet prefix to be in range of " +
                            "[0,32]. The prefix was: {0}",
                            cidrPrefix));
                }

                this.subnetMask = PrefixToSubnetMask(cidrPrefix);
                this.networkAddress = GetNetworkAddress(ipAddress, this.subnetMask);
                this.RuleType = ruleType;
            }

            public IPFilterRuleType RuleType { get; }

            public bool Matches(IPEndPoint remoteAddress)
            {
                if (remoteAddress.AddressFamily == AddressFamily.InterNetwork)
                {
                    return GetNetworkAddress(remoteAddress.Address, this.subnetMask) == this.networkAddress;
                }
                return false;
            }

            static int GetNetworkAddress(IPAddress ipAddress, int subnetMask)
            {
                return IpToInt(ipAddress) & subnetMask;
            }

            static int PrefixToSubnetMask(int cidrPrefix)
            {
                /*
                 * Perform the shift on a long and downcast it to int afterwards.
                 * This is necessary to handle a cidrPrefix of zero correctly.
                 * The left shift operator on an int only uses the five least
                 * significant bits of the right-hand operand. Thus -1 << 32 evaluates
                 * to -1 instead of 0. The left shift operator applied on a long
                 * uses the six least significant bits.
                 *
                 * Also see https://github.com/netty/netty/issues/2767
                 */
                return (int)((-1L << 32 - cidrPrefix) & 0xffffffff);
            }

            static int IpToInt(IPAddress ipAddress)
            {
                byte[] octets = ipAddress.GetAddressBytes();
                if (octets.Length != 4)
                {
                    throw new ArgumentOutOfRangeException(nameof(ipAddress), "Octets count must be equal 4 for IPv4 address.");
                }

                return (octets[0] & 0xff) << 24 |
                    (octets[1] & 0xff) << 16 |
                    (octets[2] & 0xff) << 8 |
                    octets[3] & 0xff;
            }
        }

        private class IP6SubnetFilterRule : IIPFilterRule
        {
            readonly BigInteger networkAddress;
            readonly BigInteger subnetMask;

            public IP6SubnetFilterRule(IPAddress ipAddress, int cidrPrefix, IPFilterRuleType ruleType)
            {
                if (cidrPrefix < 0 || cidrPrefix > 128)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(cidrPrefix),
                        string.Format(
                            "IPv6 requires the subnet prefix to be in range of " +
                            "[0,128]. The prefix was: {0}",
                            cidrPrefix));
                }

                this.subnetMask = CidrToSubnetMask((byte)cidrPrefix);
                this.networkAddress = GetNetworkAddress(ipAddress, this.subnetMask);
                this.RuleType = ruleType;
            }

            public IPFilterRuleType RuleType { get; }

            public bool Matches(IPEndPoint remoteAddress)
            {
                if (remoteAddress.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    return this.networkAddress == GetNetworkAddress(remoteAddress.Address, this.subnetMask);
                }
                return false;
            }

            static BigInteger CidrToSubnetMask(byte cidr)
            {
                var mask = new BigInteger(
                    new byte[]
                    {
                        0xff, 0xff, 0xff, 0xff,
                        0xff, 0xff, 0xff, 0xff,
                        0xff, 0xff, 0xff, 0xff,
                        0xff, 0xff, 0xff, 0xff,
                        0x00
                    });


                BigInteger masked = cidr == 0 ? 0 : mask << (128 - cidr);
                byte[] m = masked.ToByteArray();
                var bmask = new byte[16];
                int copy = m.Length > 16 ? 16 : m.Length;
                Array.Copy(m, 0, bmask, 0, copy);
                byte[] resBytes = bmask.Reverse().ToArray();
                return new BigInteger(resBytes);
            }

            static BigInteger IpToInt(IPAddress ipAddress)
            {
                byte[] octets = ipAddress.GetAddressBytes();
                if (octets.Length != 16)
                {
                    throw new ArgumentOutOfRangeException(nameof(ipAddress), "Octets count must be equal 16 for IPv6 address.");
                }
                return new BigInteger(octets);
            }

            static BigInteger GetNetworkAddress(IPAddress ipAddress, BigInteger subnetMask)
            {
                return IpToInt(ipAddress) & subnetMask;
            }
        }
    }
}