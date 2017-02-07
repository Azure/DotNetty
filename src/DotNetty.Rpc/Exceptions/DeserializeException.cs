namespace DotNetty.Rpc.Exceptions
{
    using System;

    public class DeserializeException : Exception
    {
        public string RequestId { get; private set; }

        public DeserializeException(string requestId)
        {
            this.RequestId = requestId;
        }
    }
}
