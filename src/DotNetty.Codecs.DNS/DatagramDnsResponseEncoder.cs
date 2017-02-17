using DotNetty.Buffers;
using DotNetty.Codecs.DNS.Messages;
using DotNetty.Codecs.DNS.Records;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using System;
using System.Collections.Generic;
using System.Net;

namespace DotNetty.Codecs.DNS
{
    public class DatagramDnsResponseEncoder : MessageToMessageEncoder<IAddressedEnvelope<IDnsResponse>>
    {
        private IDnsRecordEncoder recordEncoder;

        public DatagramDnsResponseEncoder() : this(new DefaultDnsRecordEncoder()) { }

        public DatagramDnsResponseEncoder(IDnsRecordEncoder recordEncoder)
        {
            this.recordEncoder = recordEncoder ?? throw new ArgumentNullException(nameof(recordEncoder));
        }

        protected override void Encode(IChannelHandlerContext context, IAddressedEnvelope<IDnsResponse> message, List<object> output)
        {
            EndPoint recipient = message.Recipient;
            IDnsResponse response = message.Content;
            IByteBuffer buffer = AllocateBuffer(context, message);

            bool success = false;
            try
            {
                EncodeHeader(response, buffer);
                EncodeQuestions(response, buffer);
                EncodeRecords(response, DnsSection.ANSWER, buffer);
                EncodeRecords(response, DnsSection.AUTHORITY, buffer);
                EncodeRecords(response, DnsSection.ADDITIONAL, buffer);
                success = true;
            }
            finally
            {
                if (!success)
                    buffer.Release();
            }

            output.Add(new DatagramPacket(buffer, recipient, null));
        }

        protected IByteBuffer AllocateBuffer(IChannelHandlerContext ctx,
            IAddressedEnvelope<IDnsResponse> message) => ctx.Allocator.Buffer(1024);

        private static void EncodeHeader(IDnsResponse response, IByteBuffer buffer)
        {
            buffer.WriteShort(response.Id);
            int flags = 32768;
            flags |= (response.OpCode.ByteValue & 0xFF) << 11;
            if (response.IsAuthoritativeAnswer)
                flags |= 1 << 10;

            if (response.IsTruncated)
                flags |= 1 << 9;

            if (response.IsRecursionDesired)
                flags |= 1 << 8;

            if (response.IsRecursionAvailable)
                flags |= 1 << 7;

            flags |= response.Z << 4;
            flags |= response.Code.IntValue;
            buffer.WriteShort(flags);
            buffer.WriteShort(response.Count(DnsSection.QUESTION));
            buffer.WriteShort(response.Count(DnsSection.ANSWER));
            buffer.WriteShort(response.Count(DnsSection.AUTHORITY));
            buffer.WriteShort(response.Count(DnsSection.ADDITIONAL));
        }

        private void EncodeQuestions(IDnsResponse response, IByteBuffer buffer)
        {
            int count = response.Count(DnsSection.QUESTION);
            for (int i = 0; i < count; i++)
            {
                recordEncoder.EncodeQuestion(response.GetRecord<IDnsQuestion>(DnsSection.QUESTION, i), buffer);
            }
        }

        private void EncodeRecords(IDnsResponse response, DnsSection section, IByteBuffer buffer)
        {
            int count = response.Count(section);
            for (int i = 0; i < count; i++)
            {
                recordEncoder.EncodeRecord(response.GetRecord<IDnsRecord>(section, i), buffer);
            }
        }

        
    }
}
