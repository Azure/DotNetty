namespace DotNetty.Rpc.Exceptions
{
    using System;

    public class DeserializeException : Exception
    {
        public string RequestId { get; private set; }

        public string RpcMessage { get; private set; }

        public DeserializeException(string requestId,string rpcMessage)
        {
            this.RequestId = requestId;
            this.RpcMessage = rpcMessage;
        }
    }
}
