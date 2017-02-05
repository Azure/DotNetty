using System;

namespace DotNetty.Rpc.Exceptions
{
    public class TimeOutException : Exception
    {
        public TimeOutException(string msg)
            : base(msg)
        {
            
        }
    }
}
