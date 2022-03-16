// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.HaProxy
{
    using System;
    using System.Collections.Generic;

    /**
     * A protocol proxied by HAProxy which is represented by its transport protocol and address family.
     */
    public sealed class HAProxyProxiedProtocol
    {
        static readonly Dictionary<string, HAProxyProxiedProtocol> NAME_LOOKUP = new Dictionary<string, HAProxyProxiedProtocol>();

        /**
         * The UNKNOWN represents a connection which was forwarded for an unknown protocol and an unknown address family.
         */
        public static readonly HAProxyProxiedProtocol UNKNOWN = new HAProxyProxiedProtocol("UNKNOWN", HAProxyConstants.TPAF_UNKNOWN_BYTE, AddressFamily.AF_UNSPEC, TransportProtocol.UNSPEC);
        /**
         * The TCP4 represents a connection which was forwarded for an IPv4 client over TCP.
         */
        public static readonly HAProxyProxiedProtocol TCP4 = new HAProxyProxiedProtocol("TCP4", HAProxyConstants.TPAF_TCP4_BYTE, AddressFamily.AF_IPv4, TransportProtocol.STREAM);
        /**
         * The TCP6 represents a connection which was forwarded for an IPv6 client over TCP.
         */
        public static readonly HAProxyProxiedProtocol TCP6 = new HAProxyProxiedProtocol("TCP6", HAProxyConstants.TPAF_TCP6_BYTE, AddressFamily.AF_IPv6, TransportProtocol.STREAM);
        /**
         * The UDP4 represents a connection which was forwarded for an IPv4 client over UDP.
         */
        public static readonly HAProxyProxiedProtocol UDP4 = new HAProxyProxiedProtocol("UDP4", HAProxyConstants.TPAF_UDP4_BYTE, AddressFamily.AF_IPv4, TransportProtocol.DGRAM);
        /**
         * The UDP6 represents a connection which was forwarded for an IPv6 client over UDP.
         */
        public static readonly HAProxyProxiedProtocol UDP6 = new HAProxyProxiedProtocol("UDP6", HAProxyConstants.TPAF_UDP6_BYTE, AddressFamily.AF_IPv6, TransportProtocol.DGRAM);
        /**
         * The UNIX_STREAM represents a connection which was forwarded for a UNIX stream socket.
         */
        public static readonly HAProxyProxiedProtocol UNIX_STREAM = new HAProxyProxiedProtocol("UNIX_STREAM", HAProxyConstants.TPAF_UNIX_STREAM_BYTE, AddressFamily.AF_UNIX, TransportProtocol.STREAM);
        /**
         * The UNIX_DGRAM represents a connection which was forwarded for a UNIX datagram socket.
         */
        public static readonly HAProxyProxiedProtocol UNIX_DGRAM = new HAProxyProxiedProtocol("UNIX_DGRAM", HAProxyConstants.TPAF_UNIX_DGRAM_BYTE, AddressFamily.AF_UNIX, TransportProtocol.DGRAM);

        public static IEnumerable<HAProxyProxiedProtocol> Values
        {
            get
            {
                yield return UNKNOWN;
                yield return TCP4;
                yield return TCP6;
                yield return UDP4;
                yield return UDP6;
                yield return UNIX_STREAM;
                yield return UNIX_DGRAM;
            }
        }

        readonly byte byteValue;
        readonly AddressFamily addressFamily;
        readonly TransportProtocol transportProtocol;

        HAProxyProxiedProtocol(string name, byte byteValue, AddressFamily addressFamily, TransportProtocol transportProtocol)
        {
            NAME_LOOKUP.Add(name, this);
            this.byteValue = byteValue;
            this.addressFamily = addressFamily;
            this.transportProtocol = transportProtocol;
        }

        /**
         * Returns the {@link HAProxyProxiedProtocol} represented by the specified byte.
         *
         * @param tpafByte transport protocol and address family byte
         */
        public static HAProxyProxiedProtocol ValueOf(byte tpafByte)
        {
            switch (tpafByte)
            {
                case HAProxyConstants.TPAF_TCP4_BYTE:
                    return TCP4;
                case HAProxyConstants.TPAF_TCP6_BYTE:
                    return TCP6;
                case HAProxyConstants.TPAF_UNKNOWN_BYTE:
                    return UNKNOWN;
                case HAProxyConstants.TPAF_UDP4_BYTE:
                    return UDP4;
                case HAProxyConstants.TPAF_UDP6_BYTE:
                    return UDP6;
                case HAProxyConstants.TPAF_UNIX_STREAM_BYTE:
                    return UNIX_STREAM;
                case HAProxyConstants.TPAF_UNIX_DGRAM_BYTE:
                    return UNIX_DGRAM;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tpafByte), "unknown transport protocol + address family: " + (tpafByte & 0xFF));
            }
        }

        public static HAProxyProxiedProtocol ValueOf(string value)
        {
            HAProxyProxiedProtocol protocol;
            NAME_LOOKUP.TryGetValue(value, out protocol);
            if (protocol != null)
            {
                return protocol;
            }
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        /**
         * Returns the byte value of this protocol and address family.
         */
        public byte ByteValue()
        {
            return this.byteValue;
        }

        /**
         * Returns the {@link AddressFamily} of this protocol and address family.
         */
        public AddressFamily AddressFamilyType()
        {
            return this.addressFamily;
        }

        /**
         * Returns the {@link TransportProtocol} of this protocol and address family.
         */
        public TransportProtocol TransportProtocolType()
        {
            return this.transportProtocol;
        }

        /**
        * The address family of an HAProxy proxy protocol header.
        */
        public sealed class AddressFamily
        {
            /**
             * The UNSPECIFIED address family represents a connection which was forwarded for an unknown protocol.
             */
            public static readonly AddressFamily AF_UNSPEC = new AddressFamily(HAProxyConstants.AF_UNSPEC_BYTE);
            /**
             * The IPV4 address family represents a connection which was forwarded for an IPV4 client.
             */
            public static readonly AddressFamily AF_IPv4 = new AddressFamily(HAProxyConstants.AF_IPV4_BYTE);
            /**
             * The IPV6 address family represents a connection which was forwarded for an IPV6 client.
             */
            public static readonly AddressFamily AF_IPv6 = new AddressFamily(HAProxyConstants.AF_IPV6_BYTE);
            /**
             * The UNIX address family represents a connection which was forwarded for a unix socket.
             */
            public static readonly AddressFamily AF_UNIX = new AddressFamily(HAProxyConstants.AF_UNIX_BYTE);

            public static IEnumerable<AddressFamily> Values
            {
                get
                {
                    yield return AF_UNSPEC;
                    yield return AF_IPv4;
                    yield return AF_IPv6;
                    yield return AF_UNIX;
                }
            }

            /**
             * The highest 4 bits of the transport protocol and address family byte contain the address family
             */
            const byte FAMILY_MASK = (byte)0xf0;

            readonly byte byteValue;

            /**
             * Creates a new instance
             */
            AddressFamily(byte byteValue)
            {
                this.byteValue = byteValue;
            }

            /**
             * Returns the {@link AddressFamily} represented by the highest 4 bits of the specified byte.
             *
             * @param tpafByte transport protocol and address family byte
             */
            public static AddressFamily ValueOf(byte tpafByte)
            {
                int addressFamily = tpafByte & FAMILY_MASK;
                switch ((byte)addressFamily)
                {
                    case HAProxyConstants.AF_IPV4_BYTE:
                        return AF_IPv4;
                    case HAProxyConstants.AF_IPV6_BYTE:
                        return AF_IPv6;
                    case HAProxyConstants.AF_UNSPEC_BYTE:
                        return AF_UNSPEC;
                    case HAProxyConstants.AF_UNIX_BYTE:
                        return AF_UNIX;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(tpafByte), "unknown address family: " + addressFamily);
                }
            }

            /**
             * Returns the byte value of this address family.
             */
            public byte ByteValue()
            {
                return this.byteValue;
            }
        }

        /**
         * The transport protocol of an HAProxy proxy protocol header
         */
        public sealed class TransportProtocol
        {
            /**
            * The UNSPEC transport protocol represents a connection which was forwarded for an unknown protocol.
            */
            public static readonly TransportProtocol UNSPEC = new TransportProtocol(HAProxyConstants.TRANSPORT_UNSPEC_BYTE);
            /**
             * The STREAM transport protocol represents a connection which was forwarded for a TCP connection.
             */
            public static readonly TransportProtocol STREAM = new TransportProtocol(HAProxyConstants.TRANSPORT_STREAM_BYTE);
            /**
             * The DGRAM transport protocol represents a connection which was forwarded for a UDP connection.
             */
            public static readonly TransportProtocol DGRAM = new TransportProtocol(HAProxyConstants.TRANSPORT_DGRAM_BYTE);

            public static IEnumerable<TransportProtocol> Values
            {
                get
                {
                    yield return UNSPEC;
                    yield return STREAM;
                    yield return DGRAM;
                }
            }

            /**
             * The transport protocol is specified in the lowest 4 bits of the transport protocol and address family byte
             */
            const byte TRANSPORT_MASK = 0x0f;

            readonly byte transportByte;

            /**
            * Creates a new instance.
            */
            TransportProtocol(byte transportByte)
            {
                this.transportByte = transportByte;
            }

            /**
             * Returns the {@link TransportProtocol} represented by the lowest 4 bits of the specified byte.
             *
             * @param tpafByte transport protocol and address family byte
             */
            public static TransportProtocol ValueOf(byte tpafByte)
            {
                int transportProtocol = tpafByte & TRANSPORT_MASK;
                switch ((byte)transportProtocol)
                {
                    case HAProxyConstants.TRANSPORT_STREAM_BYTE:
                        return STREAM;
                    case HAProxyConstants.TRANSPORT_UNSPEC_BYTE:
                        return UNSPEC;
                    case HAProxyConstants.TRANSPORT_DGRAM_BYTE:
                        return DGRAM;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(tpafByte), "unknown transport protocol: " + transportProtocol);
                }
            }

            /**
             * Returns the byte value of this transport protocol.
             */
            public byte ByteValue()
            {
                return this.transportByte;
            }
        }
    }
}
