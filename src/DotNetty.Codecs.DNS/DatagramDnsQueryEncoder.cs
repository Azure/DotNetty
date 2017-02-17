using System;
using System.Collections.Generic;
using DotNetty.Transport.Channels;
using DotNetty.Codecs.DNS.Messages;
using DotNetty.Buffers;
using DotNetty.Codecs.DNS.Records;
using System.Net;
using DotNetty.Transport.Channels.Sockets;

namespace DotNetty.Codecs.DNS
{
    public class DatagramDnsQueryEncoder : MessageToMessageEncoder<IAddressedEnvelope<IDnsQuery>>
    {
        private readonly IDnsRecordEncoder recordEncoder;

        public DatagramDnsQueryEncoder() : this(new DefaultDnsRecordEncoder()) { }

        public DatagramDnsQueryEncoder(IDnsRecordEncoder recordEncoder)
        {
            this.recordEncoder = recordEncoder ?? throw new ArgumentNullException(nameof(recordEncoder));
        }

        protected override void Encode(IChannelHandlerContext context, IAddressedEnvelope<IDnsQuery> message, List<object> output)
        {
            EndPoint recipient = message.Recipient;
            IDnsQuery query = message.Content;
            IByteBuffer buffer = AllocateBuffer(context, message);
            bool success = false;

            try
            {
                EncodeHeader(query, buffer);
                EncodeQuestions(query, buffer);
                EncodeRecords(query, DnsSection.ADDITIONAL, buffer);
                success = true;
            }
            finally
            {
                if (!success)
                    buffer.Release();
            }

            output.Add(new DatagramPacket(buffer, recipient, null));
        }

        private IByteBuffer AllocateBuffer(IChannelHandlerContext ctx, 
            IAddressedEnvelope<IDnsQuery> message) => ctx.Allocator.Buffer(1024);

        private void EncodeHeader(IDnsQuery query, IByteBuffer buffer)
        {
            buffer.WriteShort(query.Id);
            int flags = 0;
            flags |= (query.OpCode.ByteValue & 0xFF) << 14;
            if (query.IsRecursionDesired)
                flags |= 1 << 8;

            buffer.WriteShort(flags);
            buffer.WriteShort(query.Count(DnsSection.QUESTION));
            buffer.WriteShort(0);
            buffer.WriteShort(0);
            buffer.WriteShort(query.Count(DnsSection.ADDITIONAL));
        }

        private void EncodeQuestions(IDnsQuery query, IByteBuffer buffer)
        {
            int count = query.Count(DnsSection.QUESTION);
            for (int i = 0; i < count; i++)
            {
                recordEncoder.EncodeQuestion(query.GetRecord<IDnsQuestion>(DnsSection.QUESTION, i), buffer);
            }
        }

        private void EncodeRecords(IDnsQuery query, DnsSection section, IByteBuffer buffer)
        {
            int count = query.Count(section);
            for (int i = 0; i < count; i++)
            {
                recordEncoder.EncodeRecord(query.GetRecord<IDnsRecord>(section, i), buffer);
            }
        }
    }
}
