using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNetty.Codecs.CoapTcp.blbt
{
    class BLBTMessage: Message
    {
        public const byte DEFAULT_VERSION = 0x01;
        public const byte DEFAULT_TYPE = 0x01;

        private BLBTMessage(byte code, byte[] token, List<MessageOption> options, byte[] payload):
            base(DEFAULT_VERSION, DEFAULT_TYPE, code, token, options, payload)
        {}

        public static BLBTMessage Create(byte code, byte[] token, List<MessageOption> options, byte[] payload)
        {
            return new BLBTMessage(code, token, options, payload);
        }
    }
}
