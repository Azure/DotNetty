namespace DotNetty.Rpc.Protocol
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Rpc.Exceptions;
    using DotNetty.Transport.Channels;
    using Newtonsoft.Json.Linq;

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

            T obj;
            try
            {
                obj = SerializationUtil.Deserialize<T>(data);
            }
            catch (Exception)
            {
                JObject rpcRequest = SerializationUtil.SafeDeserialize(data);
                string requestId = Convert.ToString(rpcRequest["RequestId"]);
                if (string.IsNullOrEmpty(requestId))
                    throw;
                throw new DeserializeException(requestId, rpcRequest.ToString());
            }
            output.Add(obj);
        }
    }
}
