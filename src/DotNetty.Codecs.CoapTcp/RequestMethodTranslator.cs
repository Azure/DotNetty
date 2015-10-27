using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNetty.Codecs.CoapTcp
{
    public class RequestMethodTranslator
    {
        private const byte MESSGAE_TYPE_BITMASK = 0xE0;
        private const byte REQUEST_CODE_BITMASK = 0x1F; 

        public static RequestMethod Translate(byte code)
        {
            byte prefix = (byte)((code & MESSGAE_TYPE_BITMASK) >> 5);
            if (0 != prefix)
            {
                throw new ArgumentException("code does not represent a request; code:" + code);
            }

            byte suffix = (byte)(code & REQUEST_CODE_BITMASK);
            switch (suffix) 
            {
                case 0:
                    return RequestMethod.GET;
                case 1:
                    return RequestMethod.POST;
                case 2:
                    return RequestMethod.PUT;
                case 4:
                    return RequestMethod.DELETE;
                default:
                    // there can be additional method added after this coding in the future
                    return RequestMethod.OTHER;
            }
        }

        public static byte Translate(RequestMethod method)
        {
            if (RequestMethod.OTHER == method)
            {
                throw new ArgumentException("unknown code for method:" + method);
            }
            return (byte)method;
        }
    }
}
