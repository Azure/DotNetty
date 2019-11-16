namespace DotNetty.Codecs.HaProxy
{
    using System;

    /**
     * A {@link DecoderException} which is thrown when an invalid HAProxy proxy protocol header is encountered
     */
    public class HAProxyProtocolException : DecoderException
    {

        public HAProxyProtocolException()
            : base("")
        {

        }

        public HAProxyProtocolException(string message)
            : base(message)
        {
        }

        public HAProxyProtocolException(Exception cause)
            : base(cause)
        {
        }

        public HAProxyProtocolException(string message, Exception cause)
            : base(cause)
        {
        }

    }
}
