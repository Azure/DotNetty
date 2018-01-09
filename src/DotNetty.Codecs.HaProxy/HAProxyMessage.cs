﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.HaProxy
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Util;

    using static DotNetty.Codecs.HaProxy.HAProxyProxiedProtocol;

    /**
     * Message container for decoded HAProxy proxy protocol parameters
     */
    public sealed class HAProxyMessage
    {

        /**
         * Version 1 proxy protocol message for 'UNKNOWN' proxied protocols. Per spec, when the proxied protocol is
         * 'UNKNOWN' we must discard all other header values.
         */
        private static readonly HAProxyMessage V1_UNKNOWN_MSG = new HAProxyMessage(
                HAProxyProtocolVersion.V1, HAProxyCommand.PROXY, HAProxyProxiedProtocol.UNKNOWN, null, null, 0, 0);

        /**
         * Version 2 proxy protocol message for 'UNKNOWN' proxied protocols. Per spec, when the proxied protocol is
         * 'UNKNOWN' we must discard all other header values.
         */
        private static readonly HAProxyMessage V2_UNKNOWN_MSG = new HAProxyMessage(
                HAProxyProtocolVersion.V2, HAProxyCommand.PROXY, HAProxyProxiedProtocol.UNKNOWN, null, null, 0, 0);

        /**
         * Version 2 proxy protocol message for local requests. Per spec, we should use an unspecified protocol and family
         * for 'LOCAL' commands. Per spec, when the proxied protocol is 'UNKNOWN' we must discard all other header values.
         */
        private static readonly HAProxyMessage V2_LOCAL_MSG = new HAProxyMessage(
                HAProxyProtocolVersion.V2, HAProxyCommand.LOCAL, HAProxyProxiedProtocol.UNKNOWN, null, null, 0, 0);

        private readonly HAProxyProtocolVersion protocolVersion;
        private readonly HAProxyCommand command;
        private readonly HAProxyProxiedProtocol proxiedProtocol;
        private readonly string sourceAddress;
        private readonly string destinationAddress;
        private readonly int sourcePort;
        private readonly int destinationPort;
        private readonly IList<HAProxyTLV> tlvs;

        /**
         * Creates a new instance
         */
        private HAProxyMessage(
                HAProxyProtocolVersion protocolVersion, HAProxyCommand command, HAProxyProxiedProtocol proxiedProtocol,
                string sourceAddress, string destinationAddress, string sourcePort, string destinationPort)
                : this(
                    protocolVersion, command, proxiedProtocol,
                    sourceAddress, destinationAddress, PortStringToInt(sourcePort), PortStringToInt(destinationPort))
        {
        }

        /**
         * Creates a new instance
         */
        private HAProxyMessage(
                HAProxyProtocolVersion protocolVersion, HAProxyCommand command, HAProxyProxiedProtocol proxiedProtocol,
                string sourceAddress, string destinationAddress, int sourcePort, int destinationPort)
                : this(protocolVersion, command, proxiedProtocol,
                 sourceAddress, destinationAddress, sourcePort, destinationPort, new List<HAProxyTLV>())
        {
        }

        /**
         * Creates a new instance
         */
        private HAProxyMessage(
                HAProxyProtocolVersion protocolVersion, HAProxyCommand command, HAProxyProxiedProtocol proxiedProtocol,
                string sourceAddress, string destinationAddress, int sourcePort, int destinationPort,
                List<HAProxyTLV> tlvs)
        {

            if (proxiedProtocol == null)
            {
                throw new ArgumentNullException(nameof(proxiedProtocol));
            }
            AddressFamily addrFamily = proxiedProtocol.AddressFamilyType();

            CheckAddress(sourceAddress, addrFamily);
            CheckAddress(destinationAddress, addrFamily);
            CheckPort(sourcePort);
            CheckPort(destinationPort);

            this.protocolVersion = protocolVersion;
            this.command = command;
            this.proxiedProtocol = proxiedProtocol;
            this.sourceAddress = sourceAddress;
            this.destinationAddress = destinationAddress;
            this.sourcePort = sourcePort;
            this.destinationPort = destinationPort;
            this.tlvs = tlvs.AsReadOnly();
        }

        /**
         * Decodes a version 2, binary proxy protocol header.
         *
         * @param header                     a version 2 proxy protocol header
         * @return                           {@link HAProxyMessage} instance
         * @throws HAProxyProtocolException  if any portion of the header is invalid
         */
        internal static HAProxyMessage DecodeHeader(IByteBuffer header)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }

            if (header.ReadableBytes < 16)
            {
                throw new HAProxyProtocolException(
                        "incomplete header: " + header.ReadableBytes + " bytes (expected: 16+ bytes)");
            }

            // Per spec, the 13th byte is the protocol version and command byte
            header.SkipBytes(12);
            byte verCmdByte = header.ReadByte();

            HAProxyProtocolVersion ver;
            try
            {
                ver = HAProxyProtocolVersion.ValueOf(verCmdByte);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new HAProxyProtocolException(e);
            }

            if (ver != HAProxyProtocolVersion.V2)
            {
                throw new HAProxyProtocolException("version 1 unsupported: 0x" + verCmdByte.ToString("x"));
            }

            HAProxyCommand cmd;
            try
            {
                cmd = HAProxyCommand.ValueOf(verCmdByte);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new HAProxyProtocolException(e);
            }

            if (cmd == HAProxyCommand.LOCAL)
            {
                return V2_LOCAL_MSG;
            }

            // Per spec, the 14th byte is the protocol and address family byte
            HAProxyProxiedProtocol protAndFam;
            try
            {
                protAndFam = HAProxyProxiedProtocol.ValueOf(header.ReadByte());
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new HAProxyProtocolException(e);
            }

            if (protAndFam == HAProxyProxiedProtocol.UNKNOWN)
            {
                return V2_UNKNOWN_MSG;
            }

            int addressInfoLen = header.ReadUnsignedShort();

            string srcAddress;
            string dstAddress;
            int addressLen;
            int srcPort = 0;
            int dstPort = 0;

            AddressFamily addressFamily = protAndFam.AddressFamilyType();

            if (addressFamily == AddressFamily.AF_UNIX)
            {
                // unix sockets require 216 bytes for address information
                if (addressInfoLen < 216 || header.ReadableBytes < 216)
                {
                    throw new HAProxyProtocolException(
                        "incomplete UNIX socket address information: " +
                                Math.Min(addressInfoLen, header.ReadableBytes) + " bytes (expected: 216+ bytes)");
                }
                int startIdx = header.ReaderIndex;
                int addressEnd = header.ForEachByte(startIdx, 108, ByteProcessor.FindNul);
                if (addressEnd == -1)
                {
                    addressLen = 108;
                }
                else
                {
                    addressLen = addressEnd - startIdx;
                }
                srcAddress = header.ToString(startIdx, addressLen, Encoding.ASCII);

                startIdx += 108;

                addressEnd = header.ForEachByte(startIdx, 108, ByteProcessor.FindNul);
                if (addressEnd == -1)
                {
                    addressLen = 108;
                }
                else
                {
                    addressLen = addressEnd - startIdx;
                }
                dstAddress = header.ToString(startIdx, addressLen, Encoding.ASCII);
                // AF_UNIX defines that exactly 108 bytes are reserved for the address. The previous methods
                // did not increase the reader index although we already consumed the information.
                header.SetReaderIndex(startIdx + 108);
            }
            else
            {
                if (addressFamily == AddressFamily.AF_IPv4)
                {
                    // IPv4 requires 12 bytes for address information
                    if (addressInfoLen < 12 || header.ReadableBytes < 12)
                    {
                        throw new HAProxyProtocolException(
                            "incomplete IPv4 address information: " +
                                    Math.Min(addressInfoLen, header.ReadableBytes) + " bytes (expected: 12+ bytes)");
                    }
                    addressLen = 4;
                }
                else if (addressFamily == AddressFamily.AF_IPv6)
                {
                    // IPv6 requires 36 bytes for address information
                    if (addressInfoLen < 36 || header.ReadableBytes < 36)
                    {
                        throw new HAProxyProtocolException(
                            "incomplete IPv6 address information: " +
                                    Math.Min(addressInfoLen, header.ReadableBytes) + " bytes (expected: 36+ bytes)");
                    }
                    addressLen = 16;
                }
                else
                {
                    throw new HAProxyProtocolException(
                        "unable to parse address information (unknown address family: " + addressFamily + ')');
                }

                // Per spec, the src address begins at the 17th byte
                srcAddress = IpBytesToString(header, addressLen);
                dstAddress = IpBytesToString(header, addressLen);
                srcPort = header.ReadUnsignedShort();
                dstPort = header.ReadUnsignedShort();
            }

            List<HAProxyTLV> tlvs = ReadTlvs(header);

            return new HAProxyMessage(ver, cmd, protAndFam, srcAddress, dstAddress, srcPort, dstPort, tlvs);
        }

        private static List<HAProxyTLV> ReadTlvs(IByteBuffer header)
        {
            HAProxyTLV haProxyTLV = ReadNextTLV(header);
            if (haProxyTLV == null)
            {
                return new List<HAProxyTLV>();
            }
            // In most cases there are less than 4 TLVs available
            List<HAProxyTLV> haProxyTLVs = new List<HAProxyTLV>(4);

            do
            {
                haProxyTLVs.Add(haProxyTLV);
                var sslProxy = haProxyTLV as HAProxySSLTLV;
                if (sslProxy != null) {
                    haProxyTLVs.AddRange(sslProxy.EncapsulatedTLVs());
                }
            } while ((haProxyTLV = ReadNextTLV(header)) != null);
            return haProxyTLVs;
        }

        private static HAProxyTLV ReadNextTLV(IByteBuffer header)
        {

            // We need at least 4 bytes for a TLV
            if (header.ReadableBytes < 4)
            {
                return null;
            }

            byte typeAsByte = header.ReadByte();
            HAProxyTLV.Type type = HAProxyTLV.TypeForByteValue(typeAsByte);

            int length = header.ReadUnsignedShort();
            switch (type)
            {
                case HAProxyTLV.Type.PP2_TYPE_SSL:
                    IByteBuffer rawContent = header.RetainedSlice(header.ReaderIndex, length);
                    IByteBuffer byteBuf = header.ReadSlice(length);
                    byte client = byteBuf.ReadByte();
                    int verify = byteBuf.ReadInt();

                    if (byteBuf.ReadableBytes >= 4)
                    {

                        List<HAProxyTLV> encapsulatedTlvs = new List<HAProxyTLV>(4);
                        do
                        {
                            HAProxyTLV haProxyTLV = ReadNextTLV(byteBuf);
                            if (haProxyTLV == null)
                            {
                                break;
                            }
                            encapsulatedTlvs.Add(haProxyTLV);
                        } while (byteBuf.ReadableBytes >= 4);

                        return new HAProxySSLTLV(verify, client, encapsulatedTlvs, rawContent);
                    }
                    return new HAProxySSLTLV(verify, client, new List<HAProxyTLV>(), rawContent);
                // If we're not dealing with a SSL Type, we can use the same mechanism
                case HAProxyTLV.Type.PP2_TYPE_ALPN:
                case HAProxyTLV.Type.PP2_TYPE_AUTHORITY:
                case HAProxyTLV.Type.PP2_TYPE_SSL_VERSION:
                case HAProxyTLV.Type.PP2_TYPE_SSL_CN:
                case HAProxyTLV.Type.PP2_TYPE_NETNS:
                case HAProxyTLV.Type.OTHER:
                    return new HAProxyTLV(type, typeAsByte, header.ReadRetainedSlice(length));
                default:
                    return null;
            }
        }

        /**
         * Decodes a version 1, human-readable proxy protocol header.
         *
         * @param header                     a version 1 proxy protocol header
         * @return                           {@link HAProxyMessage} instance
         * @throws HAProxyProtocolException  if any portion of the header is invalid
         */
        internal static HAProxyMessage DecodeHeader(string header)
        {
            if (header == null)
            {
                throw new HAProxyProtocolException("header");
            }

            string[] parts = header.Split(' ');
            int numParts = parts.Length;

            if (numParts < 2)
            {
                throw new HAProxyProtocolException(
                        "invalid header: " + header + " (expected: 'PROXY' and proxied protocol values)");
            }

            if (!"PROXY".Equals(parts[0]))
            {
                throw new HAProxyProtocolException("unknown identifier: " + parts[0]);
            }

            HAProxyProxiedProtocol protAndFam;
            try
            {
                protAndFam = HAProxyProxiedProtocol.ValueOf(parts[1]);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new HAProxyProtocolException(e);
            }

            if (protAndFam != HAProxyProxiedProtocol.TCP4 &&
                    protAndFam != HAProxyProxiedProtocol.TCP6 &&
                    protAndFam != HAProxyProxiedProtocol.UNKNOWN)
            {
                throw new HAProxyProtocolException("unsupported v1 proxied protocol: " + parts[1]);
            }

            if (protAndFam == HAProxyProxiedProtocol.UNKNOWN)
            {
                return V1_UNKNOWN_MSG;
            }

            if (numParts != 6)
            {
                throw new HAProxyProtocolException("invalid TCP4/6 header: " + header + " (expected: 6 parts)");
            }

            return new HAProxyMessage(
                    HAProxyProtocolVersion.V1, HAProxyCommand.PROXY,
                    protAndFam, parts[2], parts[3], parts[4], parts[5]);
        }

        /**
         * Convert ip address bytes to string representation
         *
         * @param header     buffer containing ip address bytes
         * @param addressLen number of bytes to read (4 bytes for IPv4, 16 bytes for IPv6)
         * @return           string representation of the ip address
         */
        private static string IpBytesToString(IByteBuffer header, int addressLen)
        {
            StringBuilder sb = new StringBuilder();
            if (addressLen == 4)
            {
                sb.Append(header.ReadByte() & 0xff);
                sb.Append('.');
                sb.Append(header.ReadByte() & 0xff);
                sb.Append('.');
                sb.Append(header.ReadByte() & 0xff);
                sb.Append('.');
                sb.Append(header.ReadByte() & 0xff);
            }
            else
            {
                sb.Append(header.ReadUnsignedShort().ToString("x"));
                sb.Append(':');
                sb.Append(header.ReadUnsignedShort().ToString("x"));
                sb.Append(':');
                sb.Append(header.ReadUnsignedShort().ToString("x"));
                sb.Append(':');
                sb.Append(header.ReadUnsignedShort().ToString("x"));
                sb.Append(':');
                sb.Append(header.ReadUnsignedShort().ToString("x"));
                sb.Append(':');
                sb.Append(header.ReadUnsignedShort().ToString("x"));
                sb.Append(':');
                sb.Append(header.ReadUnsignedShort().ToString("x"));
                sb.Append(':');
                sb.Append(header.ReadUnsignedShort().ToString("x"));
            }
            return sb.ToString();
        }

        /**
         * Convert port to integer
         *
         * @param value                      the port
         * @return                           port as an integer
         * @throws HAProxyProtocolException  if port is not a valid integer
         */
        private static int PortStringToInt(string value)
        {
            int port;
            try
            {
                port = int.Parse(value);
            }
            catch (FormatException e)
            {
                throw new HAProxyProtocolException(e);
            }

            if (port <= 0 || port > 65535)
            {
                throw new HAProxyProtocolException("invalid port: " + value + " (expected: 1 ~ 65535)");
            }

            return port;
        }

        /**
         * Validate an address (IPv4, IPv6, Unix Socket)
         *
         * @param address                    human-readable address
         * @param addrFamily                 the {@link AddressFamily} to check the address against
         * @throws HAProxyProtocolException  if the address is invalid
         */
        private static void CheckAddress(string address, AddressFamily addrFamily)
        {
            if (addrFamily == null)
            {
                throw new ArgumentNullException(nameof(addrFamily));
            }

            if (addrFamily == AddressFamily.AF_UNIX)
            {
                return;
            }
            else if(addrFamily == AddressFamily.AF_UNSPEC)
            {
                if (address != null)
                {
                    throw new HAProxyProtocolException("unable to validate an AF_UNSPEC address: " + address);
                }
                return;
            }

            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            if (addrFamily == AddressFamily.AF_IPv4)
            {
                if (!NetUtil.IsValidIpV4Address(address))
                {
                    throw new HAProxyProtocolException("invalid IPv4 address: " + address);
                }
            }
            else if (addrFamily == AddressFamily.AF_IPv6)
            {
                if (!NetUtil.IsValidIpV6Address(address))
                {
                    throw new HAProxyProtocolException("invalid IPv6 address: " + address);
                }
            }
            else
            {
                throw new HAProxyProtocolException("Not possible");
            }
        }

        /**
         * Validate a UDP/TCP port
         *
         * @param port                       the UDP/TCP port
         * @throws HAProxyProtocolException  if the port is out of range (0-65535 inclusive)
         */
        private static void CheckPort(int port)
        {
            if (port < 0 || port > 65535)
            {
                throw new HAProxyProtocolException("invalid port: " + port + " (expected: 1 ~ 65535)");
            }
        }

        /**
         * Returns the {@link HAProxyProtocolVersion} of this {@link HAProxyMessage}.
         */
        public HAProxyProtocolVersion ProtocolVersion()
        {
            return this.protocolVersion;
        }

        /**
         * Returns the {@link HAProxyCommand} of this {@link HAProxyMessage}.
         */
        public HAProxyCommand Command()
        {
            return this.command;
        }

        /**
         * Returns the {@link HAProxyProxiedProtocol} of this {@link HAProxyMessage}.
         */
        public HAProxyProxiedProtocol ProxiedProtocol()
        {
            return this.proxiedProtocol;
        }

        /**
         * Returns the human-readable source address of this {@link HAProxyMessage}.
         */
        public string SourceAddress()
        {
            return this.sourceAddress;
        }

        /**
         * Returns the human-readable destination address of this {@link HAProxyMessage}.
         */
        public string DestinationAddress()
        {
            return this.destinationAddress;
        }

        /**
         * Returns the UDP/TCP source port of this {@link HAProxyMessage}.
         */
        public int SourcePort()
        {
            return this.sourcePort;
        }

        /**
         * Returns the UDP/TCP destination port of this {@link HAProxyMessage}.
         */
        public int DestinationPort()
        {
            return this.destinationPort;
        }

        /**
         * Returns a list of {@link HAProxyTLV} or an empty list if no TLVs are present.
         * <p>
         * TLVs are only available for the Proxy Protocol V2
         */
        public IList<HAProxyTLV> Tlvs()
        {
            return this.tlvs;
        }
    }
}
