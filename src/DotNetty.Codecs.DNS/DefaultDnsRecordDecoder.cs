using System.Text;
using DotNetty.Buffers;
using DotNetty.Codecs.DNS.Records;

namespace DotNetty.Codecs.DNS
{
    public class DefaultDnsRecordDecoder : IDnsRecordDecoder
    {
        private const string ROOT = ".";

        internal DefaultDnsRecordDecoder() { }

        public IDnsQuestion DecodeQuestion(IByteBuffer inputBuffer)
        {
            string name = DecodeName(inputBuffer);
            DnsRecordType type = DnsRecordType.From(inputBuffer.ReadUnsignedShort());
            var recordClass = (DnsRecordClass)inputBuffer.ReadUnsignedShort();
            return new DefaultDnsQuestion(name, type, recordClass);
        }

        public IDnsRecord DecodeRecord(IByteBuffer inputBuffer) 
        {
            int startOffset = inputBuffer.ReaderIndex;
            string name = DecodeName(inputBuffer);

            int endOffset = inputBuffer.WriterIndex;
            if (endOffset - startOffset < 10)
            {
                inputBuffer.SetReaderIndex(startOffset);
                return null;
            }

            DnsRecordType type = DnsRecordType.From(inputBuffer.ReadUnsignedShort());
            var recordClass = (DnsRecordClass)inputBuffer.ReadUnsignedShort();
            long ttl = inputBuffer.ReadUnsignedInt();
            int length = inputBuffer.ReadUnsignedShort();
            int offset = inputBuffer.ReaderIndex;

            if (endOffset - offset < length)
            {
                inputBuffer.SetReaderIndex(startOffset);
                return null;
            }

            IDnsRecord record = DecodeRecord(name, type, recordClass, ttl, inputBuffer, offset, length);
            inputBuffer.SetReaderIndex(offset + length);
            return record;
        }

        protected IDnsRecord DecodeRecord(string name, DnsRecordType type, DnsRecordClass dnsClass, long timeToLive,
            IByteBuffer inputBuffer, int offset, int length)
        {
            if (type == DnsRecordType.PTR)
                return new DefaultDnsPtrRecord(name, dnsClass, timeToLive, DecodeName(inputBuffer.SetIndex(offset, offset + length)));

            return new DefaultDnsRawRecord(name, type, dnsClass, timeToLive, inputBuffer.SetIndex(offset, offset + length));
        }

        public static string DecodeName(IByteBuffer inputBuffer)
        {
            int position = -1;
            int @checked = 0;
            int end = inputBuffer.WriterIndex;
            int readable = inputBuffer.ReadableBytes;

            if (readable == 0)
                return ROOT;

            var name = new StringBuilder(readable << 1);
            while (inputBuffer.IsReadable())
            {
                int len = inputBuffer.ReadUnsignedShort();
                bool pointer = (len & 0xc0) == 0xc0;

                if (pointer)
                {
                    if (position == -1)
                        position = inputBuffer.ReaderIndex + 1;

                    if (!inputBuffer.IsReadable())
                        throw new CorruptedFrameException("truncated pointer in a name");

                    int next = (len & 0x3f) << 8 | inputBuffer.ReadUnsignedShort();
                    if (next >= end)
                        throw new CorruptedFrameException("name has an out-of-range pointer");

                    inputBuffer.SetReaderIndex(next);

                    @checked += 2;
                    if (@checked >= end)
                        throw new CorruptedFrameException("name contains a loop");
                }
                else if (len != 0)
                {
                    if (!inputBuffer.IsReadable(len))
                        throw new CorruptedFrameException("truncated label in a name");

                    name.Append(inputBuffer.ToString(inputBuffer.ReaderIndex, len, Encoding.UTF8))
                        .Append('.');
                    inputBuffer.SkipBytes(len);
                }
                else
                {
                    break;
                }
            }

            if (position != -1)
                inputBuffer.SetReaderIndex(position);

            if (name.Length == 0)
                return ROOT;

            if (name[name.Length - 1] != '.')
                name.Append('.');

            return name.ToString();
        }
    }
}
