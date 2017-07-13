// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Embedded
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Runtime.ExceptionServices;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    public class EmbeddedChannel : AbstractChannel
    {
        static readonly EndPoint LOCAL_ADDRESS = new EmbeddedSocketAddress();
        static readonly EndPoint REMOTE_ADDRESS = new EmbeddedSocketAddress();

        enum State
        {
            Open,
            Active,
            Closed
        };

        static readonly IChannelHandler[] EMPTY_HANDLERS = new IChannelHandler[0];

        static readonly IInternalLogger logger = InternalLoggerFactory.GetInstance<EmbeddedChannel>();

        static readonly ChannelMetadata METADATA_NO_DISCONNECT = new ChannelMetadata(false);
        static readonly ChannelMetadata METADATA_DISCONNECT = new ChannelMetadata(true);

        readonly EmbeddedEventLoop loop = new EmbeddedEventLoop();

        Queue<object> inboundMessages;
        Queue<object> outboundMessages;
        Exception lastException;
        State state;

        /// <summary>
        ///     Create a new instance with an empty pipeline.
        /// </summary>
        public EmbeddedChannel()
            : this(EmbeddedChannelId.Instance, EMPTY_HANDLERS)
        {
        }

        /// <summary>
        ///     Create a new instance with an empty pipeline with the specified <see cref="IChannelId" />.
        /// </summary>
        /// <param name="channelId">The <see cref="IChannelId" /> of this channel. </param>
        public EmbeddedChannel(IChannelId channelId)
            : this(channelId, EMPTY_HANDLERS)
        {
        }

        /// <summary>
        ///     Create a new instance with the pipeline initialized with the specified handlers.
        /// </summary>
        /// <param name="handlers">
        ///     The <see cref="IChannelHandler" />s that will be added to the <see cref="IChannelPipeline" />
        /// </param>
        public EmbeddedChannel(params IChannelHandler[] handlers)
            : this(EmbeddedChannelId.Instance, handlers)
        {
        }

        public EmbeddedChannel(IChannelId id, params IChannelHandler[] handlers)
            : this(id, false, handlers)
        {
        }

        /// <summary>Create a new instance with the pipeline initialized with the specified handlers.</summary>
        /// <param name="id">The <see cref="IChannelId" /> of this channel.</param>
        /// <param name="hasDisconnect">
        ///     <c>false</c> if this <see cref="IChannel" /> will delegate <see cref="DisconnectAsync" />
        ///     to <see cref="CloseAsync" />, <c>true</c> otherwise.
        /// </param>
        /// <param name="handlers">
        ///     The <see cref="IChannelHandler" />s that will be added to the <see cref="IChannelPipeline" />
        /// </param>
        public EmbeddedChannel(IChannelId id, bool hasDisconnect, params IChannelHandler[] handlers)
            : this(id, hasDisconnect, true, handlers)
        { }

        public EmbeddedChannel(IChannelId id, bool hasDisconnect, bool start, params IChannelHandler[] handlers)
            : base(null, id)
        {
            this.Metadata = hasDisconnect ? METADATA_DISCONNECT : METADATA_NO_DISCONNECT;
            this.Configuration = new DefaultChannelConfiguration(this);
            if (handlers == null)
            {
                throw new ArgumentNullException(nameof(handlers));
            }

            IChannelPipeline p = this.Pipeline;
            p.AddLast(new ActionChannelInitializer<IChannel>(channel =>
            {
                IChannelPipeline pipeline = channel.Pipeline;
                foreach (IChannelHandler h in handlers)
                {
                    if (h == null)
                    {
                        break;
                    }
                    pipeline.AddLast(h);
                }
            }));

            if (start)
            {
                this.Start();
            }
        }

        public void Start()
        {
            Task future = this.loop.RegisterAsync(this);
            Debug.Assert(future.IsCompleted);
            this.Pipeline.AddLast(new LastInboundHandler(this.InboundMessages, this.RecordException));
        }

        protected sealed override DefaultChannelPipeline NewChannelPipeline() => new EmbeddedChannelPipeline(this);

        public override ChannelMetadata Metadata { get; }

        public override IChannelConfiguration Configuration { get; }

        /// <summary>
        ///     Returns the <see cref="Queue{T}" /> which holds all of the <see cref="object" />s that
        ///     were received by this <see cref="IChannel" />.
        /// </summary>
        public Queue<object> InboundMessages => this.inboundMessages ?? (this.inboundMessages = new Queue<object>());

        /// <summary>
        ///     Returns the <see cref="Queue{T}" /> which holds all of the <see cref="object" />s that
        ///     were written by this <see cref="IChannel" />.
        /// </summary>
        public Queue<object> OutboundMessages => this.outboundMessages ?? (this.outboundMessages = new Queue<object>());

        /// <summary>
        /// Return received data from this <see cref="IChannel"/>.
        /// </summary>
        public T ReadInbound<T>() => (T)Poll(this.inboundMessages);

        /// <summary>
        /// Read data from the outbound. This may return <c>null</c> if nothing is readable.
        /// </summary>
        public T ReadOutbound<T>() => (T)Poll(this.outboundMessages);

        protected override EndPoint LocalAddressInternal => this.Active ? LOCAL_ADDRESS : null;

        protected override EndPoint RemoteAddressInternal => this.Active ? REMOTE_ADDRESS : null;

        protected override IChannelUnsafe NewUnsafe() => new DefaultUnsafe(this);

        protected override bool IsCompatible(IEventLoop eventLoop) => eventLoop is EmbeddedEventLoop;

        protected override void DoBind(EndPoint localAddress)
        {
            //NOOP
        }

        protected override void DoRegister() => this.state = State.Active;

        protected override void DoDisconnect() => this.DoClose();

        protected override void DoClose() => this.state = State.Closed;

        protected override void DoBeginRead()
        {
            //NOOP
        }

        protected override void DoWrite(ChannelOutboundBuffer input)
        {
            for (;;)
            {
                object msg = input.Current;
                if (msg == null)
                {
                    break;
                }

                ReferenceCountUtil.Retain(msg);
                this.OutboundMessages.Enqueue(msg);
                input.Remove();
            }
        }

        public override bool Open => this.state != State.Closed;

        public override bool Active => this.state == State.Active;

        /// <summary>
        ///     Run all tasks (which also includes scheduled tasks) that are pending in the <see cref="IEventLoop" />
        ///     for this <see cref="IChannel" />.
        /// </summary>
        public void RunPendingTasks()
        {
            try
            {
                this.loop.RunTasks();
            }
            catch (Exception ex)
            {
                this.RecordException(ex);
            }

            try
            {
                this.loop.RunScheduledTasks();
            }
            catch (Exception ex)
            {
                this.RecordException(ex);
            }
        }

        /// <summary>
        ///     Run all pending scheduled tasks in the <see cref="IEventLoop" /> for this <see cref="IChannel" />.
        /// </summary>
        /// <returns>
        ///     The <see cref="PreciseTimeSpan" /> when the next scheduled task is ready to run. If no other task is
        ///     scheduled then it will return <see cref="PreciseTimeSpan.Zero" />.
        /// </returns>
        public PreciseTimeSpan RunScheduledPendingTasks()
        {
            try
            {
                return this.loop.RunScheduledTasks();
            }
            catch (Exception ex)
            {
                this.RecordException(ex);
                return this.loop.NextScheduledTask();
            }
        }

        /// <summary>
        ///     Write messages to the inbound of this <see cref="IChannel" />
        /// </summary>
        /// <param name="msgs">The messages to be written.</param>
        /// <returns><c>true</c> if the write operation did add something to the inbound buffer</returns>
        public bool WriteInbound(params object[] msgs)
        {
            this.EnsureOpen();
            if (msgs.Length == 0)
            {
                return IsNotEmpty(this.inboundMessages);
            }

            IChannelPipeline p = this.Pipeline;
            foreach (object m in msgs)
            {
                p.FireChannelRead(m);
            }
            p.FireChannelReadComplete();
            this.RunPendingTasks();
            this.CheckException();
            return IsNotEmpty(this.inboundMessages);
        }

        /// <summary>
        ///     Write messages to the outbound of this <see cref="IChannel" />.
        /// </summary>
        /// <param name="msgs">The messages to be written.</param>
        /// <returns><c>true</c> if the write operation did add something to the inbound buffer</returns>
        public bool WriteOutbound(params object[] msgs)
        {
            this.EnsureOpen();
            if (msgs.Length == 0)
            {
                return IsNotEmpty(this.outboundMessages);
            }

            ThreadLocalObjectList futures = ThreadLocalObjectList.NewInstance(msgs.Length);

            foreach (object m in msgs)
            {
                if (m == null)
                {
                    break;
                }
                futures.Add(this.WriteAsync(m));
            }
            // We need to call RunPendingTasks first as a IChannelHandler may have used IEventLoop.Execute(...) to
            // delay the write on the next event loop run.
            this.RunPendingTasks();
            this.Flush();

            int size = futures.Count;
            for (int i = 0; i < size; i++)
            {
                var future = (Task)futures[i];
                if (future.IsCompleted)
                {
                    this.RecordException(future);
                }
                else
                {
                    // The write may be delayed to run later by runPendingTasks()
                    future.ContinueWith(t => this.RecordException(t));
                }
                if (future.Exception != null)
                {
                    this.RecordException(future.Exception);
                }
            }

            this.RunPendingTasks();
            this.CheckException();
            return IsNotEmpty(this.outboundMessages);
        }

        void RecordException(Task future)
        {
            switch (future.Status)
            {
                case TaskStatus.Canceled:
                case TaskStatus.Faulted:
                    this.RecordException(future.Exception);
                    break;
                default:
                    break;
            }
        }

        void RecordException(Exception cause)
        {
            if (this.lastException == null)
            {
                this.lastException = cause;
            }
            else
            {
                logger.Warn("More than one exception was raised. " + "Will report only the first one and log others.", cause);
            }
        }

        /// <summary>
        ///     Mark this <see cref="IChannel" /> as finished. Any further try to write data to it will fail.
        /// </summary>
        /// <returns>bufferReadable returns <c>true</c></returns>
        public bool Finish() => this.Finish(false);

        /**
         * Mark this {@link Channel} as finished and release all pending message in the inbound and outbound buffer.
         * Any futher try to write data to it will fail.
         *
         * @return bufferReadable returns {@code true} if any of the used buffers has something left to read
         */
        public bool FinishAndReleaseAll() => this.Finish(true);

        /**
         * Mark this {@link Channel} as finished. Any futher try to write data to it will fail.
         *
         * @param releaseAll if {@code true} all pending message in the inbound and outbound buffer are released.
         * @return bufferReadable returns {@code true} if any of the used buffers has something left to read
         */
        bool Finish(bool releaseAll)
        {
            this.CloseSafe();
            try
            {
                this.CheckException();
                return IsNotEmpty(this.inboundMessages) || IsNotEmpty(this.outboundMessages);
            }
            finally
            {
                if (releaseAll)
                {
                    ReleaseAll(this.inboundMessages);
                    ReleaseAll(this.outboundMessages);
                }
            }
        }

        /**
         * Release all buffered inbound messages and return {@code true} if any were in the inbound buffer, {@code false}
         * otherwise.
         */
        public bool ReleaseInbound() => ReleaseAll(this.inboundMessages);

        /**
         * Release all buffered outbound messages and return {@code true} if any were in the outbound buffer, {@code false}
         * otherwise.
         */
        public bool ReleaseOutbound() => ReleaseAll(this.outboundMessages);

        static bool ReleaseAll(Queue<object> queue)
        {
            if (queue.Count > 0)
            {
                for (;;)
                {
                    if (queue.Count == 0)
                    {
                        break;
                    }
                    object msg = queue.Dequeue();
                    ReferenceCountUtil.Release(msg);
                }
                return true;
            }
            return false;
        }

        void FinishPendingTasks(bool cancel)
        {
            this.RunPendingTasks();
            if (cancel)
            {
                // Cancel all scheduled tasks that are left.
                this.loop.CancelScheduledTasks();
            }
        }

        public override Task CloseAsync()
        {
            // We need to call RunPendingTasks() before calling super.CloseAsync() as there may be something in the queue
            // that needs to be run before the actual close takes place.
            this.RunPendingTasks();
            Task future = base.CloseAsync();

            // Now finish everything else and cancel all scheduled tasks that were not ready set.
            this.FinishPendingTasks(true);
            return future;
        }

        public override Task DisconnectAsync()
        {
            Task future = base.DisconnectAsync();
            this.FinishPendingTasks(false); // todo
            return future;
        }

        static bool IsNotEmpty(Queue<object> queue) => queue != null && queue.Count > 0;

        /// <summary>
        ///     Check to see if there was any <see cref="Exception" /> and rethrow if so.
        /// </summary>
        public void CheckException()
        {
            Exception e = this.lastException;
            if (e == null)
            {
                return;
            }

            this.lastException = null;
            ExceptionDispatchInfo.Capture(e).Throw();
        }

        /// <summary>
        ///     Ensure the <see cref="IChannel" /> is open and if not throw an exception.
        /// </summary>
        protected void EnsureOpen()
        {
            if (!this.Open)
            {
                this.RecordException(new ClosedChannelException());
                this.CheckException();
            }
        }

        static object Poll(Queue<object> queue) => IsNotEmpty(queue) ? queue.Dequeue() : null;

        class DefaultUnsafe : AbstractUnsafe
        {
            public DefaultUnsafe(AbstractChannel channel)
                : base(channel)
            {
            }

            public override Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress) => TaskEx.Completed;
        }

        internal sealed class LastInboundHandler : ChannelHandlerAdapter
        {
            readonly Queue<object> inboundMessages;
            readonly Action<Exception> recordException;

            public LastInboundHandler(Queue<object> inboundMessages, Action<Exception> recordException)
            {
                this.inboundMessages = inboundMessages;
                this.recordException = recordException;
            }

            public override void ChannelRead(IChannelHandlerContext context, object message) => this.inboundMessages.Enqueue(message);

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) => this.recordException(exception);
        }

        sealed class EmbeddedChannelPipeline : DefaultChannelPipeline
        {
            public EmbeddedChannelPipeline(EmbeddedChannel channel)
                : base(channel)
            {
            }

            protected override void OnUnhandledInboundException(Exception cause) => ((EmbeddedChannel)this.Channel).RecordException(cause);

            protected override void OnUnhandledInboundMessage(object msg) => ((EmbeddedChannel)this.Channel).InboundMessages.Enqueue(msg);
        }
    }
}