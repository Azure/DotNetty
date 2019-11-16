// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.HaProxy
{
    using System;
    using System.Collections.Generic;

    /**
     * The command of an HAProxy proxy protocol header
     */
    public sealed class HAProxyCommand
    {
        /**
         * The LOCAL command represents a connection that was established on purpose by the proxy
         * without being relayed.
         */
        public static readonly HAProxyCommand LOCAL = new HAProxyCommand(HAProxyConstants.COMMAND_LOCAL_BYTE);
        /**
         * The PROXY command represents a connection that was established on behalf of another node,
         * and reflects the original connection endpoints.
         */
        public static readonly HAProxyCommand PROXY = new HAProxyCommand(HAProxyConstants.COMMAND_PROXY_BYTE);

        public static IEnumerable<HAProxyCommand> Values
        {
            get
            {
                yield return LOCAL;
                yield return PROXY;
            }
        }

        /**
         * Returns the {@link HAProxyCommand} represented by the lowest 4 bits of the specified byte.
         *
         * @param verCmdByte protocol version and command byte
         */
        public static HAProxyCommand ValueOf(byte verCmdByte)
        {
            int cmd = verCmdByte & COMMAND_MASK;
            switch ((byte)cmd)
            {
                case HAProxyConstants.COMMAND_PROXY_BYTE:
                    return PROXY;
                case HAProxyConstants.COMMAND_LOCAL_BYTE:
                    return LOCAL;
                default:
                    throw new ArgumentOutOfRangeException(nameof(verCmdByte), "unknown command: " + cmd);
            }
        }

        /**
         * The command is specified in the lowest 4 bits of the protocol version and command byte
         */
        const byte COMMAND_MASK = 0x0f;

        readonly byte byteValue;

        HAProxyCommand(byte byteValue)
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
