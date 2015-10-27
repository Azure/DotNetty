using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNetty.Codecs.CoapTcp.Tests
{
    class MessageOptionTestHelper
    {
        public static int estimateOptionByteSize(int delta, int payloadSize)
        {
            return 1 + GetExtraDeltaBytesSize(delta) + GetExtraLengthBytesSize(payloadSize) + payloadSize;
        }

        private static int GetExtraDeltaBytesSize(int delta)
        {
            return GetExtraByteSize(delta);
        }

        private static int GetExtraLengthBytesSize(int payloadSize)
        {
            return GetExtraByteSize(payloadSize);
        }

        private static int GetExtraByteSize(int value)
        {
            if (value < 13)
            {
                return 0;
            }
            if (value < 269)
            {
                return 1;
            }
            if (value < 65805) {
                return 2;
            }
            throw new ArgumentException("value " + value + "beyond the limit: " + 65805);
        }


    }
}
