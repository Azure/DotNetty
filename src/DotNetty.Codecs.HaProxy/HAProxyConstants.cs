// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.HaProxy
{
    sealed class HAProxyConstants
    {

        /**
         * Command byte constants
         */
        internal const byte COMMAND_LOCAL_BYTE = 0x00;
        internal const byte COMMAND_PROXY_BYTE = 0x01;

        /**
         * Version byte constants
         */
        internal const byte VERSION_ONE_BYTE = 0x10;
        internal const byte VERSION_TWO_BYTE = 0x20;

        /**
         * Transport protocol byte constants
         */
        internal const byte TRANSPORT_UNSPEC_BYTE = 0x00;
        internal const byte TRANSPORT_STREAM_BYTE = 0x01;
        internal const byte TRANSPORT_DGRAM_BYTE = 0x02;

        /**
         * Address family byte constants
         */
        internal const byte AF_UNSPEC_BYTE = 0x00;
        internal const byte AF_IPV4_BYTE = 0x10;
        internal const byte AF_IPV6_BYTE = 0x20;
        internal const byte AF_UNIX_BYTE = 0x30;

        /**
         * Transport protocol and address family byte constants
         */
        internal const byte TPAF_UNKNOWN_BYTE = 0x00;
        internal const byte TPAF_TCP4_BYTE = 0x11;
        internal const byte TPAF_TCP6_BYTE = 0x21;
        internal const byte TPAF_UDP4_BYTE = 0x12;
        internal const byte TPAF_UDP6_BYTE = 0x22;
        internal const byte TPAF_UNIX_STREAM_BYTE = 0x31;
        internal const byte TPAF_UNIX_DGRAM_BYTE = 0x32;

        private HAProxyConstants() { }
    }
}
