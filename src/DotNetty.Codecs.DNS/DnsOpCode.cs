using System;
using System.Collections.Generic;
using System.Text;

namespace DotNetty.Codecs.DNS
{
    public class DnsOpCode
    {
        public static readonly DnsOpCode QUERY = new DnsOpCode(0x00, "QUERY");
                      
        public static readonly DnsOpCode IQUERY = new DnsOpCode(0x01, "IQUERY");
                      
        public static readonly DnsOpCode STATUS = new DnsOpCode(0x02, "STATUS");
                      
        public static readonly DnsOpCode NOTIFY = new DnsOpCode(0x04, "NOTIFY");
                      
        public static readonly DnsOpCode UPDATE = new DnsOpCode(0x05, "UPDATE");

        public byte ByteValue { get; }
        public string Name { get; }
        private string text;

        public static DnsOpCode From(int byteValue)
        {
            switch (byteValue)
            {
                case 0x00:
                    return QUERY;
                case 0x01:
                    return IQUERY;
                case 0x02:
                    return STATUS;
                case 0x04:
                    return NOTIFY;
                case 0x05:
                    return UPDATE;
                default:
                    return new DnsOpCode(byteValue);
            }
        }

        public DnsOpCode(int byteValue, string name = "UNKNOWN")
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ByteValue = (byte)byteValue;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;

            if (!(obj is DnsOpCode))
                return false;

            return ByteValue == ((DnsOpCode)obj).ByteValue;
        }

        public override int GetHashCode()
        {
            return ByteValue;
        }

        public override string ToString()
        {
            string text = this.text;
            if (string.IsNullOrWhiteSpace(text))
                this.text = text = $"{Name}({ByteValue & 0xFF})";

            return text;
        }
    }
}
