using System.Reflection;
using System.Text;

namespace DotNetty.Codecs.DNS.Records
{
    public class DefaultDnsQuestion : AbstractDnsRecord, IDnsQuestion
    {
        public DefaultDnsQuestion(string name,
            DnsRecordType type, long timeToLive,
            DnsRecordClass dnsClass = DnsRecordClass.IN)
            : base(name, type, timeToLive, dnsClass) { }

        public DefaultDnsQuestion(string name, DnsRecordType type) : base(name, type, 0) { }

        public DefaultDnsQuestion(string name, DnsRecordType type, DnsRecordClass dnsClass) : 
            base(name, type, 0, dnsClass){ }

        public override string ToString()
        {
            var builder = new StringBuilder(64);

            builder.Append(GetType().GetTypeInfo().Name)
                .Append('(')
                .Append(Name)
                .Append(' ')
                .AppendRecordClass(DnsClass)
                .Append(' ')
                .Append(Type.Name)
                .Append(')');

            return builder.ToString();
        }
    }
}
