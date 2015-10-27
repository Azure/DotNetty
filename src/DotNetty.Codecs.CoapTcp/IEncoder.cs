namespace DotNetty.Codecs.CoapTcp
{
    public interface IEncoder
    {
        byte[] Encode(Message message);
    }
}
