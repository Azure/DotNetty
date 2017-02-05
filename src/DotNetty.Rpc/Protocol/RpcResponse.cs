namespace DotNetty.Rpc.Protocol
{
    public class RpcResponse
    {
        public string RequestId { get; set; }

        public string Error { get; set; }

        public object Result { get; set; }
    }
}
