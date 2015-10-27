using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNetty.Codecs.CoapTcp
{
    class ResponseCodeTranslator
    {
        private const byte MESSGAE_TYPE_BITMASK = 0xE0;
        private const byte REQUEST_CODE_BITMASK = 0x1F;

        public static string Translate(byte code)
        {
            byte prefix = (byte)((code & MESSGAE_TYPE_BITMASK) >> 5);

            if (2 <= prefix && prefix <= 5)
            {
                byte suffix = (byte)(code & REQUEST_CODE_BITMASK);
                return String.Format("{0}.{1}", prefix, suffix);
            }
            throw new ArgumentException("code does not represent a request; code:" + code);
        }

        public static byte Translate(string s)
        {
            string[] codes = s.Split('.');
            if (2 != codes.Length)
            {
                throw new ArgumentException("code is malformed; code:" + code);
            }

            byte prefix = (byte)Int32.Parse(codes[0]);
            byte suffix = (byte)Int32.Parse(codes[1]);
            return (byte)((prefix << 5) & suffix);
        }
    }
}
