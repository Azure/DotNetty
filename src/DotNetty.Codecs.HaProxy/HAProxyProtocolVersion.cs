// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.HaProxy
{
    using System;
    using System.Collections.Generic;

    /**
     * The HAProxy proxy protocol specification version.
     */
    public sealed class HAProxyProtocolVersion
    {
        /**
         * The ONE proxy protocol version represents a version 1 (human-readable) header.
         */
        public static readonly HAProxyProtocolVersion V1 = new HAProxyProtocolVersion(HAProxyConstants.VERSION_ONE_BYTE);
        /**
         * The TWO proxy protocol version represents a version 2 (binary) header.
         */
        public static readonly HAProxyProtocolVersion V2 = new HAProxyProtocolVersion(HAProxyConstants.VERSION_TWO_BYTE);

        public static IEnumerable<HAProxyProtocolVersion> Values
        {
            get
            {
                yield return V1;
                yield return V2;
            }
        }

        /**
         * Returns the {@link HAProxyProtocolVersion} represented by the highest 4 bits of the specified byte.
         *
         * @param verCmdByte protocol version and command byte
         */
        public static HAProxyProtocolVersion ValueOf(byte verCmdByte)
        {
            int version = verCmdByte & VERSION_MASK;
            switch ((byte)version)
            {
                case HAProxyConstants.VERSION_TWO_BYTE:
                    return V2;
                case HAProxyConstants.VERSION_ONE_BYTE:
                    return V1;
                default:
                    throw new ArgumentOutOfRangeException(nameof(verCmdByte), "unknown version: " + version);
            }
        }

        /**
         * The highest 4 bits of the protocol version and command byte contain the version
         */
        const byte VERSION_MASK = (byte)0xf0;

        readonly byte byteValue;

        HAProxyProtocolVersion(byte byteValue)
        {
            this.byteValue = byteValue;
        }

        /**
         * Returns the byte value of this command.
         */
        public byte ByteValue()
        {
            return this.byteValue;
        }
    }
}
