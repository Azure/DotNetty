namespace DotNetty.Codecs.CoapTcp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public abstract class Message
    {
        private const byte DEFAULT_VERSION = 1;
        private const byte MESSGAE_TYPE_BITMASK = 0xE0;

        public byte Version { get { return version; } }
        public byte Type { get { return type; } }
        public byte Code { get { return code; } }
        public byte[] Token { get { return token; } }
        public List<MessageOption> Options { get { return options; } }
        public byte[] Payload { get { return payload; } }

        protected byte version;
        protected byte type;
        protected byte code;
        protected byte[] token;
        protected List<MessageOption> options;
        protected byte[] payload;

        protected Message(byte version, byte type, byte code, byte[] token, List<MessageOption> options, byte[] payload)
        {
            this.version = version;
            this.type = type;
            this.code = code;
            this.token = token;
            this.options = options;
            this.payload = payload;
        }

        protected Message(byte type, byte code, byte[] token, List<MessageOption> options, byte[] payload) :
            this(DEFAULT_VERSION, type, code, token, options, payload)
        { }

        public MessageType GetMessageType()
        {
            // code == 0.00 means EMPTY
            if (0 == code) 
            {
                return MessageType.EMPTY;
            }

            byte prefix = (byte)((code & MESSGAE_TYPE_BITMASK) >> 5);
            if (prefix == 0) {
                return MessageType.REQUEST;
            }
            else if (2 <= prefix && prefix <= 5) {
                return MessageType.RESPONSE;
            }
            throw new ArgumentException("undefined request/response type for code:" + code);
        }

        public override bool Equals(object obj)
        {
            if (null == obj || GetType() != obj.GetType())
            {
                return false;
            }

            Message message = (Message)obj;
            return (Version == message.Version &&
                Type == message.Type &&
                Code == message.Code &&
                Token.SequenceEqual(message.Token) &&
                TokenEquals(message.Options) &&
                Payload.SequenceEqual(message.Payload));
        }

        public override int GetHashCode()
        {
            return base.GetHashCode() + Type << 24 + Code << 16;
        }

        private bool TokenEquals(List<MessageOption> options)
        {
            if (Options.Count != options.Count)
            {
                return false;
            }
            for (int i = 0; i < Options.Count; i++)
            {
                if (!Options[i].Equals(options[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
