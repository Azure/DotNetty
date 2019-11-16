// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.HaProxy
{
    using DotNetty.Buffers;

    /**
     * A Type-Length Value (TLV vector) that can be added to the PROXY protocol
     * to include additional information like SSL information.
     *
     * @see HAProxySSLTLV
     */
    public class HAProxyTLV : DefaultByteBufferHolder
    {

        readonly Type type;
        readonly byte typeByteValue;

        /**
         * The registered types a TLV can have regarding the PROXY protocol 1.5 spec
         */
        public enum Type
        {
            PP2_TYPE_ALPN,
            PP2_TYPE_AUTHORITY,
            PP2_TYPE_SSL,
            PP2_TYPE_SSL_VERSION,
            PP2_TYPE_SSL_CN,
            PP2_TYPE_NETNS,
            /**
             * A TLV type that is not officially defined in the spec. May be used for nonstandard TLVs
             */
            OTHER
        }

        /**
        * Returns the the {@link Type} for a specific byte value as defined in the PROXY protocol 1.5 spec
        * <p>
        * If the byte value is not an official one, it will return {@link Type#OTHER}.
        *
        * @param byteValue the byte for a type
        *
        * @return the {@link Type} of a TLV
        */
        public static Type TypeForByteValue(byte byteValue)
        {
            switch (byteValue)
            {
                case 0x01:
                    return Type.PP2_TYPE_ALPN;
                case 0x02:
                    return Type.PP2_TYPE_AUTHORITY;
                case 0x20:
                    return Type.PP2_TYPE_SSL;
                case 0x21:
                    return Type.PP2_TYPE_SSL_VERSION;
                case 0x22:
                    return Type.PP2_TYPE_SSL_CN;
                case 0x30:
                    return Type.PP2_TYPE_NETNS;
                default:
                    return Type.OTHER;
            }
        }

        /**
         * Creates a new HAProxyTLV
         *
         * @param type the {@link Type} of the TLV
         * @param typeByteValue the byteValue of the TLV. This is especially important if non-standard TLVs are used
         * @param content the raw content of the TLV
         */
        internal HAProxyTLV(Type type, byte typeByteValue, IByteBuffer content) : base(content)
        {
            this.type = type;
            this.typeByteValue = typeByteValue;
        }

        /**
         * Returns the {@link Type} of this TLV
         */
        public Type TLVType()
        {
            return this.type;
        }

        /**
         * Returns the type of the TLV as byte
         */
        public byte TypeByteValue()
        {
            return this.typeByteValue;
        }

        public override IByteBufferHolder Replace(IByteBuffer content)
        {
            return new HAProxyTLV(type, typeByteValue, content);
        }

    }
}
