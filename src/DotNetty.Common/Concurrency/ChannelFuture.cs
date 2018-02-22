// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.ExceptionServices;

    public struct ChannelFuture : ICriticalNotifyCompletion 
    {
        public static readonly ChannelFuture Completed = new ChannelFuture();

        public static ChannelFuture FromException(Exception ex) => new ChannelFuture(ex);
        
        readonly object state;

        public ChannelFuture(IChannelFuture future) : this((object)future)
        {
            this.state = future;
        }

        ChannelFuture(Exception ex) : this(ExceptionDispatchInfo.Capture(ex))
        {
                        
        }

        ChannelFuture(object state)
        {
            this.state = state;
        }

        public ChannelFuture GetAwaiter() => this;

        public bool IsCompleted => this.state is IChannelFuture future ? future.IsCompleted : true;

        public void GetResult()
        {
            switch (this.state)
            {
                case null:
                    break;
                case ExceptionDispatchInfo edi:
                    edi.Throw();
                    break;
                case IChannelFuture future:
                    future.GetResult();
                    break;
                default:
                    throw new InvalidOperationException("should not come here");
            }
        }

        public void OnCompleted(Action continuation)
        {
            if (this.state is IChannelFuture future)
            {
                future.OnCompleted(continuation);
            }
            else
            {
                continuation();
            }
        }
        
        public void OnCompleted(Action<object> continuation, object state)
        {
            if (this.state is IChannelFuture future)
            {
                future.OnCompleted(continuation, state);
            }
            else
            {
                continuation(state);
            }
        }
        
        public void UnsafeOnCompleted(Action continuation) => this.OnCompleted(continuation);
    }
}