namespace DotNetty.Rpc.Protocol
{
    public class RpcRequest
    {
        public string RequestId { get; set; }

        public object Message { get; set; }
    }
}
