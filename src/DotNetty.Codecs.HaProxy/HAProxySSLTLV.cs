// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.HaProxy
{
    using System.Collections.Generic;
    using DotNetty.Buffers;

    /**
     * Represents a {@link HAProxyTLV} of the type {@link HAProxyTLV.Type#PP2_TYPE_SSL}.
     * This TLV encapsulates other TLVs and has additional information like verification information and a client bitfield.
    */
    public sealed class HAProxySSLTLV : HAProxyTLV
    {

        readonly int verify;
        readonly IList<HAProxyTLV> tlvs;
        readonly byte clientBitField;

        /**
         * Creates a new HAProxySSLTLV
         *
         * @param verify the verification result as defined in the specification for the pp2_tlv_ssl struct (see
         * http://www.haproxy.org/download/1.5/doc/proxy-protocol.txt)
         * @param clientBitField the bitfield with client information
         * @param tlvs the encapsulated {@link HAProxyTLV}s
         * @param rawContent the raw TLV content
         */
        internal HAProxySSLTLV(int verify, byte clientBitField, List<HAProxyTLV> tlvs, IByteBuffer rawContent) : base(Type.PP2_TYPE_SSL, (byte)0x20, rawContent)
        {
            this.verify = verify;
            this.tlvs = tlvs.AsReadOnly();
            this.clientBitField = clientBitField;
        }

        /**
         * Returns {@code true} if the bit field for PP2_CLIENT_CERT_CONN was set
         */
        public bool IsPP2ClientCertConn()
        {
            return (this.clientBitField & 0x2) != 0;
        }

        /**
         * Returns {@code true} if the bit field for PP2_CLIENT_SSL was set
         */
        public bool IsPP2ClientSSL()
        {
            return (this.clientBitField & 0x1) != 0;
        }

        /**
         * Returns {@code true} if the bit field for PP2_CLIENT_CERT_SESS was set
         */
        public bool IsPP2ClientCertSess()
        {
            return (this.clientBitField & 0x4) != 0;
        }

        /**
         * Returns the verification result
         */
        public int Verify()
        {
            return this.verify;
        }

        /**
         * Returns an unmodifiable Set of encapsulated {@link HAProxyTLV}s.
         */
        public IList<HAProxyTLV> EncapsulatedTLVs()
        {
            return this.tlvs;
        }
    }
}
