using System;
using System.Net;
using System.Text;

namespace DotNetty.Codecs.DNS.Records
{
    public class DefaultDnsOptEcsRecord : AbstractDnsOptPseudoRrRecord, IDnsOptEcsRecord
    {
        private readonly int srcPrefixLength;
        private readonly byte[] address;

        public int SourcePrefixLength { get; }

        public int ScopePrefixLength => 0;

        public byte[] Address => (byte[])address.Clone();

        public DefaultDnsOptEcsRecord(int maxPayloadSize, int extendedRcode, int version,
            int srcPrefixLength, byte[] address) : base(maxPayloadSize, extendedRcode, version)
        {
            SourcePrefixLength = srcPrefixLength;
            address = VerifyAddress(address);
        }

        public DefaultDnsOptEcsRecord(int maxPayloadSize, int srcPrefixLength, byte[] address)
            : this(maxPayloadSize, 0, 0, srcPrefixLength, address) { }

        public DefaultDnsOptEcsRecord(int maxPayloadSize, IPAddress address)
            : this(maxPayloadSize, 0, 0, 0, address.GetAddressBytes()) { }

        private static byte[] VerifyAddress(byte[] bytes)
        {
            if (bytes.Length == 4 || bytes.Length == 16)
                return bytes;

            throw new ArgumentException("bytes.length must either 4 or 16");
        }

        public override string ToString()
        {
            StringBuilder builder = GetBuilder();
            builder.Length = builder.Length - 1;
            return builder.Append(" address:")
                .Append(string.Join(".", address, 0, address.Length))
                .Append(" sourcePrefixLength:")
                .Append(SourcePrefixLength)
                .Append(" scopePrefixLength:")
                .Append(ScopePrefixLength)
                .Append(')').ToString();
        }
    }
}
