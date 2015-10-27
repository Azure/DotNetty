namespace DotNetty.Codecs.CoapTcp
{
    using DotNetty.Codecs.CoapTcp.blbt;

    class CodecFactory
    {
        public static BLBTL1Codec SINGLETON = new BLBTL1Codec();
        public static IDecoder GetDecoder()
        {
            return SINGLETON;
        }

        public static IEncoder GetEncoder()
        {
            return SINGLETON;
        }
    }
}
