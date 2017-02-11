using DotNetty.Transport.Channels.Sockets;
using System;
using System.Collections.Generic;
using DotNetty.Transport.Channels;
using DotNetty.Codecs.DNS.Messages;
using DotNetty.Buffers;
using DotNetty.Codecs.DNS.Records;

namespace DotNetty.Codecs.DNS
{
    public class DatagramDnsQueryDecoder : MessageToMessageDecoder<DatagramPacket>
    {
        private readonly IDnsRecordDecoder recordDecoder;

        public DatagramDnsQueryDecoder() : this(new DefaultDnsRecordDecoder()) { }

        public DatagramDnsQueryDecoder(IDnsRecordDecoder recordDecoder)
        {
            this.recordDecoder = recordDecoder ?? throw new ArgumentNullException(nameof(recordDecoder));
        }

        protected override void Decode(IChannelHandlerContext context, DatagramPacket message, List<object> output)
        {
            IByteBuffer buffer = message.Content;
            IDnsQuery query = NewQuery(message, buffer);
            bool success = false;

            try
            {
                int questionCount = buffer.ReadUnsignedShort();
                int answerCount = buffer.ReadUnsignedShort();
                int authorityRecordCount = buffer.ReadUnsignedShort();
                int additionalRecordCount = buffer.ReadUnsignedShort();

                DecodeQuestions(query, buffer, questionCount);
                DecodeRecords(query, DnsSection.ANSWER, buffer, answerCount);
                DecodeRecords(query, DnsSection.AUTHORITY, buffer, authorityRecordCount);
                DecodeRecords(query, DnsSection.ADDITIONAL, buffer, additionalRecordCount);

                output.Add(query);
                success = true;
            }
            finally
            {
                if (!success)
                    query.Release();
            }
        }

        private static IDnsQuery NewQuery(DatagramPacket packet, IByteBuffer buffer)
        {
            int id = buffer.ReadUnsignedShort();
            int flags = buffer.ReadUnsignedShort();
            if (flags >> 15 == 1)
                throw new CorruptedFrameException("not a query");

            IDnsQuery query = new DatagramDnsQuery(
                packet.Sender, packet.Recipient, id, 
                DnsOpCode.From((byte)(flags >> 11 & 0xf)));

            query.IsRecursionDesired = (flags >> 8 & 1) == 1;
            query.Z = flags >> 4 & 0x7;
            return query;
        }

        private void DecodeQuestions(IDnsQuery query, IByteBuffer buffer, int questionCount)
        {
            for (int i = questionCount; i > 0; i--)
            {
                query.AddRecord(DnsSection.QUESTION, recordDecoder.DecodeQuestion(buffer));
            }
        }

        private void DecodeRecords(IDnsQuery query, DnsSection section, IByteBuffer buffer, int count)
        {
            for (int i = count; i > 0; i--)
            {
                IDnsRecord r = recordDecoder.DecodeRecord(buffer);
                if (r == null)
                    break;

                query.AddRecord(section, r);
            }
        }
    }
}
