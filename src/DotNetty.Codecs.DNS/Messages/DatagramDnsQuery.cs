using DotNetty.Transport.Channels;
using System;
using System.Net;

namespace DotNetty.Codecs.DNS.Messages
{
    public class DatagramDnsQuery : DefaultDnsQuery, IAddressedEnvelope<DatagramDnsQuery>
    {
        public DatagramDnsQuery Content => this;

        public EndPoint Sender { get; }

        public EndPoint Recipient { get; }

        public DatagramDnsQuery(EndPoint sender, EndPoint recipient, int id) : this(sender, recipient, id, DnsOpCode.QUERY) { }

        public DatagramDnsQuery(EndPoint sender, EndPoint recipient, int id, DnsOpCode opCode) : base(id, opCode)
        {
            if (recipient == null && sender == null)
                throw new ArgumentNullException("recipient and sender");
            
            Sender = sender;
            Recipient = recipient;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
                return false;

            if (!base.Equals(obj))
                return false;

            if (!(obj is IAddressedEnvelope<DatagramDnsQuery>))
                return false;

            IAddressedEnvelope<DatagramDnsQuery> that = (IAddressedEnvelope<DatagramDnsQuery>)obj;
            if (Sender == null)
            {
                if (that.Sender != null)
                    return false;
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
                hashCode = hashCode * 31 + Sender.GetHashCode();

            if (Recipient != null)
                hashCode = hashCode * 31 + Recipient.GetHashCode();

            return hashCode;
        }
    }
}
