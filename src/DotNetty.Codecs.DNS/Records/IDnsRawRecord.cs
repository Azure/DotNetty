using DotNetty.Buffers;

namespace DotNetty.Codecs.DNS.Records
{
    public interface IDnsRawRecord : IDnsRecord, IByteBufferHolder
    {
    }
}
