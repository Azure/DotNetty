using DotNetty.Common;
using DotNetty.Codecs.DNS.Records;

namespace DotNetty.Codecs.DNS.Messages
{
    public interface IDnsMessage : IReferenceCounted
    {
        int Id { get; set; }
        DnsOpCode OpCode { get; set; }
        bool IsRecursionDesired { get; set; }
        int Z { get; set; }
        int Count(DnsSection section);
        int Count();
        TRecord GetRecord<TRecord>(DnsSection section) where TRecord : IDnsRecord;
        TRecord GetRecord<TRecord>(DnsSection section, int index) where TRecord : IDnsRecord;
        void SetRecord(DnsSection section, IDnsRecord record);
        void SetRecord(DnsSection section, int index, IDnsRecord record);
        void AddRecord(DnsSection section, IDnsRecord record);
        void AddRecord(DnsSection section, int index, IDnsRecord record);
        void RemoveRecord(DnsSection section, int index);
        void Clear(DnsSection section);
        void Clear();
    }
}
