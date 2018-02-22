namespace DotNetty.Common.Concurrency
{
    using System;

    public class CompletedFuture : IChannelFuture
    {
        public static readonly IChannelFuture Instance = new CompletedFuture();

        CompletedFuture()
        {
            
        }
        
        public void OnCompleted(Action continuation) => continuation();
        
        public void UnsafeOnCompleted(Action continuation) => this.OnCompleted(continuation);

        public bool IsCompleted => true;

        public void GetResult()
        {
            
        }

        public void OnCompleted(Action<object> continuation, object state) => continuation(state);

        public void Init(IEventExecutor executor)
        {
            
        }

        public void Recycle()
        {
            
        }
    }
}