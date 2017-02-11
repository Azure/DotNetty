using DotNetty.Transport.Channels;
using System;
using System.Net;

namespace DotNetty.Codecs.DNS.Messages
{
    public class DatagramDnsResponse : DefaultDnsResponse, IAddressedEnvelope<DatagramDnsResponse>
    {
        public EndPoint Sender { get; }

        public EndPoint Recipient { get; }

        public DatagramDnsResponse Content { get; }

        public DatagramDnsResponse(EndPoint sender, EndPoint recipient, int id)
            : this(sender, recipient, id, DnsOpCode.QUERY, DnsResponseCode.NOERROR) { }

        public DatagramDnsResponse(EndPoint sender, EndPoint recipient, int id, DnsOpCode opCode)
            : this(sender, recipient, id, opCode, DnsResponseCode.NOERROR) { }

        public DatagramDnsResponse(EndPoint sender, EndPoint recipient, int id, DnsOpCode opCode, DnsResponseCode responseCode)
            : base(id, opCode, responseCode)
        {
            Sender = sender ?? throw new ArgumentNullException(nameof(sender));
            Recipient = recipient ?? throw new ArgumentNullException(nameof(recipient));
        }

        public override bool Equals(object obj)
        {
            if (this == obj) return true;

            if (!base.Equals(obj)) return false;

            if (!(obj is IAddressedEnvelope<DatagramDnsResponse>)) return false;

            var that = (IAddressedEnvelope<DatagramDnsResponse>)obj;

            if (Sender == null)
            {
                if (that.Sender != null)
                    return true;
            }
            else if (!Sender.Equals(that.Sender))
            {
                return false;
            }

            if (Recipient == null)
            {
                if (that.Recipient != null)
                    return false;
            }
            else if (!Recipient.Equals(that.Recipient))
            {
                return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            int hashCode = base.GetHashCode();
            if (Sender != null)
            {
                hashCode = hashCode * 31 + Sender.GetHashCode();
            }

            if (Recipient != null)
            {
                hashCode = hashCode * 31 + Recipient.GetHashCode();
            }

            return hashCode;
        }
    }
}
