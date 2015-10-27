namespace DotNetty.Codecs.CoapTcp
{
    public interface IDecoder
    {
        Message Decode(byte[] bytes);
    }
}
