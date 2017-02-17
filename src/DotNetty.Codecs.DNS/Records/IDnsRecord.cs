namespace DotNetty.Codecs.DNS.Records
{
    public interface IDnsRecord
    {
        DnsRecordClass DnsClass { get; }
        string Name { get; }
        long TimeToLive { get; set; }
        DnsRecordType Type { get; }
    }
}
