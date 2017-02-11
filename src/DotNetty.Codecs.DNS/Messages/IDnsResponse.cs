namespace DotNetty.Codecs.DNS.Messages
{
    public interface IDnsResponse : IDnsMessage
    {
        bool IsAuthoritativeAnswer { get; set; }
        bool IsTruncated { get; set; }
        bool IsRecursionAvailable { get; set; }
        DnsResponseCode Code { get; set; }
    }
}
