using DotNetty.Buffers;
using DotNetty.Codecs.DNS.Records;

namespace DotNetty.Codecs.DNS
{
    public interface IDnsRecordEncoder
    {
        void EncodeQuestion(IDnsQuestion question, IByteBuffer output);
        void EncodeRecord(IDnsRecord record, IByteBuffer output);
    }
}
