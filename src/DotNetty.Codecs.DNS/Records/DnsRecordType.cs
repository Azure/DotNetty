using System;
using System.Collections.Generic;
using System.Text;

namespace DotNetty.Codecs.DNS.Records
{
    /// <summary>
    /// Represents a DNS record type.
    /// </summary>
    public class DnsRecordType
    {
        #region Types
        public static readonly DnsRecordType A = new DnsRecordType(0x0001, "A");

        public static readonly DnsRecordType NS = new DnsRecordType(0x0002, "NS");

        public static readonly DnsRecordType CNAME = new DnsRecordType(0x0005, "CNAME");

        public static readonly DnsRecordType SOA = new DnsRecordType(0x0006, "SOA");

        public static readonly DnsRecordType PTR = new DnsRecordType(0x000c, "PTR");

        public static readonly DnsRecordType MX = new DnsRecordType(0x000f, "MX");

        public static readonly DnsRecordType TXT = new DnsRecordType(0x0010, "TXT");

        public static readonly DnsRecordType RP = new DnsRecordType(0x0011, "RP");

        public static readonly DnsRecordType AFSDB = new DnsRecordType(0x0012, "AFSDB");

        public static readonly DnsRecordType SIG = new DnsRecordType(0x0018, "SIG");

        public static readonly DnsRecordType KEY = new DnsRecordType(0x0019, "KEY");

        public static readonly DnsRecordType AAAA = new DnsRecordType(0x001c, "AAAA");

        public static readonly DnsRecordType LOC = new DnsRecordType(0x001d, "LOC");

        public static readonly DnsRecordType SRV = new DnsRecordType(0x0021, "SRV");

        public static readonly DnsRecordType NAPTR = new DnsRecordType(0x0023, "NAPTR");

        public static readonly DnsRecordType KX = new DnsRecordType(0x0024, "KX");

        public static readonly DnsRecordType CERT = new DnsRecordType(0x0025, "CERT");

        public static readonly DnsRecordType DNAME = new DnsRecordType(0x0027, "DNAME");

        public static readonly DnsRecordType OPT = new DnsRecordType(0x0029, "OPT");

        public static readonly DnsRecordType APL = new DnsRecordType(0x002a, "APL");

        public static readonly DnsRecordType DS = new DnsRecordType(0x002b, "DS");

        public static readonly DnsRecordType SSHFP = new DnsRecordType(0x002c, "SSHFP");

        public static readonly DnsRecordType IPSECKEY = new DnsRecordType(0x002d, "IPSECKEY");

        public static readonly DnsRecordType RRSIG = new DnsRecordType(0x002e, "RRSIG");

        public static readonly DnsRecordType NSEC = new DnsRecordType(0x002f, "NSEC");

        public static readonly DnsRecordType DNSKEY = new DnsRecordType(0x0030, "DNSKEY");

        public static readonly DnsRecordType DHCID = new DnsRecordType(0x0031, "DHCID");

        public static readonly DnsRecordType NSEC3 = new DnsRecordType(0x0032, "NSEC3");

        public static readonly DnsRecordType NSEC3PARAM = new DnsRecordType(0x0033, "NSEC3PARAM");

        public static readonly DnsRecordType TLSA = new DnsRecordType(0x0034, "TLSA");

        public static readonly DnsRecordType HIP = new DnsRecordType(0x0037, "HIP");

        public static readonly DnsRecordType SPF = new DnsRecordType(0x0063, "SPF");

        public static readonly DnsRecordType TKEY = new DnsRecordType(0x00f9, "TKEY");

        public static readonly DnsRecordType TSIG = new DnsRecordType(0x00fa, "TSIG");

        public static readonly DnsRecordType IXFR = new DnsRecordType(0x00fb, "IXFR");

        public static readonly DnsRecordType AXFR = new DnsRecordType(0x00fc, "AXFR");

        public static readonly DnsRecordType ANY = new DnsRecordType(0x00ff, "ANY");

        public static readonly DnsRecordType CAA = new DnsRecordType(0x0101, "CAA");

        public static readonly DnsRecordType TA = new DnsRecordType(0x8000, "TA");

        public static readonly DnsRecordType DLV = new DnsRecordType(0x8001, "DLV");
        #endregion

        private static readonly Dictionary<string, DnsRecordType> byName = new Dictionary<string, DnsRecordType>();
        private static readonly Dictionary<int, DnsRecordType> byType = new Dictionary<int, DnsRecordType>();
        private static readonly string EXPECTED;
        private string text = string.Empty;

        public int IntValue { get; }
        public string Name { get; }

        private DnsRecordType(int intValue) : this(intValue, "UNKNOWN") { }

        public DnsRecordType(int intValue, string name)
        {
            if ((intValue & 0xffff) != intValue)
            {
                throw new ArgumentException("intValue: " + intValue + " (expected: 0 ~ 65535)");
            }
            IntValue = intValue;
            Name = name;
        }

        static DnsRecordType()
        {
            DnsRecordType[] all = {
                A, NS, CNAME, SOA, PTR, MX, TXT, RP, AFSDB, SIG, KEY, AAAA, LOC, SRV, NAPTR, KX, CERT, DNAME, OPT, APL,
                DS, SSHFP, IPSECKEY, RRSIG, NSEC, DNSKEY, DHCID, NSEC3, NSEC3PARAM, TLSA, HIP, SPF, TKEY, TSIG, IXFR,
                AXFR, ANY, CAA, TA, DLV
            };

            var expected = new StringBuilder(512);

            expected.Append(" (expected: ");

            foreach (var type in all)
            {
                byName.Add(type.Name, type);
                byType.Add(type.IntValue, type);

                expected.Append(type.Name)
                    .Append('(')
                    .Append(type.IntValue)
                    .Append("), ");                
            }

            expected.Length = expected.Length - 2;
            expected.Append(')');
            EXPECTED = expected.ToString(); 
        }

        public static DnsRecordType From(int intValue)
        {
            if (byType.ContainsKey(intValue))
                return byType[intValue];

            return new DnsRecordType(intValue);
        }

        public static DnsRecordType From(string name)
        {
            if (byName.ContainsKey(name))
                return byName[name];

            throw new ArgumentException($"name: {name} {EXPECTED}");
        }

        public override int GetHashCode()
        {
            return IntValue;
        }

        public override bool Equals(object obj)
        {
            return obj is DnsRecordType && ((DnsRecordType)obj).IntValue == IntValue;
        }

        public override string ToString()
        {
            string text = this.text;
            if (text == null)
                this.text = text = Name + '(' + IntValue + ')';

            return text;
        }
    }
}
