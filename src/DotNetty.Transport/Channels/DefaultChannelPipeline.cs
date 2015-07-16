// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;

    sealed class DefaultChannelPipeline : IChannelPipeline
    {
        //static readonly InternalLogger logger = InternalLoggerFactory.getInstance(DefaultChannelPipeline.class);

        readonly IChannel channel;

        readonly AbstractChannelHandlerContext head; // also used for syncing
        readonly AbstractChannelHandlerContext tail;

        public DefaultChannelPipeline(IChannel channel)
        {
            Contract.Requires(channel != null);
            Contract.Requires(channel.EventLoop is IPausableEventExecutor); // required per current impl of HeadContext.DeregisterAsync

            this.channel = channel;

            this.tail = new TailContext(this);
            this.head = new HeadContext(this);

            this.head.Next = this.tail;
            this.tail.Prev = this.head;
        }

        public IChannel Channel()
        {
            return this.channel;
        }

        IEnumerator<IChannelHandler> IEnumerable<IChannelHandler>.GetEnumerator()
        {
            AbstractChannelHandlerContext current = this.head;
            while (current != null)
            {
                yield return current.Handler;
                current = current.Next;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<IChannelHandler>)this).GetEnumerator();
        }

        public IChannelPipeline AddFirst(IChannelHandler handler)
        {
            Contract.Requires(handler != null);

            lock (this.head)
            {
                var newCtx = new DefaultChannelHandlerContext(this, null, null, handler);
                CheckMultiplicity(newCtx);

                AbstractChannelHandlerContext nextCtx = this.head.Next;
                newCtx.Prev = this.head;
                newCtx.Next = nextCtx;
                this.head.Next = newCtx;
                nextCtx.Prev = newCtx;

                this.CallHandlerAdded(newCtx);
            }
            return this;
        }

        public IChannelPipeline AddLast(IChannelHandler handler)
        {
            Contract.Requires(handler != null);

            var newCtx = new DefaultChannelHandlerContext(this, null, null, handler);
            CheckMultiplicity(newCtx);

            AbstractChannelHandlerContext prev = this.tail.Prev;
            newCtx.Prev = prev;
            newCtx.Next = this.tail;
            prev.Next = newCtx;
            this.tail.Prev = newCtx;

            this.CallHandlerAdded(newCtx);
            return this;
        }

        public IChannelPipeline AddFirst(params IChannelHandler[] handlers)
        {
            Contract.Requires(handlers != null);

            for (int i = handlers.Length - 1; i >= 0; i--)
            {
                IChannelHandler h = handlers[i];
                this.AddFirst(h);
            }

            return this;
        }

        public IChannelPipeline AddLast(params IChannelHandler[] handlers)
        {
            foreach (IChannelHandler h in handlers)
            {
                this.AddLast(h);
            }
            return this;
        }

        public IChannelPipeline Remove(IChannelHandler handler)
        {
            this.Remove(this.GetContextOrThrow(handler));
            return this;
        }

        public T Remove<T>() where T : class, IChannelHandler
        {
            return (T)this.Remove(this.GetContextOrThrow<T>()).Handler;
        }

        AbstractChannelHandlerContext Remove(AbstractChannelHandlerContext ctx)
        {
            Contract.Requires(ctx != this.head && ctx != this.tail);

            lock (this.head)
            {
                AbstractChannelHandlerContext prev = ctx.Prev;
                AbstractChannelHandlerContext next = ctx.Next;
                prev.Next = next;
                next.Prev = prev;
                this.CallHandlerRemoved(ctx);
                return ctx;
            }
        }

        public IChannelHandler RemoveFirst()
        {
            if (this.head.Next == this.tail)
            {
                throw new InvalidOperationException("Pipeline is empty.");
            }
            return this.Remove(this.head.Next).Handler;
        }

        public IChannelHandler RemoveLast()
        {
            if (this.head.Next == this.tail)
            {
                throw new InvalidOperationException("Pipeline is empty.");
            }
            return this.Remove(this.tail.Prev).Handler;
        }

        public IChannelPipeline Replace(IChannelHandler oldHandler, IChannelHandler newHandler)
        {
            this.Replace(this.GetContextOrThrow(oldHandler), newHandler);
            return this;
        }

        public T Replace<T>(
            IChannelHandler newHandler) where T : class, IChannelHandler
        {
            return (T)this.Replace(this.GetContextOrThrow<T>(), newHandler);
        }

        IChannelHandler Replace(
            AbstractChannelHandlerContext ctx, IChannelHandler newHandler)
        {
            Contract.Requires(ctx != this.head && ctx != this.tail);

            lock (this.head)
            {
                var newCtx = new DefaultChannelHandlerContext(this, null, null, newHandler);

                CheckMultiplicity(newCtx);

                AbstractChannelHandlerContext prev = ctx.Prev;
                AbstractChannelHandlerContext next = ctx.Next;
                newCtx.Prev = prev;
                newCtx.Next = next;

                // Finish the replacement of oldCtx with newCtx in the linked list.
                // Note that this doesn't mean events will be sent to the new handler immediately
                // because we are currently at the event handler thread and no more than one handler methods can be invoked
                // at the same time (we ensured that in replace().)
                prev.Next = newCtx;
                next.Prev = newCtx;

                // update the reference to the replacement so forward of buffered content will work correctly
                ctx.Prev = newCtx;
                ctx.Next = newCtx;

                // Invoke newHandler.handlerAdded() first (i.e. before oldHandler.handlerRemoved() is invoked)
                // because callHandlerRemoved() will trigger inboundBufferUpdated() or flush() on newHandler and those
                // event handlers must be called after handlerAdded().
                this.CallHandlerAdded(newCtx);
                this.CallHandlerRemoved(ctx);
                return ctx.Handler;
            }
        }

        static void CheckMultiplicity(IChannelHandlerContext ctx)
        {
            IChannelHandler handler = ctx.Handler;
            var adapter = handler as ChannelHandlerAdapter;
            if (adapter != null)
            {
                ChannelHandlerAdapter h = adapter;
                if (!h.IsSharable && h.Added)
                {
                    throw new ChannelPipelineException(
                        h.GetType().Name + " is not a @Sharable handler, so can't be added or removed multiple times.");
                }
                h.Added = true;
            }
        }

        void CallHandlerAdded(AbstractChannelHandlerContext ctx)
        {
            try
            {
                ctx.Handler.HandlerAdded(ctx);
            }
            catch (Exception ex)
            {
                bool removed = false;
                try
                {
                    this.Remove(ctx);
                    removed = true;
                }
                catch (Exception ex2)
                {
                    if (ChannelEventSource.Log.IsWarningEnabled)
                    {
                        ChannelEventSource.Log.Warning("Failed to remove a handler: " + ctx.Name, ex2);
                    }
                }

                if (removed)
                {
                    this.FireExceptionCaught(new ChannelPipelineException(
                        ctx.Handler.GetType().Name +
                            ".HandlerAdded() has thrown an exception; removed.", ex));
                }
                else
                {
                    this.FireExceptionCaught(new ChannelPipelineException(
                        ctx.Handler.GetType().Name +
                            ".HandlerAdded() has thrown an exception; also failed to remove.", ex));
                }
            }
        }

        void CallHandlerRemoved(AbstractChannelHandlerContext ctx)
        {
            // Notify the complete removal.
            try
            {
                ctx.Handler.HandlerRemoved(ctx);
                ctx.Removed = true;
            }
            catch (Exception ex)
            {
                this.FireExceptionCaught(new ChannelPipelineException(
                    ctx.Handler.GetType().Name + ".handlerRemoved() has thrown an exception.", ex));
            }
        }

        /// <summary>
        /// Waits for a future to finish.  If the task is interrupted, then the current thread will be interrupted.
        /// It is expected that the task performs any appropriate locking.
        /// <p>
        /// If the internal call throws a {@link Throwable}, but it is not an instance of {@link LogError} or
        /// {@link RuntimeException}, then it is wrapped inside a {@link ChannelPipelineException} and that is
        /// thrown instead.</p>
        ///
        /// @param future wait for this future
        /// @see Future#get()
        /// @throws LogError if the task threw this.
        /// @throws RuntimeException if the task threw this.
        /// @throws ChannelPipelineException with a {@link Throwable} as a cause, if the task threw another type of
        ///         {@link Throwable}.
        /// </summary>
        public IChannelHandler First()
        {
            IChannelHandlerContext first = this.FirstContext();
            if (first == null)
            {
                return null;
            }
            return first.Handler;
        }

        public IChannelHandlerContext FirstContext()
        {
            AbstractChannelHandlerContext first = this.head.Next;
            if (first == this.tail)
            {
                return null;
            }
            return this.head.Next;
        }

        public IChannelHandler Last()
        {
            IChannelHandlerContext last = this.LastContext();
            if (last == null)
            {
                return null;
            }
            return last.Handler;
        }

        public IChannelHandlerContext LastContext()
        {
            AbstractChannelHandlerContext last = this.tail.Prev;
            if (last == this.head)
            {
                return null;
            }
            return last;
        }

        public T Get<T>() where T : class, IChannelHandler
        {
            IChannelHandlerContext ctx = this.Context<T>();
            if (ctx == null)
            {
                return null;
            }
            else
            {
                return (T)ctx.Handler;
            }
        }

        public IChannelHandlerContext Context(IChannelHandler handler)
        {
            Contract.Requires(handler != null);

            AbstractChannelHandlerContext ctx = this.head.Next;
            while (true)
            {
                if (ctx == null)
                {
                    return null;
                }

                if (ctx.Handler == handler)
                {
                    return ctx;
                }

                ctx = ctx.Next;
            }
        }

        public IChannelHandlerContext Context<T>() where T : class, IChannelHandler
        {
            AbstractChannelHandlerContext ctx = this.head.Next;
            while (true)
            {
                if (ctx == null)
                {
                    return null;
                }
                if (ctx.Handler is T)
                {
                    return ctx;
                }
                ctx = ctx.Next;
            }
        }

        /// <summary>
        /// Returns the {@link String} representation of this pipeline.
        /// </summary>
        public override string ToString()
        {
            StringBuilder buf = new StringBuilder()
                .Append(this.GetType().Name)
                .Append('{');
            AbstractChannelHandlerContext ctx = this.head.Next;
            while (true)
            {
                if (ctx == this.tail)
                {
                    break;
                }

                buf.Append('(')
                    .Append(ctx.Name)
                    .Append(" = ")
                    .Append(ctx.Handler.GetType().Name)
                    .Append(')');

                ctx = ctx.Next;
                if (ctx == this.tail)
                {
                    break;
                }

                buf.Append(", ");
            }
            buf.Append('}');
            return buf.ToString();
        }

        public IChannelPipeline FireChannelRegistered()
        {
            this.head.FireChannelRegistered();
            return this;
        }

        public IChannelPipeline FireChannelUnregistered()
        {
            this.head.FireChannelUnregistered();

            // Remove all handlers sequentially if channel is closed and unregistered.
            if (!this.channel.Open)
            {
                this.Destroy();
            }
            return this;
        }

        /// <summary>
        /// Removes all handlers from the pipeline one by one from tail (exclusive) to head (exclusive) to trigger
        /// handlerRemoved().
        ///
        /// Note that we traverse up the pipeline ({@link #destroyUp(AbstractChannelHandlerContext)})
        /// before traversing down ({@link #destroyDown(Thread, AbstractChannelHandlerContext)}) so that
        /// the handlers are removed after all events are handled.
        ///
        /// See: https://github.com/netty/netty/issues/3156
        /// </summary>
        void Destroy()
        {
            this.DestroyUp(this.head.Next);
        }

        void DestroyUp(AbstractChannelHandlerContext ctx)
        {
            Thread currentThread = Thread.CurrentThread;
            AbstractChannelHandlerContext tailContext = this.tail;
            while (true)
            {
                if (ctx == tailContext)
                {
                    this.DestroyDown(currentThread, tailContext.Prev);
                    break;
                }

                ctx = ctx.Next;
            }
        }

        void DestroyDown(Thread currentThread, AbstractChannelHandlerContext ctx)
        {
            // We have reached at tail; now traverse backwards.
            AbstractChannelHandlerContext headContext = this.head;
            while (true)
            {
                if (ctx == headContext)
                {
                    break;
                }

                lock (this.head)
                {
                    AbstractChannelHandlerContext prev = ctx.Prev;
                    AbstractChannelHandlerContext next = ctx.Next;
                    prev.Next = next;
                    next.Prev = prev;
                    this.CallHandlerRemoved(ctx);
                }

                ctx = ctx.Prev;
            }
        }

        public IChannelPipeline FireChannelActive()
        {
            this.head.FireChannelActive();

            if (this.channel.Configuration.AutoRead)
            {
                this.channel.Read();
            }

            return this;
        }

        public IChannelPipeline FireChannelInactive()
        {
            this.head.FireChannelInactive();
            return this;
        }

        public IChannelPipeline FireExceptionCaught(Exception cause)
        {
            this.head.FireExceptionCaught(cause);
            return this;
        }

        public IChannelPipeline FireUserEventTriggered(object evt)
        {
            this.head.FireUserEventTriggered(evt);
            return this;
        }

        public IChannelPipeline FireChannelRead(object msg)
        {
            this.head.FireChannelRead(msg);
            return this;
        }

        public IChannelPipeline FireChannelReadComplete()
        {
            this.head.FireChannelReadComplete();
            if (this.channel.Configuration.AutoRead)
            {
                this.Read();
            }
            return this;
        }

        public IChannelPipeline FireChannelWritabilityChanged()
        {
            this.head.FireChannelWritabilityChanged();
            return this;
        }

        public Task BindAsync(EndPoint localAddress)
        {
            return this.tail.BindAsync(localAddress);
        }

        public Task ConnectAsync(EndPoint remoteAddress)
        {
            return this.tail.ConnectAsync(remoteAddress);
        }

        public Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
        {
            return this.tail.ConnectAsync(remoteAddress, localAddress);
        }

        public Task DisconnectAsync()
        {
            return this.tail.DisconnectAsync();
        }

        public Task CloseAsync()
        {
            return this.tail.CloseAsync();
        }

        public Task DeregisterAsync()
        {
            return this.tail.DeregisterAsync();
        }

        public IChannelPipeline Read()
        {
            this.tail.Read();
            return this;
        }

        public Task WriteAsync(object msg)
        {
            return this.tail.WriteAsync(msg);
        }

        public IChannelPipeline Flush()
        {
            this.tail.Flush();
            return this;
        }

        public Task WriteAndFlushAsync(object msg)
        {
            return this.tail.WriteAndFlushAsync(msg);
        }

        AbstractChannelHandlerContext GetContextOrThrow(IChannelHandler handler)
        {
            var ctx = (AbstractChannelHandlerContext)this.Context(handler);
            if (ctx == null)
            {
                throw new ArgumentException(string.Format("Handler of type `{0}` could not be found in the pipeline.", handler.GetType().Name));
            }
            else
            {
                return ctx;
            }
        }

        AbstractChannelHandlerContext GetContextOrThrow<T>() where T : class, IChannelHandler
        {
            var ctx = (AbstractChannelHandlerContext)this.Context<T>();
            if (ctx == null)
            {
                throw new ArgumentException(string.Format("Handler of type `{0}` could not be found in the pipeline.", typeof(T).Name));
            }
            else
            {
                return ctx;
            }
        }

        sealed class TailContext : AbstractChannelHandlerContext, IChannelHandler
        {
            static readonly PropagationDirections SkipPropagationDirections = CalculateSkipPropagationFlags(typeof(TailContext));

            public TailContext(DefaultChannelPipeline pipeline)
                : base(pipeline, null, "<null>", SkipPropagationDirections)
            {
            }

            public override IChannelHandler Handler
            {
                get { return this; }
            }

            public void ChannelRegistered(IChannelHandlerContext context)
            {
            }

            public void ChannelUnregistered(IChannelHandlerContext context)
            {
            }

            public void ChannelActive(IChannelHandlerContext context)
            {
            }

            public void ChannelInactive(IChannelHandlerContext context)
            {
            }

            public void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                try
                {
                    ChannelEventSource.Log.Warning(
                        "An ExceptionCaught() event was fired, and it reached at the tail of the pipeline. " +
                            "It usually means that no handler in the pipeline could handle the exception.",
                        exception);
                }
                finally
                {
                    ReferenceCountUtil.Release(exception);
                }
            }

            [Skip]
            public Task DeregisterAsync(IChannelHandlerContext context)
            {
                return context.DeregisterAsync();
            }

            public void ChannelRead(IChannelHandlerContext context, object message)
            {
                try
                {
                    ChannelEventSource.Log.Verbose(
                        "Discarded inbound message {} that reached at the tail of the pipeline. " +
                            "Please check your pipeline configuration.", message.ToString());
                }
                finally
                {
                    ReferenceCountUtil.Release(message);
                }
            }

            public void ChannelReadComplete(IChannelHandlerContext context)
            {
            }

            public void ChannelWritabilityChanged(IChannelHandlerContext context)
            {
            }

            [Skip]
            public void HandlerAdded(IChannelHandlerContext context)
            {
            }

            [Skip]
            public void HandlerRemoved(IChannelHandlerContext context)
            {
            }

            [Skip]
            public Task DisconnectAsync(IChannelHandlerContext context)
            {
                return context.DisconnectAsync();
            }

            [Skip]
            public Task CloseAsync(IChannelHandlerContext context)
            {
                return context.CloseAsync();
            }

            [Skip]
            public void Read(IChannelHandlerContext context)
            {
                context.Read();
            }

            public void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
                ReferenceCountUtil.Release(evt);
            }

            [Skip]
            public Task WriteAsync(IChannelHandlerContext ctx, object message)
            {
                return ctx.WriteAsync(message);
            }

            [Skip]
            public void Flush(IChannelHandlerContext context)
            {
                context.Flush();
            }

            [Skip]
            public Task BindAsync(IChannelHandlerContext context, EndPoint localAddress)
            {
                return context.BindAsync(localAddress);
            }

            [Skip]
            public Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress)
            {
                return context.ConnectAsync(remoteAddress, localAddress);
            }
        }

        sealed class HeadContext : AbstractChannelHandlerContext, IChannelHandler
        {
            static readonly PropagationDirections SkipPropagationDirections = CalculateSkipPropagationFlags(typeof(HeadContext));

            readonly IChannel channel;

            public HeadContext(DefaultChannelPipeline pipeline)
                : base(pipeline, null, "<null>", SkipPropagationDirections)
            {
                this.channel = pipeline.Channel();
            }

            public override IChannelHandler Handler
            {
                get { return this; }
            }

            public void Flush(IChannelHandlerContext context)
            {
                this.channel.Unsafe.Flush();
            }

            public Task BindAsync(IChannelHandlerContext context, EndPoint localAddress)
            {
                return this.channel.Unsafe.BindAsync(localAddress);
            }

            public Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress)
            {
                return this.channel.Unsafe.ConnectAsync(remoteAddress, localAddress);
            }

            public Task DisconnectAsync(IChannelHandlerContext context)
            {
                return this.channel.Unsafe.DisconnectAsync();
            }

            public Task CloseAsync(IChannelHandlerContext context)
            {
                return this.channel.Unsafe.CloseAsync();
            }

            public Task DeregisterAsync(IChannelHandlerContext context)
            {
                Contract.Assert(!((IPausableEventExecutor)context.Channel.EventLoop).IsAcceptingNewTasks);

                // submit deregistration task
                return context.Channel.EventLoop.Unwrap().SubmitAsync(
                    u => ((IChannelUnsafe)u).DeregisterAsync(), this.channel.Unsafe);
            }

            public void Read(IChannelHandlerContext context)
            {
                this.channel.Unsafe.BeginRead();
            }

            public Task WriteAsync(IChannelHandlerContext context, object message)
            {
                return this.channel.Unsafe.WriteAsync(message);
            }

            [Skip]
            public void ChannelWritabilityChanged(IChannelHandlerContext context)
            {
                context.FireChannelWritabilityChanged();
            }

            [Skip]
            public void HandlerAdded(IChannelHandlerContext context)
            {
            }

            [Skip]
            public void HandlerRemoved(IChannelHandlerContext context)
            {
            }

            [Skip]
            public void ExceptionCaught(IChannelHandlerContext ctx, Exception exception)
            {
                ctx.FireExceptionCaught(exception);
            }

            [Skip]
            public void ChannelRegistered(IChannelHandlerContext context)
            {
                context.FireChannelRegistered();
            }

            [Skip]
            public void ChannelUnregistered(IChannelHandlerContext context)
            {
                context.FireChannelUnregistered();
            }

            [Skip]
            public void ChannelActive(IChannelHandlerContext context)
            {
                context.FireChannelActive();
            }

            [Skip]
            public void ChannelInactive(IChannelHandlerContext context)
            {
                context.FireChannelInactive();
            }

            [Skip]
            public void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                ctx.FireChannelRead(msg);
            }

            [Skip]
            public void ChannelReadComplete(IChannelHandlerContext ctx)
            {
                ctx.FireChannelReadComplete();
            }

            [Skip]
            public void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
            }
        }
    }
}