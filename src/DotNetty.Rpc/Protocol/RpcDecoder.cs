namespace DotNetty.Rpc.Protocol
{
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Transport.Channels;

    public class RpcDecoder<T> : ByteToMessageDecoder
    {
        protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            if (input.ReadableBytes < 4)
            {
                return;
            }
            input.MarkReaderIndex();

            int dataLength = input.ReadInt();

            if (input.ReadableBytes < dataLength)
            {
                input.ResetReaderIndex();
                return;
            }

            var data = new byte[dataLength];

            input.ReadBytes(data);

            var obj = SerializationUtil.Deserialize<T>(data);
            output.Add(obj);
        }
    }
}
