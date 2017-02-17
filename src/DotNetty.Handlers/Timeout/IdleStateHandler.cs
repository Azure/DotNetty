// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Timeout
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Triggers an <see cref="IdleStateEvent"/> when a <see cref="IChannel"/> has not performed
    /// read, write, or both operation for a while.
    /// 
    /// <para>
    /// 
    /// <h3>Supported idle states</h3>
    /// <table border="1">
    ///     <tr>
    ///         <th>Property</th><th>Meaning</th>
    ///     </tr>
    ///     <tr>
    ///         <td><code>readerIdleTime</code></td>
    ///         <td>an <see cref="IdleStateEvent"/> whose state is <see cref="IdleState.ReaderIdle"/>
    ///             will be triggered when no read was performed for the specified period of
    ///             time.  Specify <code>0</code> to disable.
    ///         </td>
    ///     </tr>
    ///     <tr>
    ///         <td><code>writerIdleTime</code></td>
    ///         <td>an <see cref="IdleStateEvent"/> whose state is <see cref="IdleState.WriterIdle"/>
    ///             will be triggered when no write was performed for the specified period of
    ///             time.  Specify <code>0</code> to disable.</td>
    ///     </tr>
    ///     <tr>
    ///         <td><code>allIdleTime</code></td>
    ///         <td>an <see cref="IdleStateEvent"/> whose state is <see cref="IdleState.AllIdle"/>
    ///             will be triggered when neither read nor write was performed for the
    ///             specified period of time.  Specify <code>0</code> to disable.</td>
    ///     </tr>
    /// </table>
    /// 
    /// <para>
    /// 
    /// <example>
    ///
    /// An example that sends a ping message when there is no outbound traffic
    /// for 30 seconds.  The connection is closed when there is no inbound traffic
    /// for 60 seconds.
    ///
    /// <c>
    /// var bootstrap = new <see cref="DotNetty.Transport.Bootstrapping.ServerBootstrap"/>();
    ///
    /// bootstrap.ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
    /// {
    ///     IChannelPipeline pipeline = channel.Pipeline;
    ///     
    ///     pipeline.AddLast("idleStateHandler", new <see cref="IdleStateHandler"/>(60, 30, 0);
    ///     pipeline.AddLast("myHandler", new MyHandler());
    /// }    
    /// </c>
    /// 
    /// Handler should handle the <see cref="IdleStateEvent"/>  triggered by <see cref="IdleStateHandler"/>.
    /// 
    /// <c>
    /// public class MyHandler : ChannelDuplexHandler 
    /// {
    ///     public override void UserEventTriggered(<see cref="IChannelHandlerContext"/> context, <see cref="object"/> evt)
    ///     {
    ///         if(evt is <see cref="IdleStateEvent"/>) 
    ///         {
    ///             <see cref="IdleStateEvent"/> e = (<see cref="IdleStateEvent"/>) evt;
    ///             if (e.State == <see cref="IdleState"/>.ReaderIdle) 
    ///             {
    ///                 ctx.close();
    ///             } 
    ///             else if(e.State == <see cref="IdleState"/>.WriterIdle) 
    ///             {
    ///                 ctx.writeAndFlush(new PingMessage());
    ///             }
    ///          }
    ///      }
    /// }
    /// </c>
    /// </example>
    /// 
    /// <seealso cref="ReadTimeoutHandler"/>
    /// <seealso cref="WriteTimeoutHandler"/>
    /// </summary>
    public class IdleStateHandler : ChannelDuplexHandler
    {
        static readonly TimeSpan MinTimeout = TimeSpan.FromMilliseconds(1);

        readonly Action<Task> writeListener;

        readonly TimeSpan readerIdleTime;
        readonly TimeSpan writerIdleTime;
        readonly TimeSpan allIdleTime;

        volatile IScheduledTask readerIdleTimeout;
        TimeSpan lastReadTime;
        bool firstReaderIdleEvent = true;

        volatile IScheduledTask writerIdleTimeout;
        TimeSpan lastWriteTime;
        bool firstWriterIdleEvent = true;

        volatile IScheduledTask allIdleTimeout;
        bool firstAllIdleEvent = true;

        volatile int state;
        // 0 - none, 1 - initialized, 2 - destroyed
        volatile bool reading;

        static readonly Action<object, object> ReadTimeoutAction = HandleReadTimeout;
        static readonly Action<object, object> WriteTimeoutAction = HandleWriteTimeout;
        static readonly Action<object, object> AllTimeoutAction = HandleAllTimeout;

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetty.Handlers.Timeout.IdleStateHandler"/> class.
        /// </summary>
        /// <param name="readerIdleTimeSeconds">Reader idle time seconds.</param>
        /// <param name="writerIdleTimeSeconds">Writer idle time seconds.</param>
        /// <param name="allIdleTimeSeconds">All idle time seconds.</param>
        public IdleStateHandler(
            int readerIdleTimeSeconds,
            int writerIdleTimeSeconds,
            int allIdleTimeSeconds)
            : this(TimeSpan.FromSeconds(readerIdleTimeSeconds), 
                   TimeSpan.FromSeconds(writerIdleTimeSeconds), 
                   TimeSpan.FromSeconds(allIdleTimeSeconds))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetty.Handlers.Timeout.IdleStateHandler"/> class.
        /// </summary>
        /// <param name="readerIdleTime">Reader idle time.</param>
        /// <param name="writerIdleTime">Writer idle time.</param>
        /// <param name="allIdleTime">All idle time.</param>
        public IdleStateHandler(TimeSpan readerIdleTime, TimeSpan writerIdleTime, TimeSpan allIdleTime)
        {
            this.readerIdleTime = readerIdleTime != TimeSpan.Zero
                ? TimeUtil.Max(readerIdleTime, IdleStateHandler.MinTimeout)
                : TimeSpan.Zero;

            this.writerIdleTime = writerIdleTime != TimeSpan.Zero
                ? TimeUtil.Max(writerIdleTime, IdleStateHandler.MinTimeout)
                : TimeSpan.Zero;

            this.allIdleTime = allIdleTime != TimeSpan.Zero
                ? TimeUtil.Max(allIdleTime, IdleStateHandler.MinTimeout)
                : TimeSpan.Zero;

            this.writeListener = new Action<Task>(antecedent =>
                {
                    this.lastWriteTime = TimeUtil.GetSystemTime();
                    this.firstWriterIdleEvent = this.firstAllIdleEvent = true;
                });
        }

        /// <summary>
        /// Return the readerIdleTime that was given when instance this class in milliseconds.
        /// </summary>
        /// <returns>The reader idle time in millis.</returns>
        public TimeSpan ReaderIdleTime
        {
            get{ return this.readerIdleTime; }
        }

        /// <summary>
        /// Return the writerIdleTime that was given when instance this class in milliseconds.
        /// </summary>
        /// <returns>The writer idle time in millis.</returns>
        public TimeSpan WriterIdleTime
        {
            get{ return this.writerIdleTime; }
        }

        /// <summary>
        /// Return the allIdleTime that was given when instance this class in milliseconds.
        /// </summary>
        /// <returns>The all idle time in millis.</returns>
        public TimeSpan AllIdleTime
        {
            get{ return this.allIdleTime; }
        }

        public override void HandlerAdded(IChannelHandlerContext context)
        {
            if (context.Channel.Active && context.Channel.Registered)
            {
                // channelActvie() event has been fired already, which means this.channelActive() will
                // not be invoked. We have to initialize here instead.
                this.Initialize(context);
            }
            else
            {
                // channelActive() event has not been fired yet.  this.channelActive() will be invoked
                // and initialization will occur there.
            }
        }

        public override void HandlerRemoved(IChannelHandlerContext context)
        {
            this.Destroy();
        }

        public override void ChannelRegistered(IChannelHandlerContext context)
        {
            // Initialize early if channel is active already.
            if (context.Channel.Active)
            {
                this.Initialize(context);
            }

            base.ChannelRegistered(context);
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            // This method will be invoked only if this handler was added
            // before channelActive() event is fired.  If a user adds this handler
            // after the channelActive() event, initialize() will be called by beforeAdd().
            this.Initialize(context);
            base.ChannelActive(context);
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            this.Destroy();
            base.ChannelInactive(context);
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            if (this.readerIdleTime.Ticks > 0 || this.allIdleTime.Ticks > 0)
            {
                this.reading = true;
                this.firstReaderIdleEvent = this.firstAllIdleEvent = true;
            }

            context.FireChannelRead(message);
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            if (this.readerIdleTime.Ticks > 0 || this.allIdleTime.Ticks > 0)
            {
                this.lastReadTime = TimeUtil.GetSystemTime();
                this.reading = false;
            }

            context.FireChannelReadComplete();
        }

        public override Task WriteAsync(IChannelHandlerContext context, object message)
        {
            if (this.writerIdleTime.Ticks > 0 || this.allIdleTime.Ticks > 0)
            {
                Task task = context.WriteAsync(message);
                task.ContinueWith(this.writeListener);

                return task;
            }

            return context.WriteAsync(message);
        }

        void Initialize(IChannelHandlerContext context)
        {
            // Avoid the case where destroy() is called before scheduling timeouts.
            // See: https://github.com/netty/netty/issues/143
            switch (this.state)
            {
                case 1:
                case 2:
                    return;
            }

            this.state = 1;

            IEventExecutor executor = context.Executor;

            this.lastReadTime = this.lastWriteTime = TimeUtil.GetSystemTime();
            if (this.readerIdleTime.Ticks > 0)
            {
                this.readerIdleTimeout = executor.Schedule(ReadTimeoutAction, this, context, 
                    this.readerIdleTime);
            }

            if (this.writerIdleTime.Ticks > 0)
            {
                this.writerIdleTimeout = executor.Schedule(WriteTimeoutAction, this, context, 
                    this.writerIdleTime);
            }

            if (this.allIdleTime.Ticks > 0)
            {
                this.allIdleTimeout = executor.Schedule(AllTimeoutAction, this, context, 
                    this.allIdleTime);
            }
        }

        void Destroy()
        {
            this.state = 2;

            if (this.readerIdleTimeout != null)
            {
                this.readerIdleTimeout.Cancel();
                this.readerIdleTimeout = null;
            }

            if (this.writerIdleTimeout != null)
            {
                this.writerIdleTimeout.Cancel();
                this.writerIdleTimeout = null;
            }

            if (this.allIdleTimeout != null)
            {
                this.allIdleTimeout.Cancel();
                this.allIdleTimeout = null;
            }
        }

        /// <summary>
        /// Is called when an <see cref="IdleStateEvent"/> should be fired. This implementation calls
        /// <see cref="IChannelHandlerContext.FireUserEventTriggered(object)"/>.
        /// </summary>
        /// <param name="context">Context.</param>
        /// <param name="stateEvent">Evt.</param>
        protected void ChannelIdle(IChannelHandlerContext context, IdleStateEvent stateEvent)
        {
            context.FireUserEventTriggered(stateEvent);
        }

        static void HandleReadTimeout(object handler, object ctx)
        {
            var self = (IdleStateHandler)handler; // instead of this
            var context = (IChannelHandlerContext)ctx;

            if (!context.Channel.Open)
            {
                return;
            }

            TimeSpan nextDelay = self.readerIdleTime;

            if (!self.reading)
            {
                nextDelay -= TimeUtil.GetSystemTime() - self.lastReadTime;
            }

            if (nextDelay.Ticks <= 0)
            {
                // Reader is idle - set a new timeout and notify the callback.
                self.readerIdleTimeout = 
                    context.Executor.Schedule(ReadTimeoutAction, self, context, 
                        self.readerIdleTime);

                try
                {
                    IdleStateEvent stateEvent;
                    if (self.firstReaderIdleEvent)
                    {
                        self.firstReaderIdleEvent = false;
                        stateEvent = IdleStateEvent.FirstReaderIdleStateEvent;
                    }
                    else
                    {
                        stateEvent = IdleStateEvent.ReaderIdleStateEvent;
                    }

                    self.ChannelIdle(context, stateEvent);
                }
                catch (Exception ex)
                {
                    context.FireExceptionCaught(ex);
                }
            }
            else
            {
                self.readerIdleTimeout = context.Executor.Schedule(ReadTimeoutAction, self, context, 
                    nextDelay);
            }
        }

        static void HandleWriteTimeout(object handler, object ctx)
        {
            var self = (IdleStateHandler)handler;
            var context = (IChannelHandlerContext)ctx;

            if (!context.Channel.Open)
            {
                return;
            }

            TimeSpan lastWriteTime = self.lastWriteTime;
            TimeSpan nextDelay = self.writerIdleTime - (TimeUtil.GetSystemTime() - lastWriteTime);

            if (nextDelay.Ticks <= 0)
            {
                self.writerIdleTimeout = context.Executor.Schedule(WriteTimeoutAction, self, context,
                    self.writerIdleTime);

                try
                {
                    IdleStateEvent stateEvent;
                    if (self.firstWriterIdleEvent)
                    {
                        self.firstWriterIdleEvent = false;
                        stateEvent = IdleStateEvent.FirstWriterIdleStateEvent;
                    }
                    else
                    {
                        stateEvent = IdleStateEvent.WriterIdleStateEvent;
                    }

                    self.ChannelIdle(context, stateEvent);
                }
                catch (Exception ex)
                {
                    context.FireExceptionCaught(ex);
                }
            }
            else
            {
                self.writerIdleTimeout = context.Executor.Schedule(WriteTimeoutAction, self, context, nextDelay);
            }
        }
           
        static void HandleAllTimeout(object handler, object ctx)
        {
            var self = (IdleStateHandler)handler;
            var context = (IChannelHandlerContext)ctx;

            if (!context.Channel.Open)
            {
                return;
            }

            TimeSpan nextDelay = self.allIdleTime;
            if (!self.reading)
            {
                nextDelay -= TimeUtil.GetSystemTime() - TimeUtil.Max(self.lastReadTime, self.lastWriteTime);
            }

            if (nextDelay.Ticks <= 0)
            {
                self.allIdleTimeout = context.Executor.Schedule(AllTimeoutAction, self, context, 
                    self.allIdleTime);

                try
                {
                    IdleStateEvent stateEvent;
                    if (self.firstAllIdleEvent)
                    {
                        self.firstAllIdleEvent = false;
                        stateEvent = IdleStateEvent.FirstAllIdleStateEvent;
                    }
                    else
                    {
                        stateEvent = IdleStateEvent.AllIdleStateEvent;
                    }

                    self.ChannelIdle(context, stateEvent);
                }
                catch (Exception ex)
                {
                    context.FireExceptionCaught(ex);
                }
            }
            else
            {
                // Either read or write occurred before the timeout - set a new
                // timeout with shorter delay.
                self.allIdleTimeout = context.Executor.Schedule(AllTimeoutAction, self, context, nextDelay);
            }
        }
    }
}

