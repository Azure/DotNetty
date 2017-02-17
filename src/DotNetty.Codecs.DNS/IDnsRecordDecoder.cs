using DotNetty.Buffers;
using DotNetty.Codecs.DNS.Records;

namespace DotNetty.Codecs.DNS
{
    public interface IDnsRecordDecoder
    {
        IDnsQuestion DecodeQuestion(IByteBuffer inputBuffer);
        IDnsRecord DecodeRecord(IByteBuffer inputBuffer);
    }
}
