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
    using System.Runtime.CompilerServices;

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
    /// </para>
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
    /// bootstrap.ChildHandler(new ActionChannelInitializer&lt;ISocketChannel&gt;(channel =>
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
    /// </para>
    /// 
    /// <seealso cref="ReadTimeoutHandler"/>
    /// <seealso cref="WriteTimeoutHandler"/>
    /// </summary>
    public class IdleStateHandler : ChannelDuplexHandler
    {
        static readonly TimeSpan MinTimeout = TimeSpan.FromMilliseconds(1);

        readonly Action writeListener;

        readonly bool observeOutput;
        readonly TimeSpan readerIdleTime;
        readonly TimeSpan writerIdleTime;
        readonly TimeSpan allIdleTime;

        IScheduledTask readerIdleTimeout;
        TimeSpan lastReadTime;
        bool firstReaderIdleEvent = true;

        IScheduledTask writerIdleTimeout;
        TimeSpan lastWriteTime;
        bool firstWriterIdleEvent = true;

        IScheduledTask allIdleTimeout;
        bool firstAllIdleEvent = true;

        // 0 - none, 1 - initialized, 2 - destroyed
        byte state;
        bool reading;

        TimeSpan lastChangeCheckTimeStamp;
        int lastMessageHashCode;
        long lastPendingWriteBytes;

        static readonly Action<object, object> ReadTimeoutAction = WrapperTimeoutHandler(HandleReadTimeout);
        static readonly Action<object, object> WriteTimeoutAction = WrapperTimeoutHandler(HandleWriteTimeout);
        static readonly Action<object, object> AllTimeoutAction = WrapperTimeoutHandler(HandleAllTimeout);

        /// <summary>
        /// Initializes a new instance firing <see cref="IdleStateEvent"/>s.
        /// </summary>
        /// <param name="readerIdleTimeSeconds">
        ///     an <see cref="IdleStateEvent"/> whose state is <see cref="IdleState.ReaderIdle"/>
        ///     will be triggered when no read was performed for the specified
        ///     period of time.  Specify <code>0</code> to disable.
        /// </param>
        /// <param name="writerIdleTimeSeconds">
        ///     an <see cref="IdleStateEvent"/> whose state is <see cref="IdleState.WriterIdle"/>
        ///     will be triggered when no write was performed for the specified
        ///     period of time.  Specify <code>0</code> to disable.
        /// </param>
        /// <param name="allIdleTimeSeconds">
        ///     an <see cref="IdleStateEvent"/> whose state is <see cref="IdleState.AllIdle"/>
        ///     will be triggered when neither read nor write was performed for
        ///     the specified period of time.  Specify <code>0</code> to disable.
        /// </param>
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
        /// <see cref="IdleStateHandler.IdleStateHandler(bool, TimeSpan, TimeSpan, TimeSpan)"/>
        /// </summary>
        public IdleStateHandler(TimeSpan readerIdleTime, TimeSpan writerIdleTime, TimeSpan allIdleTime)
            : this(false, readerIdleTime, writerIdleTime, allIdleTime)
        {
        }

        /// <summary>
        /// Initializes a new instance firing <see cref="IdleStateEvent"/>s.
        /// </summary>
        /// <param name="observeOutput">
        ///     whether or not the consumption of <code>bytes</code> should be taken into
        ///     consideration when assessing write idleness. The default is <code>false</code>.
        /// </param>
        /// <param name="readerIdleTime">
        ///     an <see cref="IdleStateEvent"/> whose state is <see cref="IdleState.ReaderIdle"/>
        ///     will be triggered when no read was performed for the specified
        ///     period of time.  Specify <see cref="TimeSpan.Zero"/> to disable.
        /// </param>
        /// <param name="writerIdleTime">
        ///     an <see cref="IdleStateEvent"/> whose state is <see cref="IdleState.WriterIdle"/>
        ///     will be triggered when no write was performed for the specified
        ///     period of time.  Specify <see cref="TimeSpan.Zero"/> to disable.
        /// </param>
        /// <param name="allIdleTime">
        ///     an <see cref="IdleStateEvent"/> whose state is <see cref="IdleState.AllIdle"/>
        ///     will be triggered when neither read nor write was performed for
        ///     the specified period of time.  Specify <see cref="TimeSpan.Zero"/> to disable.
        /// </param>
        public IdleStateHandler(bool observeOutput,
            TimeSpan readerIdleTime, TimeSpan writerIdleTime, TimeSpan allIdleTime)
        {
            this.observeOutput = observeOutput;

            this.readerIdleTime = readerIdleTime > TimeSpan.Zero
                ? TimeUtil.Max(readerIdleTime, IdleStateHandler.MinTimeout)
                : TimeSpan.Zero;

            this.writerIdleTime = writerIdleTime > TimeSpan.Zero
                ? TimeUtil.Max(writerIdleTime, IdleStateHandler.MinTimeout)
                : TimeSpan.Zero;

            this.allIdleTime = allIdleTime > TimeSpan.Zero
                ? TimeUtil.Max(allIdleTime, IdleStateHandler.MinTimeout)
                : TimeSpan.Zero;

            this.writeListener = new Action(() =>
                {
                    this.lastWriteTime = this.Ticks();
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
                // channelActive() event has been fired already, which means this.channelActive() will
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
            if ((this.readerIdleTime.Ticks > 0 || this.allIdleTime.Ticks > 0) && reading)
            {
                this.lastReadTime = this.Ticks();
                this.reading = false;
            }

            context.FireChannelReadComplete();
        }

        public override ChannelFuture WriteAsync(IChannelHandlerContext context, object message)
        {
            if (this.writerIdleTime.Ticks > 0 || this.allIdleTime.Ticks > 0)
            {
                ChannelFuture task = context.WriteAsync(message);
                //task.ContinueWith(this.writeListener, TaskContinuationOptions.ExecuteSynchronously);
                task.OnCompleted(this.writeListener);

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
            this.InitOutputChanged(context);

            this.lastReadTime = this.lastWriteTime = this.Ticks();
            if (this.readerIdleTime.Ticks > 0)
            {
                this.readerIdleTimeout = this.Schedule(context, ReadTimeoutAction, this, context, 
                    this.readerIdleTime);
            }

            if (this.writerIdleTime.Ticks > 0)
            {
                this.writerIdleTimeout = this.Schedule(context, WriteTimeoutAction, this, context, 
                    this.writerIdleTime);
            }

            if (this.allIdleTime.Ticks > 0)
            {
                this.allIdleTimeout = this.Schedule(context, AllTimeoutAction, this, context, 
                    this.allIdleTime);
            }
        }

        /// <summary>
        /// This method is visible for testing!
        /// </summary>
        /// <returns></returns>
        internal virtual TimeSpan Ticks() => TimeUtil.GetSystemTime();

        /// <summary>
        /// This method is visible for testing!
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="task"></param>
        /// <param name="context"></param>
        /// <param name="state"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        internal virtual IScheduledTask Schedule(IChannelHandlerContext ctx, Action<object, object> task,
            object context, object state, TimeSpan delay)
        {
            return ctx.Executor.Schedule(task, context, state, delay);
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
        protected virtual void ChannelIdle(IChannelHandlerContext context, IdleStateEvent stateEvent)
        {
            context.FireUserEventTriggered(stateEvent);
        }

        /// <summary>
        /// Returns a <see cref="IdleStateEvent"/>.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="first"></param>
        /// <returns></returns>
        protected virtual IdleStateEvent NewIdleStateEvent(IdleState state, bool first)
        {
            switch (state)
            {
                case IdleState.AllIdle:
                    return first ? IdleStateEvent.FirstAllIdleStateEvent : IdleStateEvent.AllIdleStateEvent;
                case IdleState.ReaderIdle:
                    return first ? IdleStateEvent.FirstReaderIdleStateEvent : IdleStateEvent.ReaderIdleStateEvent;
                case IdleState.WriterIdle:
                    return first ? IdleStateEvent.FirstWriterIdleStateEvent : IdleStateEvent.WriterIdleStateEvent;
                default:
                    throw new ArgumentException("Unhandled: state=" + state + ", first=" + first);
            }
        }

        /// <summary>
        /// <see cref="HasOutputChanged(IChannelHandlerContext, bool)"/>
        /// </summary>
        /// <param name="ctx"></param>
        private void InitOutputChanged(IChannelHandlerContext ctx)
        {
            if (observeOutput)
            {
                ChannelOutboundBuffer buf = ctx.Channel.Unsafe.OutboundBuffer;

                if (buf != null)
                {
                    lastMessageHashCode = RuntimeHelpers.GetHashCode(buf.Current);
                    lastPendingWriteBytes = buf.TotalPendingWriteBytes();
                }
            }
        }

        /// <summary>
        /// Returns <code>true</code> if and only if the <see cref="IdleStateHandler.IdleStateHandler(bool, TimeSpan, TimeSpan, TimeSpan)"/>
        /// was constructed
        /// with <code>observeOutput</code> enabled and there has been an observed change in the
        /// <see cref="ChannelOutboundBuffer"/> between two consecutive calls of this method.
        /// https://github.com/netty/netty/issues/6150
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="first"></param>
        /// <returns></returns>
        private bool HasOutputChanged(IChannelHandlerContext ctx, bool first)
        {
            if (observeOutput)
            {

                // We can take this shortcut if the ChannelPromises that got passed into write()
                // appear to complete. It indicates "change" on message level and we simply assume
                // that there's change happening on byte level. If the user doesn't observe channel
                // writability events then they'll eventually OOME and there's clearly a different
                // problem and idleness is least of their concerns.
                if (lastChangeCheckTimeStamp != lastWriteTime)
                {
                    lastChangeCheckTimeStamp = lastWriteTime;

                    // But this applies only if it's the non-first call.
                    if (!first)
                    {
                        return true;
                    }
                }

                ChannelOutboundBuffer buf = ctx.Channel.Unsafe.OutboundBuffer;

                if (buf != null)
                {
                    int messageHashCode = RuntimeHelpers.GetHashCode(buf.Current);
                    long pendingWriteBytes = buf.TotalPendingWriteBytes();

                    if (messageHashCode != lastMessageHashCode || pendingWriteBytes != lastPendingWriteBytes)
                    {
                        lastMessageHashCode = messageHashCode;
                        lastPendingWriteBytes = pendingWriteBytes;

                        if (!first)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        static Action<object, object> WrapperTimeoutHandler(Action<IdleStateHandler, IChannelHandlerContext> action)
        {
            return (handler, ctx) =>
            {
                var self = (IdleStateHandler)handler; // instead of this
                var context = (IChannelHandlerContext)ctx;

                if (!context.Channel.Open)
                {
                    return;
                }

                action(self, context);
            };
        }

        static void HandleReadTimeout(IdleStateHandler self, IChannelHandlerContext context)
        {
            TimeSpan nextDelay = self.readerIdleTime;

            if (!self.reading)
            {
                nextDelay -= self.Ticks() - self.lastReadTime;
            }

            if (nextDelay.Ticks <= 0)
            {
                // Reader is idle - set a new timeout and notify the callback.
                self.readerIdleTimeout = 
                    self.Schedule(context, ReadTimeoutAction, self, context, 
                        self.readerIdleTime);

                bool first = self.firstReaderIdleEvent;
                self.firstReaderIdleEvent = false;

                try
                {
                    IdleStateEvent stateEvent = self.NewIdleStateEvent(IdleState.ReaderIdle, first);
                    self.ChannelIdle(context, stateEvent);
                }
                catch (Exception ex)
                {
                    context.FireExceptionCaught(ex);
                }
            }
            else
            {
                // Read occurred before the timeout - set a new timeout with shorter delay.
                self.readerIdleTimeout = self.Schedule(context, ReadTimeoutAction, self, context, 
                    nextDelay);
            }
        }

        static void HandleWriteTimeout(IdleStateHandler self, IChannelHandlerContext context)
        {
            TimeSpan lastWriteTime = self.lastWriteTime;
            TimeSpan nextDelay = self.writerIdleTime - (self.Ticks() - lastWriteTime);

            if (nextDelay.Ticks <= 0)
            {
                // Writer is idle - set a new timeout and notify the callback.
                self.writerIdleTimeout = self.Schedule(context, WriteTimeoutAction, self, context,
                    self.writerIdleTime);

                bool first = self.firstWriterIdleEvent;
                self.firstWriterIdleEvent = false;

                try
                {
                    if (self.HasOutputChanged(context, first))
                    {
                        return;
                    }

                    IdleStateEvent stateEvent = self.NewIdleStateEvent(IdleState.WriterIdle, first);
                    self.ChannelIdle(context, stateEvent);
                }
                catch (Exception ex)
                {
                    context.FireExceptionCaught(ex);
                }
            }
            else
            {
                // Write occurred before the timeout - set a new timeout with shorter delay.
                self.writerIdleTimeout = self.Schedule(context, WriteTimeoutAction, self, context, nextDelay);
            }
        }
           
        static void HandleAllTimeout(IdleStateHandler self, IChannelHandlerContext context)
        {
            TimeSpan nextDelay = self.allIdleTime;
            if (!self.reading)
            {
                nextDelay -= self.Ticks() - TimeUtil.Max(self.lastReadTime, self.lastWriteTime);
            }

            if (nextDelay.Ticks <= 0)
            {
                // Both reader and writer are idle - set a new timeout and
                // notify the callback.
                self.allIdleTimeout = self.Schedule(context, AllTimeoutAction, self, context, 
                    self.allIdleTime);

                bool first = self.firstAllIdleEvent;
                self.firstAllIdleEvent = false;

                try
                {
                    if (self.HasOutputChanged(context, first))
                    {
                        return;
                    }

                    IdleStateEvent stateEvent = self.NewIdleStateEvent(IdleState.AllIdle, first);
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
                self.allIdleTimeout = self.Schedule(context, AllTimeoutAction, self, context, nextDelay);
            }
        }
    }
}

