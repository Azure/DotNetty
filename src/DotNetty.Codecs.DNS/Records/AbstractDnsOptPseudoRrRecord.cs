using System.Reflection;
using System.Text;

namespace DotNetty.Codecs.DNS.Records
{
    public abstract class AbstractDnsOptPseudoRrRecord : AbstractDnsRecord, IDnsOptPseudoRecord
    {
        private const string EMPTY_STRING = "";

        protected AbstractDnsOptPseudoRrRecord(int maxPayloadSize, int extendedRcode, int version)
            : base(EMPTY_STRING, DnsRecordType.OPT, PackIntoLong(extendedRcode, version), (DnsRecordClass)maxPayloadSize ) { }

        protected AbstractDnsOptPseudoRrRecord(int maxPayloadSize)
            : base(EMPTY_STRING, DnsRecordType.OPT, 0, (DnsRecordClass)maxPayloadSize) { }

        private static long PackIntoLong(int val, int val2)
        {
            return ((val & 0xff) << 24 | (val2 & 0xff) << 16 | (0 & 0xff) << 8 | 0 & 0xff) & 0xFFFFFFFFL;
        }

        public int ExtendedRcode => (short) (((int) TimeToLive >> 16) & 0xff);

        public int Version => (short)(((int)TimeToLive >> 16) & 0xff);

        public int Flags => (short)((short)TimeToLive & 0xff);

        public override string ToString()
        {
            return GetBuilder().ToString();
        }

        protected StringBuilder GetBuilder()
        {
            return new StringBuilder(64)
                .Append(GetType().GetTypeInfo().Name)
                .Append('(')
                .Append("OPT flags:")
                .Append(Flags)
                .Append(" version:")
                .Append(Version)
                .Append(" extendedRecode:")
                .Append(ExtendedRcode)
                .Append(" udp:")
                .Append(DnsClass)
                .Append(')');
        }
    }
}
