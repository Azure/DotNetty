using System;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace DotNetty.Codecs.DNS.Records
{
    public abstract class AbstractDnsRecord : IDnsRecord
    {
        private readonly IdnMapping idn = new IdnMapping();
        private int hashCode;

        public DnsRecordType Type { get; }
        public string Name { get; }
        public DnsRecordClass DnsClass { get; }
        public long TimeToLive { get; set; }

        protected AbstractDnsRecord(string name, DnsRecordType type, 
            long timeToLive, DnsRecordClass dnsClass = DnsRecordClass.IN)
        {
            if (TimeToLive < 0)
                throw new ArgumentException($"timeToLive: {timeToLive} (expected: >= 0)");

            TimeToLive = timeToLive;

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            Name = AppendTrailingDot(idn.GetAscii(name));
            Type = type ?? throw new ArgumentNullException(nameof(type));
            DnsClass = dnsClass;
        }

        private static string AppendTrailingDot(string name)
        {
            if (name.Length > 0 && !name.EndsWith("."))
                return name + ".";

            return name;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;

            if (!(obj is AbstractDnsRecord))
                return false;

            var that = (AbstractDnsRecord)obj;
            int hashCode = GetHashCode();
            if (hashCode != 0 && hashCode != that.GetHashCode())
                return false;

            return Type.IntValue == that.Type.IntValue &&
                DnsClass == that.DnsClass &&
                Name.Equals(that.Name);
        }

        public override int GetHashCode()
        {
            int hashCode = this.hashCode;
            if (hashCode != 0)
                return hashCode;

            return this.hashCode = Name.GetHashCode() * 31 + 
                Type.IntValue * 31 + (int)DnsClass;

        }

        public override string ToString()
        {
            var builder = new StringBuilder(64);
            builder.Append(GetType().GetTypeInfo().Name)
                .Append('(')
                .Append(Name)
                .Append(' ')
                .Append(TimeToLive)
                .Append(' ')
                .AppendRecordClass(DnsClass)
                .Append(' ')
                .Append(Type.Name)
                .Append(')');

            return builder.ToString();
        }

    }

    public enum DnsRecordClass : int
    {
        IN = 0x0001,
        CSNET = 0x0002,
        CHAOS = 0x0003,
        HESIOD = 0x0004,
        NONE = 0x00fe,
        ANY = 0x00ff
    }
    
}
