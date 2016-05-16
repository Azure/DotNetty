// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Timeout
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
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
    /// bootstrap.ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
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
            this.timeout = 
                TimeUtil.Max(timeout, MinTimeout);
        }

        public override Task WriteAsync(IChannelHandlerContext context, object message)
        {
            Task task = context.WriteAsync(message);

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

        void ScheduleTimeout(IChannelHandlerContext context, Task future)
        {
            var task = new WriteTimeoutTask(context, future, this);
            var wrappedTask = new LinkedListNode<WriteTimeoutTask>(task);

            task.ScheduledTask = context.Executor.Schedule(task, timeout);

            if (!task.ScheduledTask.Completion.IsCompleted)
            {
                this.AddWriteTimeoutTask(wrappedTask);
            }
        }

        void AddWriteTimeoutTask(LinkedListNode<WriteTimeoutTask> task)
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
        protected void WriteTimedOut(IChannelHandlerContext context)
        {
            if (!this.closed)
            {
                context.FireExceptionCaught(WriteTimeoutException.Instance);
                context.CloseAsync();
                this.closed = true;
            }
        }

        sealed class WriteTimeoutTask : OneTimeTask
        {
            readonly WriteTimeoutHandler handler;
            readonly IChannelHandlerContext context;
            readonly Task future;

            readonly static Action<Task, object> OperationCompleteAction = HandleOperationComplete;

            public WriteTimeoutTask(IChannelHandlerContext context, Task future, WriteTimeoutHandler handler)
            {
                this.context = context;
                this.future = future;
                this.handler = handler;

                future.ContinueWith(OperationCompleteAction, this, TaskContinuationOptions.ExecuteSynchronously); 
            }

            static void HandleOperationComplete(Task future, object state)
            {
                var writeTimeoutTask = (WriteTimeoutTask) state;

                writeTimeoutTask.ScheduledTask.Cancel();
                writeTimeoutTask.handler.RemoveWriteTimeoutTask(writeTimeoutTask);
            }

            public IScheduledTask ScheduledTask { get; set; }

            public Task Future => this.future;

            public override void Run()
            {
                if (!this.future.IsCompleted)
                {
                    try
                    {
                        this.handler.WriteTimedOut(context);
                    }
                    catch (Exception ex)
                    {
                        context.FireExceptionCaught(ex);
                    }
                }

                this.handler.RemoveWriteTimeoutTask(this);
            }
        }
    }
}

