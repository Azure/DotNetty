// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public static class TaskExtensions
    {
        public static async Task CloseOnComplete(this ValueTask task, IChannelHandlerContext ctx)
        {
            try
            {
                await task;
            }
            finally
            {
                await ctx.CloseAsync();
            }
        }

        static readonly Func<Task, object, Task> CloseOnCompleteContinuation = Close;
        static readonly Func<Task, object, Task> CloseOnFailureContinuation = CloseOnFailure; 
        
        public static Task CloseOnComplete(this Task task, IChannelHandlerContext ctx) 
            => task.ContinueWith(CloseOnCompleteContinuation, ctx, TaskContinuationOptions.ExecuteSynchronously);
        
        public static Task CloseOnComplete(this Task task, IChannel channel) 
            => task.ContinueWith(CloseOnCompleteContinuation, channel, TaskContinuationOptions.ExecuteSynchronously);
        
        public static Task CloseOnFailure(this Task task, IChannelHandlerContext ctx) 
            => task.ContinueWith(CloseOnFailureContinuation, ctx, TaskContinuationOptions.ExecuteSynchronously);
        
        public static Task CloseOnFailure(this Task task, IChannel channel) 
            => task.ContinueWith(CloseOnFailureContinuation, channel, TaskContinuationOptions.ExecuteSynchronously);

        static Task Close(Task task, object state)
        {
            switch (state)
            {
                case IChannelHandlerContext ctx:
                    return ctx.CloseAsync();
                case IChannel ch:
                    return ch.CloseAsync();
                default:
                    throw new InvalidOperationException("must never get here");
            }
        }
        
        static Task CloseOnFailure(Task task, object state)
        {
            if (task.IsFaulted)
            {
                return Close(task, state);
            }
            return TaskEx.Completed;
        }
    }
}