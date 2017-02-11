using System;

namespace DotNetty.Codecs.DNS.Messages
{
    public class DnsResponseCode
    {
        public static DnsResponseCode NOERROR = new DnsResponseCode(0, "NoError");
        public static DnsResponseCode FORMERR = new DnsResponseCode(1, "FormErr");
        public static DnsResponseCode SERVFAIL = new DnsResponseCode(2, "ServFail");
        public static DnsResponseCode NXDOMAIN = new DnsResponseCode(3, "NXDomain");
        public static DnsResponseCode NOTIMP = new DnsResponseCode(4, "NotImp");
        public static DnsResponseCode REFUSED = new DnsResponseCode(5, "Refused");
        public static DnsResponseCode YXDOMAIN = new DnsResponseCode(6, "YXDomain");
        public static DnsResponseCode YXRRSET = new DnsResponseCode(7, "YXRRSet");
        public static DnsResponseCode NXRRSET = new DnsResponseCode(8, "NXRRSet");
        public static DnsResponseCode NOTAUTH = new DnsResponseCode(9, "NotAuth");
        public static DnsResponseCode NOTZONE = new DnsResponseCode(10, "NotZone");
        public static DnsResponseCode BADVERS_OR_BADSIG = new DnsResponseCode(16, "BADVERS_OR_BADSIG");
        public static DnsResponseCode BADKEY = new DnsResponseCode(17, "BADKEY");
        public static DnsResponseCode BADTIME = new DnsResponseCode(18, "BADTIME");
        public static DnsResponseCode BADMODE = new DnsResponseCode(19, "BADMODE");
        public static DnsResponseCode BADNAME = new DnsResponseCode(20, "BADNAME");
        public static DnsResponseCode BADALG = new DnsResponseCode(21, "BADALG");

        private string text;

        public int IntValue { get; }
        public string Name { get; }

        private DnsResponseCode(int code) : this(code, "UNKNOWN") { }

        public DnsResponseCode(int code, string name)
        {
            if (code < 0 || code > 65535)
                throw new ArgumentException($"code: {code} (expected: 0 ~ 65535)");

            IntValue = code;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public static DnsResponseCode From(int responseCode)
        {
            switch (responseCode)
            {
                case 0:
                    return NOERROR;
                case 1:
                    return FORMERR;
                case 2:
                    return SERVFAIL;
                case 3:
                    return NXDOMAIN;
                case 4:
                    return NOTIMP;
                case 5:
                    return REFUSED;
                case 6:
                    return YXDOMAIN;
                case 7:
                    return YXRRSET;
                case 8:
                    return NXRRSET;
                case 9:
                    return NOTAUTH;
                case 10:
                    return NOTZONE;
                case 16:
                    return BADVERS_OR_BADSIG;
                case 17:
                    return BADKEY;
                case 18:
                    return BADTIME;
                case 19:
                    return BADMODE;
                case 20:
                    return BADNAME;
                case 21:
                    return BADALG;
                default:
                    return new DnsResponseCode(responseCode);
            }
        }

        public override int GetHashCode()
        {
            return IntValue;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is DnsResponseCode))
                return false;

            return IntValue == ((DnsResponseCode)obj).IntValue;
        }

        public override string ToString()
        {
            string text = this.text;
            if (text == null)
                this.text = text = $"{Name} ({IntValue})";
            return text;
        }
    }
}
