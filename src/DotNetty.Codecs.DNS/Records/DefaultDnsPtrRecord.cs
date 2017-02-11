using System;
using System.Reflection;
using System.Text;

namespace DotNetty.Codecs.DNS.Records
{
    public class DefaultDnsPtrRecord : AbstractDnsRecord, IDnsPtrRecord
    {
        public string HostName { get; }

        public DefaultDnsPtrRecord(string name, DnsRecordClass dnsClass, long timeToLive, string hostname)
            : base(name, DnsRecordType.PTR, timeToLive, dnsClass)
        {
            if (string.IsNullOrWhiteSpace(hostname))
                throw new ArgumentNullException(hostname);

            HostName = hostname;
        }

        public override string ToString()
        {
            var builder = new StringBuilder(64);

            builder.Append(GetType().GetTypeInfo().Name)
                .Append('(')
                .Append(string.IsNullOrWhiteSpace(Name) ? "<root>" : Name)
                .Append(' ')
                .Append(TimeToLive)
                .Append(' ')
                .AppendRecordClass(DnsClass)
                .Append(' ')
                .Append(Type.Name)
                .Append(' ')
                .Append(HostName);

            return builder.ToString();
        }
    }
}
