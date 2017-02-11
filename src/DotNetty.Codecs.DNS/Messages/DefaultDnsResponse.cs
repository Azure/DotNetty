using System;
using System.Text;

namespace DotNetty.Codecs.DNS.Messages
{
    public class DefaultDnsResponse : AbstractDnsMessage, IDnsResponse
    {
        public bool IsAuthoritativeAnswer { get; set; }
        public bool IsTruncated { get; set; }
        public bool IsRecursionAvailable { get; set; }
        public DnsResponseCode Code { get; set; }

        public DefaultDnsResponse(int id)
            : this(id, DnsOpCode.QUERY, DnsResponseCode.NOERROR) { }

        public DefaultDnsResponse(int id, DnsOpCode opCode)
            : this(id, opCode, DnsResponseCode.NOERROR) { }

        public DefaultDnsResponse(int id, DnsOpCode opCode, DnsResponseCode code)
            : base(id, opCode)
        {   
            Code = code ?? throw new ArgumentNullException(nameof(code));
        }

        public override string ToString()
        {
            return new StringBuilder(128).AppendResponse(this).ToString();
        }
    }
}
