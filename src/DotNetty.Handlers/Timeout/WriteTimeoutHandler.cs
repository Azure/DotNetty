// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Timeout
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Raises a <see cref="WriteTimeoutException"/> when a write operation cannot finish in a certain period of time.
    /// 
    /// <para>
    /// <example>
    /// 
    /// The connection is closed when a write operation cannot finish in 30 seconds.
    ///
    /// <c>
    /// var bootstrap = new <see cref="DotNetty.Transport.Bootstrapping.ServerBootstrap"/>();
    ///
    /// bootstrap.ChildHandler(new ActionChannelInitializer&lt;ISocketChannel&gt;(channel =>
    /// {
    ///     IChannelPipeline pipeline = channel.Pipeline;
    ///     
    ///     pipeline.AddLast("writeTimeoutHandler", new <see cref="WriteTimeoutHandler"/>(30);
    ///     pipeline.AddLast("myHandler", new MyHandler());
    /// }    
    /// </c>
    /// 
    /// <c>
    /// public class MyHandler : ChannelDuplexHandler 
    /// {
    ///     public override void ExceptionCaught(<see cref="IChannelHandlerContext"/> context, <see cref="Exception"/> exception)
    ///     {
    ///         if(exception is <see cref="WriteTimeoutException"/>) 
    ///         {
    ///             // do somethind
    ///         }
    ///         else
    ///         {
    ///             base.ExceptionCaught(context, cause);
    ///         }
    ///      }
    /// }
    /// </c>
    /// 
    /// </example>
    /// </para>
    /// <see cref="ReadTimeoutHandler"/>
    /// <see cref="IdleStateHandler"/>
    /// </summary>
    public class WriteTimeoutHandler : ChannelHandlerAdapter
    {
        static readonly TimeSpan MinTimeout = TimeSpan.FromMilliseconds(1);
        readonly TimeSpan timeout;
        bool closed;

        /// <summary>
        /// A doubly-linked list to track all WriteTimeoutTasks.
        /// </summary>
        readonly LinkedList<WriteTimeoutTask> tasks = new LinkedList<WriteTimeoutTask>();

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetty.Handlers.Timeout.ReadTimeoutHandler"/> class.
        /// </summary>
        /// <param name="timeoutSeconds">Timeout in seconds.</param>
        public WriteTimeoutHandler(int timeoutSeconds)
            : this(TimeSpan.FromSeconds(timeoutSeconds))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetty.Handlers.Timeout.ReadTimeoutHandler"/> class.
        /// </summary>
        /// <param name="timeout">Timeout.</param>
        public WriteTimeoutHandler(TimeSpan timeout)
        {
            this.timeout = (timeout > TimeSpan.Zero)
                 ? TimeUtil.Max(timeout, MinTimeout)
                 : TimeSpan.Zero;
        }

        public override ChannelFuture WriteAsync(IChannelHandlerContext context, object message)
        {
            ChannelFuture task = context.WriteAsync(message);

            if (this.timeout.Ticks > 0)
            {
                this.ScheduleTimeout(context, task);
            }

            return task;
        }

        public override void HandlerRemoved(IChannelHandlerContext context)
        {
            LinkedListNode<WriteTimeoutTask> task = this.tasks.Last;
            while (task != null)
            {
                task.Value.ScheduledTask.Cancel();
                this.tasks.RemoveLast();
                task = this.tasks.Last;
            }
        }

        void ScheduleTimeout(IChannelHandlerContext context, ChannelFuture future)
        {
            // Schedule a timeout.
            var task = new WriteTimeoutTask(context, future, this);

            task.ScheduledTask = context.Executor.Schedule(task, timeout);

            if (!task.ScheduledTask.Completion.IsCompleted)
            {
                this.AddWriteTimeoutTask(task);

                // Cancel the scheduled timeout if the flush promise is complete.
                future.OnCompleted(WriteTimeoutTask.OperationCompleteAction, task);
              //  future.ContinueWith(WriteTimeoutTask.OperationCompleteAction, task, TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        void AddWriteTimeoutTask(WriteTimeoutTask task)
        {
            this.tasks.AddLast(task);
        }

        void RemoveWriteTimeoutTask(WriteTimeoutTask task)
        {
            this.tasks.Remove(task);
        }

        /// <summary>
        /// Is called when a write timeout was detected
        /// </summary>
        /// <param name="context">Context.</param>
        protected virtual void WriteTimedOut(IChannelHandlerContext context)
        {
            if (!this.closed)
            {
                context.FireExceptionCaught(WriteTimeoutException.Instance);
                context.CloseAsync();
                this.closed = true;
            }
        }

        sealed class WriteTimeoutTask : AbstractChannelPromise, IRunnable
        {
            readonly WriteTimeoutHandler handler;
            readonly IChannelHandlerContext context;
            readonly ChannelFuture future;

            public static readonly Action<object> OperationCompleteAction = HandleOperationComplete;

            public WriteTimeoutTask(IChannelHandlerContext context, ChannelFuture future, WriteTimeoutHandler handler)
            {
                this.context = context;
                this.future = future;
                this.handler = handler;
            }

            static void HandleOperationComplete(object state)
            {
                var writeTimeoutTask = (WriteTimeoutTask) state;

                // ScheduledTask has already be set when reaching here
                writeTimeoutTask.ScheduledTask.Cancel();
                writeTimeoutTask.handler.RemoveWriteTimeoutTask(writeTimeoutTask);
            }

            public IScheduledTask ScheduledTask { get; set; }

            public void Run()
            {
                // Was not written yet so issue a write timeout
                // The future itself will be failed with a ClosedChannelException once the close() was issued
                // See https://github.com/netty/netty/issues/2159
                if (!this.future.IsCompleted)
                {
                    try
                    {
                        this.handler.WriteTimedOut(context);
                    }
                    catch (Exception ex)
                    {
                        this.context.FireExceptionCaught(ex);
                    }
                }

                this.handler.RemoveWriteTimeoutTask(this);
            }
        }
    }
}

