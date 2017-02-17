
namespace DotNetty.Codecs.DNS.Records
{
    public interface IDnsPtrRecord : IDnsRecord
    {
        string HostName { get; }
    }
}
