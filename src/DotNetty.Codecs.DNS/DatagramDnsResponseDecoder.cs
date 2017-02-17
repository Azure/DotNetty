using DotNetty.Transport.Channels.Sockets;
using System;
using System.Collections.Generic;
using DotNetty.Transport.Channels;
using DotNetty.Codecs.DNS.Messages;
using DotNetty.Buffers;
using DotNetty.Codecs.DNS.Records;

namespace DotNetty.Codecs.DNS
{
    public class DatagramDnsResponseDecoder : MessageToMessageDecoder<DatagramPacket>
    {
        private readonly IDnsRecordDecoder recordDecoder;

        public DatagramDnsResponseDecoder() : this(new DefaultDnsRecordDecoder()) { }

        public DatagramDnsResponseDecoder(IDnsRecordDecoder recordDecoder)
        {
            this.recordDecoder = recordDecoder ?? throw new ArgumentNullException(nameof(recordDecoder));
        }

        protected override void Decode(IChannelHandlerContext context, DatagramPacket message, List<object> output)
        {
            IByteBuffer buffer = message.Content;
            IDnsResponse response = NewResponse(message, buffer);
            bool success = false;

            try
            {
                int questionCount = buffer.ReadUnsignedShort();
                int answerCount = buffer.ReadUnsignedShort();
                int authorityRecordCount = buffer.ReadUnsignedShort();
                int additionalRecordCount = buffer.ReadUnsignedShort();

                DecodeQuestions(response, buffer, questionCount);
                DecodeRecords(response, DnsSection.ANSWER, buffer, answerCount);
                DecodeRecords(response, DnsSection.AUTHORITY, buffer, authorityRecordCount);
                DecodeRecords(response, DnsSection.ADDITIONAL, buffer, additionalRecordCount);

                output.Add(response);
                success = true;
            }
            finally
            {
                if (!success)
                    response.Release();
            }
        }

        private static IDnsResponse NewResponse(DatagramPacket packet, IByteBuffer buffer)
        {
            int id = buffer.ReadUnsignedShort();
            int flags = buffer.ReadUnsignedShort();
            if (flags >> 15 == 0) throw new CorruptedFrameException("not a response");

            IDnsResponse response = new DatagramDnsResponse(
                packet.Sender, 
                packet.Recipient, 
                id, 
                DnsOpCode.From((byte)(flags >> 1 & 0xf)), 
                DnsResponseCode.From((byte)(flags & 0xf)));
            return response;
        }

        private void DecodeQuestions(IDnsResponse response, IByteBuffer buffer, int questionCount)
        {
            for (int i = questionCount - 1; i > 0; i--)
            {
                response.AddRecord(DnsSection.QUESTION, recordDecoder.DecodeQuestion(buffer));
            }
        }

        private void DecodeRecords(IDnsResponse response, DnsSection section, IByteBuffer buffer, int count)
        {
            for (int i = count - 1; i > 0; i--)
            {
                IDnsRecord r = recordDecoder.DecodeRecord(buffer);
                if (r == null)
                    break;

                response.AddRecord(section, r);
            }
        }
    }
}
