namespace DotNetty.Codecs.CoapTcp.blbt
{
    using DotNetty.Codecs.CoapTcp.util;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// BLBTL1Codec implements codec for CoAP over TCP.
    /// The message structure follows the Specification (L1 alternative with 
    /// 32-bit length field in the front). The specification is at:
    /// https://www.ietf.org/id/draft-tschofenig-core-coap-tcp-tls-04.txt
    /// 
    /// BLBT aliases the first letters of proposers' last names.
    /// * C. Bormann
    /// * S. Lemay
    /// * V. Solorzano Barboza
    /// * H. Tschofenig
    /// 
    /// Message format:
    ///  0                   1                   2                   3
    ///  0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// | Message Length ...                                            |
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |Ver| T |  TKL  |     Code      |   Token (if any, TKL bytes)   |
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |  Options (if any) ...                                         |
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |1 1 1 1 1 1 1 1|    Payload (if any) ...                       |
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// 
    /// Reference:
    /// Figure 6 in 
    /// https://www.ietf.org/id/draft-tschofenig-core-coap-tcp-tls-04.txt
    /// 
    /// And, options are in format defined in RFC7252
    /// 
    ///   0   1   2   3   4   5   6   7
    /// +---------------+---------------+
    /// |               |               |
    /// |  Option Delta | Option Length |   1 byte
    /// |               |               |
    /// +---------------+---------------+
    /// \                               \
    /// /         Option Delta          /   0-2 bytes
    /// \          (extended)           \
    /// +-------------------------------+
    /// \                               \
    /// /         Option Length         /   0-2 bytes
    /// \          (extended)           \
    /// +-------------------------------+
    /// \                               \
    /// /                               /
    /// \                               \
    /// /         Option Value          /   0 or more bytes
    /// \                               \
    /// /                               /
    /// \                               \
    /// +-------------------------------+
    ///
    /// </summary>
    class BLBTL1Codec : IDecoder, IEncoder
    {
        // 32-bit fixed length shim length
        private const int SHIM_LENGTH_SIZE = 4;
        // 0x05 = 0101 (version = 01 and type = 01 (NON))
        private const int FIXED_VERSION_AND_TYPE = 0x05;

        public Message Decode(byte[] bytes)
        {
            BytesReader reader = BytesReader.Create(bytes);

            int msgLen = reader.ReadInt(SHIM_LENGTH_SIZE, IntegerEncoding.NETWORK_ORDER);
            byte meta = reader.ReadByte();
            byte code = reader.ReadByte();
            byte version = (byte)(meta & 0x03);
            byte type = (byte)((meta >> 2) & 0x03);

            int tokenLength = meta >> 4;
            byte[] token = reader.ReadBytes(tokenLength);

            List<MessageOption> options = MessageOptionHelper.ReadOptions(reader);
            int endOfOptions = reader.GetNumBytesRead();

            int payLength = msgLen + 4 - endOfOptions;
            byte[] payload = reader.ReadBytes(payLength);

            return BLBTMessage.Create(code, token, options, payload);
        }

        /// <summary>
        /// Encode serialize a message to a byte array.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public byte[] Encode(Message message)
        {
            if (message is BLBTMessage)
            {
                BLBTMessage m = (BLBTMessage)message;

                byte tokenLength = (byte)m.Token.Length;
                byte meta = (byte)(tokenLength << 4 | FIXED_VERSION_AND_TYPE);

                BytesBuilder bytesBuilder = BytesBuilder.Create();
                byte[] msg = bytesBuilder
                    .Skip(SHIM_LENGTH_SIZE)
                    .AddByte(meta)
                    .AddByte(m.Code)
                    .AddBytes(m.Token, tokenLength)
                    .Add(m.Options, MessageOptionHelper.GetOptionBytesBuildHelper())
                    .AddBytes(m.Payload)
                    .Build();

                return msg;
            }
            throw new ArgumentException("encoder does not support the message object; message:" + message.ToString());
        }
    }
}
