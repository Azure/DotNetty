// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Timeout
{
    using System;
    using System.Threading;
    using DotNetty.Common.Utilities;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Raises a <see cref="ReadTimeoutException"/> when no data was read within a certain
    /// period of time.
    ///
    /// <pre>
    /// The connection is closed when there is no inbound traffic
    /// for 30 seconds.
    ///
    /// <example>
    /// <c>
    /// var bootstrap = new <see cref="DotNetty.Transport.Bootstrapping.ServerBootstrap"/>();
    ///
    /// bootstrap.ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
    /// {
    ///     IChannelPipeline pipeline = channel.Pipeline;
    ///     
    ///     pipeline.AddLast("readTimeoutHandler", new <see cref="ReadTimeoutHandler"/>(30);
    ///     pipeline.AddLast("myHandler", new MyHandler());
    /// } 
    /// </c>
    ///            
    /// <c>
    /// public class MyHandler : ChannelDuplexHandler 
    /// {
    ///     public override void ExceptionCaught(<see cref="IChannelHandlerContext"/> context, <see cref="Exception"/> exception)
    ///     {
    ///         if(exception is <see cref="ReadTimeoutException"/>) 
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
    /// </example>
    /// 
    /// <seealso cref="WriteTimeoutHandler"/>
    /// <seealso cref="IdleStateHandler"/>
    /// </summary>
    public class ReadTimeoutHandler : ChannelHandlerAdapter
    {
        static readonly TimeSpan MinTimeout = TimeSpan.FromMilliseconds(1);

        readonly TimeSpan timeout;
        TimeSpan lastReadTime;

        volatile IScheduledTask timeoutTask; 
        volatile int state; // 0 - none, 1 - Initialized, 2 - Destroyed;
        volatile bool reading;
        bool closed;

        static readonly Action<object, object> ReadTimeoutAction = HandleReadTimeout;

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetty.Handlers.Timeout.ReadTimeoutHandler"/> class.
        /// </summary>
        /// <param name="timeoutSeconds">Timeout in seconds.</param>
        public ReadTimeoutHandler(int timeoutSeconds)
            : this(TimeSpan.FromSeconds(timeoutSeconds))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetty.Handlers.Timeout.ReadTimeoutHandler"/> class.
        /// </summary>
        /// <param name="timeout">Timeout.</param>
        public ReadTimeoutHandler(TimeSpan timeout)
        {
            this.timeout = 
                TimeUtil.Max(timeout, ReadTimeoutHandler.MinTimeout);
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
            this.reading = true;
            context.FireChannelRead(message);
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            this.lastReadTime = TimeUtil.GetSystemTime();
            this.reading = false;
            context.FireChannelReadComplete();
        }

        private void Initialize(IChannelHandlerContext context)
        {
            switch(this.state)
            {
                case 1:
                case 2:
                    return;
            }

            this.state = 1;

            this.lastReadTime = TimeUtil.GetSystemTime();
            if (this.timeout.Ticks > 0) 
            {
                this.timeoutTask = context.Executor.Schedule(ReadTimeoutAction, this, context, 
                    this.timeout);
            }
        }

        private void Destroy()
        {
            state = 2;

            if(this.timeoutTask != null)
            {
                this.timeoutTask.Cancel();
                this.timeoutTask = null;
            }
        }

        /// <summary>
        /// Is called when a read timeout was detected.
        /// </summary>
        /// <param name="context">Context.</param>
        protected virtual void ReadTimedOut(IChannelHandlerContext context)
        {
            if(!this.closed)
            {
                context.FireExceptionCaught(ReadTimeoutException.Instance);
                context.Flush();
                this.closed = true;
            }
        }

        static void HandleReadTimeout(object handler, object ctx)
        {
            var self = (ReadTimeoutHandler)handler;
            var context = (IChannelHandlerContext)ctx;

            if (!context.Channel.Open)
            {
                return;
            }

            TimeSpan nextDelay = self.timeout;
            if(!self.reading)
            {
                nextDelay -= TimeUtil.GetSystemTime() - self.lastReadTime;
            }

            if(nextDelay.Ticks <= 0)
            {
                // Read timed out - set a new timeout and notify the callback.
                self.timeoutTask = context.Executor.Schedule(ReadTimeoutAction, self, context, 
                    self.timeout);

                try
                {
                    self.ReadTimedOut(context);
                }
                catch(Exception ex)
                {
                    context.FireExceptionCaught(ex);
                }
            }
            else
            {
                self.timeoutTask = context.Executor.Schedule(ReadTimeoutAction, self, context, 
                    nextDelay);
            }
        }
    }
}

