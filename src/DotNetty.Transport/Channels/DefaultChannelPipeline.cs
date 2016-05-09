// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    sealed class DefaultChannelPipeline : IChannelPipeline
    {
        internal static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<DefaultChannelPipeline>();

        static readonly NameCachesLocal NameCaches = new NameCachesLocal();

        class NameCachesLocal : FastThreadLocal<ConditionalWeakTable<Type, string>>
        {
            protected override ConditionalWeakTable<Type, string> GetInitialValue() => new ConditionalWeakTable<Type, string>();
        }

        readonly AbstractChannel channel;

        readonly AbstractChannelHandlerContext head; // also used for syncing
        readonly AbstractChannelHandlerContext tail;

        readonly bool touch = ResourceLeakDetector.Enabled;

        /// <summary>
        ///     This is the head of a linked list that is processed by <see cref="CallHandlerAddedForAllHandlers" /> and so process
        ///     all the pending <see cref="CallHandlerAdded0" />.
        ///     We only keep the head because it is expected that the list is used infrequently and its size is small.
        ///     Thus full iterations to do insertions is assumed to be a good compromised to saving memory and tail management
        ///     complexity.
        /// </summary>
        PendingHandlerCallback pendingHandlerCallbackHead;

        /// Set to
        /// <c>true</c>
        /// once the
        /// <see cref="AbstractChannel" />
        /// is registered.Once set to
        /// <c>true</c>
        /// the value will never
        /// change.
        bool registered;

        public DefaultChannelPipeline(AbstractChannel channel)
        {
            Contract.Requires(channel != null);

            this.channel = channel;

            this.tail = new TailContext(this);
            this.head = new HeadContext(this);

            this.head.Next = this.tail;
            this.tail.Prev = this.head;
        }

        internal object Touch(object msg, AbstractChannelHandlerContext next) => this.touch ? ReferenceCountUtil.Touch(msg, next) : msg;

        public IChannel Channel => this.channel;

        IEnumerator<IChannelHandler> IEnumerable<IChannelHandler>.GetEnumerator()
        {
            AbstractChannelHandlerContext current = this.head;
            while (current != null)
            {
                yield return current.Handler;
                current = current.Next;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<IChannelHandler>)this).GetEnumerator();

        public IChannelPipeline AddFirst(string name, IChannelHandler handler) => this.AddFirst(null, name, handler);

        public IChannelPipeline AddFirst(IChannelHandlerInvoker invoker, string name, IChannelHandler handler)
        {
            Contract.Requires(handler != null);

            AbstractChannelHandlerContext newCtx;
            IEventExecutor executor;
            bool inEventLoop;
            lock (this)
            {
                CheckMultiplicity(handler);

                newCtx = new DefaultChannelHandlerContext(this, invoker, this.FilterName(name, handler), handler);
                executor = this.ExecutorSafe(invoker);

                // If the executor is null it means that the channel was not registered on an eventloop yet.
                // In this case we add the context to the pipeline and add a task that will call
                // ChannelHandler.handlerAdded(...) once the channel is registered.
                if (executor == null)
                {
                    this.AddFirst0(newCtx);
                    this.CallHandlerCallbackLater(newCtx, true);
                    return this;
                }
                inEventLoop = executor.InEventLoop;
                if (inEventLoop)
                {
                    this.AddFirst0(newCtx);
                }
            }

            if (inEventLoop)
            {
                this.CallHandlerAdded0(newCtx);
            }
            else
            {
                executor.SubmitAsync(() =>
                {
                    lock (this)
                    {
                        this.AddFirst0(newCtx);
                    }
                    this.CallHandlerAdded0(newCtx);
                    return 0;
                }).Wait();
            }
            return this;
        }

        void AddFirst0(AbstractChannelHandlerContext newCtx)
        {
            AbstractChannelHandlerContext nextCtx = this.head.Next;
            newCtx.Prev = this.head;
            newCtx.Next = nextCtx;
            this.head.Next = newCtx;
            nextCtx.Prev = newCtx;
        }

        public IChannelPipeline AddLast(string name, IChannelHandler handler) => this.AddLast(null, name, handler);

        public IChannelPipeline AddLast(IChannelHandlerInvoker invoker, string name, IChannelHandler handler)
        {
            Contract.Requires(handler != null);

            IEventExecutor executor;
            AbstractChannelHandlerContext newCtx;
            bool inEventLoop;
            lock (this)
            {
                CheckMultiplicity(handler);

                newCtx = new DefaultChannelHandlerContext(this, invoker, this.FilterName(name, handler), handler);
                executor = this.ExecutorSafe(invoker);

                // If the executor is null it means that the channel was not registered on an eventloop yet.
                // In this case we add the context to the pipeline and add a task that will call
                // ChannelHandler.handlerAdded(...) once the channel is registered.
                if (executor == null)
                {
                    this.AddLast0(newCtx);
                    this.CallHandlerCallbackLater(newCtx, true);
                    return this;
                }
                inEventLoop = executor.InEventLoop;
                if (inEventLoop)
                {
                    this.AddLast0(newCtx);
                }
            }
            if (inEventLoop)
            {
                this.CallHandlerAdded0(newCtx);
            }
            else
            {
                executor.SubmitAsync(() =>
                {
                    lock (this)
                    {
                        this.AddLast0(newCtx);
                    }
                    this.CallHandlerAdded0(newCtx);
                    return 0;
                });
            }
            return this;
        }

        void AddLast0(AbstractChannelHandlerContext newCtx)
        {
            AbstractChannelHandlerContext prev = this.tail.Prev;
            newCtx.Prev = prev;
            newCtx.Next = this.tail;
            prev.Next = newCtx;
            this.tail.Prev = newCtx;
        }

        public IChannelPipeline AddBefore(string baseName, string name, IChannelHandler handler) => this.AddBefore(null, baseName, name, handler);

        public IChannelPipeline AddBefore(IChannelHandlerInvoker invoker, string baseName, string name, IChannelHandler handler)
        {
            IEventExecutor executor;
            AbstractChannelHandlerContext newCtx;
            AbstractChannelHandlerContext ctx;
            bool inEventLoop;
            lock (this)
            {
                CheckMultiplicity(handler);
                ctx = this.GetContextOrThrow(baseName);

                newCtx = new DefaultChannelHandlerContext(this, invoker, this.FilterName(name, handler), handler);
                executor = this.ExecutorSafe(invoker);

                // If the executor is null it means that the channel was not registered on an eventloop yet.
                // In this case we add the context to the pipeline and add a task that will call
                // ChannelHandler.handlerAdded(...) once the channel is registered.
                if (executor == null)
                {
                    AddBefore0(ctx, newCtx);
                    this.CallHandlerCallbackLater(newCtx, true);
                    return this;
                }

                inEventLoop = executor.InEventLoop;
                if (inEventLoop)
                {
                    AddBefore0(ctx, newCtx);
                }
            }

            if (inEventLoop)
            {
                this.CallHandlerAdded0(newCtx);
            }
            else
            {
                executor.SubmitAsync(() =>
                {
                    lock (this)
                    {
                        AddBefore0(ctx, newCtx);
                    }
                    this.CallHandlerAdded0(newCtx);
                    return 0;
                }).Wait();
            }
            return this;
        }

        static void AddBefore0(AbstractChannelHandlerContext ctx, AbstractChannelHandlerContext newCtx)
        {
            newCtx.Prev = ctx.Prev;
            newCtx.Next = ctx;
            ctx.Prev.Next = newCtx;
            ctx.Prev = newCtx;
        }

        public IChannelPipeline AddAfter(string baseName, string name, IChannelHandler handler) => this.AddAfter(null, baseName, name, handler);

        public IChannelPipeline AddAfter(IChannelHandlerInvoker invoker, string baseName, string name, IChannelHandler handler)
        {
            IEventExecutor executor;
            AbstractChannelHandlerContext newCtx;
            AbstractChannelHandlerContext ctx;
            bool inEventLoop;

            lock (this)
            {
                CheckMultiplicity(handler);
                ctx = this.GetContextOrThrow(baseName);

                newCtx = new DefaultChannelHandlerContext(this, invoker, this.FilterName(name, handler), handler);
                executor = this.ExecutorSafe(invoker);

                // If the executor is null it means that the channel was not registered on an eventloop yet.
                // In this case we remove the context from the pipeline and add a task that will call
                // ChannelHandler.handlerRemoved(...) once the channel is registered.
                if (executor == null)
                {
                    AddAfter0(ctx, newCtx);
                    this.CallHandlerCallbackLater(newCtx, true);
                    return this;
                }
                inEventLoop = executor.InEventLoop;
                if (inEventLoop)
                {
                    AddAfter0(ctx, newCtx);
                }
            }
            if (inEventLoop)
            {
                this.CallHandlerAdded0(newCtx);
            }
            else
            {
                executor.SubmitAsync(() =>
                {
                    lock (this)
                    {
                        AddAfter0(ctx, newCtx);
                    }
                    this.CallHandlerAdded0(newCtx);
                    return 0;
                }).Wait();
            }
            return this;
        }

        static void AddAfter0(AbstractChannelHandlerContext ctx, AbstractChannelHandlerContext newCtx)
        {
            newCtx.Prev = ctx;
            newCtx.Next = ctx.Next;
            ctx.Next.Prev = newCtx;
            ctx.Next = newCtx;
        }

        public IChannelPipeline AddFirst(params IChannelHandler[] handlers) => this.AddFirst(null, handlers);

        public IChannelPipeline AddFirst(IChannelHandlerInvoker invoker, params IChannelHandler[] handlers)
        {
            Contract.Requires(handlers != null);

            for (int i = handlers.Length - 1; i >= 0; i--)
            {
                IChannelHandler h = handlers[i];
                this.AddFirst(invoker, (string)null, h);
            }

            return this;
        }

        public IChannelPipeline AddLast(params IChannelHandler[] handlers) => this.AddLast(null, handlers);

        public IChannelPipeline AddLast(IChannelHandlerInvoker invoker, params IChannelHandler[] handlers)
        {
            foreach (IChannelHandler h in handlers)
            {
                this.AddLast(invoker, (string)null, h);
            }
            return this;
        }

        string GenerateName(IChannelHandler handler)
        {
            ConditionalWeakTable<Type, string> cache = NameCaches.Value;
            Type handlerType = handler.GetType();
            string name = cache.GetValue(handlerType, t => GenerateName0(t));

            // It's not very likely for a user to put more than one handler of the same type, but make sure to avoid
            // any name conflicts.  Note that we don't cache the names generated here.
            if (this.Context0(name) != null)
            {
                string baseName = name.Substring(0, name.Length - 1); // Strip the trailing '0'.
                for (int i = 1;; i++)
                {
                    string newName = baseName + i;
                    if (this.Context0(newName) == null)
                    {
                        name = newName;
                        break;
                    }
                }
            }
            return name;
        }

        static string GenerateName0(Type handlerType) => StringUtil.SimpleClassName(handlerType) + "#0";

        public IChannelPipeline Remove(IChannelHandler handler)
        {
            this.Remove(this.GetContextOrThrow(handler));
            return this;
        }

        public IChannelHandler Remove(string name) => this.Remove(this.GetContextOrThrow(name)).Handler;

        public T Remove<T>() where T : class, IChannelHandler => (T)this.Remove(this.GetContextOrThrow<T>()).Handler;

        AbstractChannelHandlerContext Remove(AbstractChannelHandlerContext ctx)
        {
            Contract.Assert(ctx != this.head && ctx != this.tail);

            IEventExecutor executor;
            bool inEventLoop;
            lock (this)
            {
                executor = this.ExecutorSafe(ctx.invoker);

                // If the executor is null it means that the channel was not registered on an eventloop yet.
                // In this case we remove the context from the pipeline and add a task that will call
                // ChannelHandler.handlerRemoved(...) once the channel is registered.
                if (executor == null)
                {
                    Remove0(ctx);
                    this.CallHandlerCallbackLater(ctx, false);
                    return ctx;
                }
                inEventLoop = executor.InEventLoop;
                if (inEventLoop)
                {
                    Remove0(ctx);
                }
            }
            if (inEventLoop)
            {
                this.CallHandlerRemoved0(ctx);
            }
            else
            {
                executor.SubmitAsync(() =>
                {
                    lock (this)
                    {
                        Remove0(ctx);
                    }
                    this.CallHandlerRemoved0(ctx);
                    return 0;
                }).Wait();
            }
            return ctx;
        }

        static void Remove0(AbstractChannelHandlerContext context)
        {
            AbstractChannelHandlerContext prev = context.Prev;
            AbstractChannelHandlerContext next = context.Next;
            prev.Next = next;
            next.Prev = prev;
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

        public IChannelPipeline Replace(IChannelHandler oldHandler, string newName, IChannelHandler newHandler)
        {
            this.Replace(this.GetContextOrThrow(oldHandler), newName, newHandler);
            return this;
        }

        public IChannelHandler Replace(string oldName, string newName, IChannelHandler newHandler) => this.Replace(this.GetContextOrThrow(oldName), newName, newHandler);

        public T Replace<T>(string newName, IChannelHandler newHandler)
            where T : class, IChannelHandler => (T)this.Replace(this.GetContextOrThrow<T>(), newName, newHandler);

        IChannelHandler Replace(AbstractChannelHandlerContext ctx, string newName, IChannelHandler newHandler)
        {
            Contract.Assert(ctx != this.head && ctx != this.tail);

            AbstractChannelHandlerContext newCtx;
            IEventExecutor executor;
            bool inEventLoop;

            lock (this)
            {
                CheckMultiplicity(newHandler);

                if (newName == null)
                {
                    newName = ctx.Name;
                }
                else if (!ctx.Name.Equals(newName, StringComparison.Ordinal))
                {
                    newName = this.FilterName(newName, newHandler);
                }

                newCtx = new DefaultChannelHandlerContext(this, ctx.invoker, newName, newHandler);
                executor = this.ExecutorSafe(ctx.invoker);

                // If the executor is null it means that the channel was not registered on an eventloop yet.
                // In this case we replace the context in the pipeline
                // and add a task that will call ChannelHandler.handlerAdded(...) and
                // ChannelHandler.handlerRemoved(...) once the channel is registered.
                if (executor == null)
                {
                    Replace0(ctx, newCtx);
                    this.CallHandlerCallbackLater(newCtx, true);
                    this.CallHandlerCallbackLater(ctx, false);
                    return ctx.Handler;
                }
                inEventLoop = executor.InEventLoop;
                if (inEventLoop)
                {
                    Replace0(ctx, newCtx);
                }
            }

            if (inEventLoop)
            {
                // Invoke newHandler.handlerAdded() first (i.e. before oldHandler.handlerRemoved() is invoked)
                // because callHandlerRemoved() will trigger channelRead() or flush() on newHandler and those
                // event handlers must be called after handlerAdded().
                this.CallHandlerAdded0(newCtx);
                this.CallHandlerRemoved0(ctx);
            }
            else
            {
                executor.SubmitAsync(() =>
                {
                    lock (this)
                    {
                        Replace0(ctx, newCtx);
                    }
                    // Invoke newHandler.handlerAdded() first (i.e. before oldHandler.handlerRemoved() is invoked)
                    // because callHandlerRemoved() will trigger channelRead() or flush() on newHandler and
                    // those event handlers must be called after handlerAdded().
                    this.CallHandlerAdded0(newCtx);
                    this.CallHandlerRemoved0(ctx);
                    return 0;
                }).Wait();
            }

            return ctx.Handler;
        }

        static void Replace0(AbstractChannelHandlerContext oldCtx, AbstractChannelHandlerContext newCtx)
        {
            AbstractChannelHandlerContext prev = oldCtx.Prev;
            AbstractChannelHandlerContext next = oldCtx.Next;
            newCtx.Prev = prev;
            newCtx.Next = next;

            // Finish the replacement of oldCtx with newCtx in the linked list.
            // Note that this doesn't mean events will be sent to the new handler immediately
            // because we are currently at the event handler thread and no more than one handler methods can be invoked
            // at the same time (we ensured that in replace().)
            prev.Next = newCtx;
            next.Prev = newCtx;

            // update the reference to the replacement so forward of buffered content will work correctly
            oldCtx.Prev = newCtx;
            oldCtx.Next = newCtx;
        }

        static void CheckMultiplicity(IChannelHandler handler)
        {
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

        void CallHandlerAdded0(AbstractChannelHandlerContext ctx)
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
                    Remove0(ctx);
                    try
                    {
                        ctx.Handler.HandlerRemoved(ctx);
                    }
                    finally
                    {
                        ctx.Removed = true;
                    }
                    removed = true;
                }
                catch (Exception ex2)
                {
                    if (Logger.WarnEnabled)
                    {
                        Logger.Warn($"Failed to remove a handler: {ctx.Name}", ex2);
                    }
                }

                if (removed)
                {
                    this.FireExceptionCaught(new ChannelPipelineException($"{ctx.Handler.GetType().Name}.HandlerAdded() has thrown an exception; removed.", ex));
                }
                else
                {
                    this.FireExceptionCaught(new ChannelPipelineException($"{ctx.Handler.GetType().Name}.HandlerAdded() has thrown an exception; also failed to remove.", ex));
                }
            }
        }

        void CallHandlerRemoved0(AbstractChannelHandlerContext ctx)
        {
            // Notify the complete removal.
            try
            {
                try
                {
                    ctx.Handler.HandlerRemoved(ctx);
                }
                finally
                {
                    ctx.Removed = true;
                }
            }
            catch (Exception ex)
            {
                this.FireExceptionCaught(new ChannelPipelineException($"{ctx.Handler.GetType().Name}.HandlerRemoved() has thrown an exception.", ex));
            }
        }

        /// <summary>
        ///     Waits for a future to finish.  If the task is interrupted, then the current thread will be interrupted.
        ///     It is expected that the task performs any appropriate locking.
        ///     <p>
        ///         If the internal call throws a {@link Throwable}, but it is not an instance of {@link LogError} or
        ///         {@link RuntimeException}, then it is wrapped inside a {@link ChannelPipelineException} and that is
        ///         thrown instead.
        ///     </p>
        ///     @param future wait for this future
        ///     @see Future#get()
        ///     @throws LogError if the task threw this.
        ///     @throws RuntimeException if the task threw this.
        ///     @throws ChannelPipelineException with a {@link Throwable} as a cause, if the task threw another type of
        ///     {@link Throwable}.
        /// </summary>
        public IChannelHandler First() => this.FirstContext()?.Handler;

        public IChannelHandlerContext FirstContext()
        {
            AbstractChannelHandlerContext first = this.head.Next;
            return first == this.tail ? null : first;
        }

        public IChannelHandler Last() => this.LastContext()?.Handler;

        public IChannelHandlerContext LastContext()
        {
            AbstractChannelHandlerContext last = this.tail.Prev;
            return last == this.head ? null : last;
        }

        public IChannelHandler Get(string name) => this.Context(name)?.Handler;

        public T Get<T>() where T : class, IChannelHandler => (T)this.Context<T>()?.Handler;

        public IChannelHandlerContext Context(string name)
        {
            Contract.Requires(name != null);

            return this.Context0(name);
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
        ///     Returns the {@link String} representation of this pipeline.
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
        ///     Removes all handlers from the pipeline one by one from tail (exclusive) to head (exclusive) to trigger
        ///     handlerRemoved().
        ///     Note that we traverse up the pipeline <see cref="DestroyUp" />
        ///     before traversing down <see cref="DestroyDown" /> so that
        ///     the handlers are removed after all events are handled.
        ///     See: https://github.com/netty/netty/issues/3156
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        void Destroy() => this.DestroyUp(this.head.Next, false);

        void DestroyUp(AbstractChannelHandlerContext ctx, bool inEventLoop)
        {
            Thread currentThread = Thread.CurrentThread;
            AbstractChannelHandlerContext tailContext = this.tail;
            while (true)
            {
                if (ctx == tailContext)
                {
                    this.DestroyDown(currentThread, tailContext.Prev, inEventLoop);
                    break;
                }

                IEventExecutor executor = ctx.Executor;
                if (!inEventLoop && !executor.IsInEventLoop(currentThread))
                {
                    executor.Execute((self, c) => ((DefaultChannelPipeline)self).DestroyUp((AbstractChannelHandlerContext)c, true), this, ctx);
                    break;
                }

                ctx = ctx.Next;
                inEventLoop = false;
            }
        }

        void DestroyDown(Thread currentThread, AbstractChannelHandlerContext ctx, bool inEventLoop)
        {
            // We have reached at tail; now traverse backwards.
            AbstractChannelHandlerContext headContext = this.head;
            while (true)
            {
                if (ctx == headContext)
                {
                    break;
                }

                IEventExecutor executor = ctx.Executor;
                if (inEventLoop || executor.IsInEventLoop(currentThread))
                {
                    lock (this)
                    {
                        Remove0(ctx);
                        this.CallHandlerRemoved0(ctx);
                    }
                }
                else
                {
                    executor.Execute((self, c) => ((DefaultChannelPipeline)self).DestroyDown(Thread.CurrentThread, (AbstractChannelHandlerContext)c, true), this, ctx);
                    break;
                }

                ctx = ctx.Prev;
                inEventLoop = false;
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

        public Task BindAsync(EndPoint localAddress) => this.tail.BindAsync(localAddress);

        public Task ConnectAsync(EndPoint remoteAddress) => this.tail.ConnectAsync(remoteAddress);

        public Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress) => this.tail.ConnectAsync(remoteAddress, localAddress);

        public Task DisconnectAsync() => this.tail.DisconnectAsync();

        public Task CloseAsync() => this.tail.CloseAsync();

        public Task DeregisterAsync() => this.tail.DeregisterAsync();

        public IChannelPipeline Read()
        {
            this.tail.Read();
            return this;
        }

        public Task WriteAsync(object msg) => this.tail.WriteAsync(msg);

        public IChannelPipeline Flush()
        {
            this.tail.Flush();
            return this;
        }

        public Task WriteAndFlushAsync(object msg) => this.tail.WriteAndFlushAsync(msg);

        string FilterName(string name, IChannelHandler handler)
        {
            if (name == null)
            {
                return this.GenerateName(handler);
            }

            if (this.Context0(name) == null)
            {
                return name;
            }

            throw new ArgumentException("Duplicate handler name: " + name);
        }

        AbstractChannelHandlerContext Context0(string name)
        {
            AbstractChannelHandlerContext context = this.head.Next;
            while (context != this.tail)
            {
                if (context.Name.Equals(name, StringComparison.Ordinal))
                {
                    return context;
                }
                context = context.Next;
            }
            return null;
        }

        AbstractChannelHandlerContext GetContextOrThrow(string name)
        {
            var ctx = (AbstractChannelHandlerContext)this.Context(name);
            if (ctx == null)
            {
                throw new ArgumentException($"Handler with a name `{name}` could not be found in the pipeline.");
            }

            return ctx;
        }

        AbstractChannelHandlerContext GetContextOrThrow(IChannelHandler handler)
        {
            var ctx = (AbstractChannelHandlerContext)this.Context(handler);
            if (ctx == null)
            {
                throw new ArgumentException($"Handler of type `{handler.GetType().Name}` could not be found in the pipeline.");
            }

            return ctx;
        }

        AbstractChannelHandlerContext GetContextOrThrow<T>() where T : class, IChannelHandler
        {
            var ctx = (AbstractChannelHandlerContext)this.Context<T>();
            if (ctx == null)
            {
                throw new ArgumentException($"Handler of type `{typeof(T).Name}` could not be found in the pipeline.");
            }

            return ctx;
        }

        /// Should be called before
        /// <see cref="FireChannelRegistered" />
        /// is called the first time.
        internal void CallHandlerAddedForAllHandlers()
        {
            // This should only called from within the EventLoop.
            Contract.Assert(this.channel.EventLoop.InEventLoop);

            PendingHandlerCallback pendingHandlerCallbackHead;
            lock (this)
            {
                Contract.Assert(!this.registered);

                // This Channel itself was registered.
                this.registered = true;

                pendingHandlerCallbackHead = this.pendingHandlerCallbackHead;
                // Null out so it can be GC'ed.
                this.pendingHandlerCallbackHead = null;
            }

            // This must happen outside of the synchronized(...) block as otherwise handlerAdded(...) may be called while
            // holding the lock and so produce a deadlock if handlerAdded(...) will try to add another handler from outside
            // the EventLoop.
            PendingHandlerCallback task = pendingHandlerCallbackHead;
            while (task != null)
            {
                task.Execute();
                task = task.Next;
            }
        }

        void CallHandlerCallbackLater(AbstractChannelHandlerContext ctx, bool added)
        {
            Contract.Assert(!this.registered);

            PendingHandlerCallback task = added ? (PendingHandlerCallback)new PendingHandlerAddedTask(this, ctx) : new PendingHandlerRemovedTask(this, ctx);
            PendingHandlerCallback pending = this.pendingHandlerCallbackHead;
            if (pending == null)
            {
                this.pendingHandlerCallbackHead = task;
            }
            else
            {
                // Find the tail of the linked-list.
                while (pending.Next != null)
                {
                    pending = pending.Next;
                }
                pending.Next = task;
            }
        }

        IEventExecutor ExecutorSafe(IChannelHandlerInvoker invoker)
        {
            if (invoker == null)
            {
                // We check for channel().isRegistered and handlerAdded because even if isRegistered() is false we
                // can safely access the invoker() if handlerAdded is true. This is because in this case the Channel
                // was previously registered and so we can still access the old EventLoop to dispatch things.
                return this.channel.Registered || this.registered ? this.channel.EventLoop : null;
            }
            return invoker.Executor;
        }

        sealed class TailContext : AbstractChannelHandlerContext, IChannelHandler
        {
            static readonly string TailName = GenerateName0(typeof(TailContext));
            static readonly int SkipFlags = CalculateSkipPropagationFlags(typeof(TailContext));

            public TailContext(DefaultChannelPipeline pipeline)
                : base(pipeline, null, TailName, SkipFlags)
            {
            }

            public override IChannelHandler Handler => this;

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
                    Logger.Warn(
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
            public Task DeregisterAsync(IChannelHandlerContext context) => context.DeregisterAsync();

            public void ChannelRead(IChannelHandlerContext context, object message)
            {
                try
                {
                    Logger.Debug(
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
            public Task DisconnectAsync(IChannelHandlerContext context) => context.DisconnectAsync();

            [Skip]
            public Task CloseAsync(IChannelHandlerContext context) => context.CloseAsync();

            [Skip]
            public void Read(IChannelHandlerContext context) => context.Read();

            public void UserEventTriggered(IChannelHandlerContext context, object evt) => ReferenceCountUtil.Release(evt);

            [Skip]
            public Task WriteAsync(IChannelHandlerContext ctx, object message) => ctx.WriteAsync(message);

            [Skip]
            public void Flush(IChannelHandlerContext context) => context.Flush();

            [Skip]
            public Task BindAsync(IChannelHandlerContext context, EndPoint localAddress) => context.BindAsync(localAddress);

            [Skip]
            public Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress) => context.ConnectAsync(remoteAddress, localAddress);
        }

        sealed class HeadContext : AbstractChannelHandlerContext, IChannelHandler
        {
            static readonly string HeadName = GenerateName0(typeof(HeadContext));
            static readonly int SkipFlags = CalculateSkipPropagationFlags(typeof(HeadContext));

            readonly IChannelUnsafe channelUnsafe;

            public HeadContext(DefaultChannelPipeline pipeline)
                : base(pipeline, null, HeadName, SkipFlags)
            {
                this.channelUnsafe = pipeline.Channel.Unsafe;
            }

            public override IChannelHandler Handler => this;

            public void Flush(IChannelHandlerContext context) => this.channelUnsafe.Flush();

            public Task BindAsync(IChannelHandlerContext context, EndPoint localAddress) => this.channelUnsafe.BindAsync(localAddress);

            public Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress) => this.channelUnsafe.ConnectAsync(remoteAddress, localAddress);

            public Task DisconnectAsync(IChannelHandlerContext context) => this.channelUnsafe.DisconnectAsync();

            public Task CloseAsync(IChannelHandlerContext context) => this.channelUnsafe.CloseAsync();

            public Task DeregisterAsync(IChannelHandlerContext context) => this.channelUnsafe.DeregisterAsync();

            public void Read(IChannelHandlerContext context) => this.channelUnsafe.BeginRead();

            public Task WriteAsync(IChannelHandlerContext context, object message) => this.channelUnsafe.WriteAsync(message);

            [Skip]
            public void ChannelWritabilityChanged(IChannelHandlerContext context) => context.FireChannelWritabilityChanged();

            [Skip]
            public void HandlerAdded(IChannelHandlerContext context)
            {
            }

            [Skip]
            public void HandlerRemoved(IChannelHandlerContext context)
            {
            }

            [Skip]
            public void ExceptionCaught(IChannelHandlerContext ctx, Exception exception) => ctx.FireExceptionCaught(exception);

            [Skip]
            public void ChannelRegistered(IChannelHandlerContext context) => context.FireChannelRegistered();

            [Skip]
            public void ChannelUnregistered(IChannelHandlerContext context) => context.FireChannelUnregistered();

            [Skip]
            public void ChannelActive(IChannelHandlerContext context) => context.FireChannelActive();

            [Skip]
            public void ChannelInactive(IChannelHandlerContext context) => context.FireChannelInactive();

            [Skip]
            public void ChannelRead(IChannelHandlerContext ctx, object msg) => ctx.FireChannelRead(msg);

            [Skip]
            public void ChannelReadComplete(IChannelHandlerContext ctx) => ctx.FireChannelReadComplete();

            [Skip]
            public void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
            }
        }

        abstract class PendingHandlerCallback : OneTimeTask
        {
            protected readonly DefaultChannelPipeline Pipeline;
            protected readonly AbstractChannelHandlerContext Ctx;
            internal PendingHandlerCallback Next;

            protected PendingHandlerCallback(DefaultChannelPipeline pipeline, AbstractChannelHandlerContext ctx)
            {
                this.Pipeline = pipeline;
                this.Ctx = ctx;
            }

            internal abstract void Execute();
        }

        sealed class PendingHandlerAddedTask : PendingHandlerCallback
        {
            public PendingHandlerAddedTask(DefaultChannelPipeline pipeline, AbstractChannelHandlerContext ctx)
                : base(pipeline, ctx)
            {
            }

            public override void Run() => this.Pipeline.CallHandlerAdded0(this.Ctx);

            internal override void Execute()
            {
                IEventExecutor executor = this.Ctx.Executor;
                if (executor.InEventLoop)
                {
                    this.Pipeline.CallHandlerAdded0(this.Ctx);
                }
                else
                {
                    try
                    {
                        executor.Execute(this);
                    }
                    catch (RejectedExecutionException e)
                    {
                        if (Logger.WarnEnabled)
                        {
                            Logger.Warn(
                                "Can't invoke HandlerAdded() as the IEventExecutor {} rejected it, removing handler {}.",
                                executor, this.Ctx.Name, e);
                        }
                        Remove0(this.Ctx);
                        this.Ctx.Removed = true;
                    }
                }
            }
        }

        sealed class PendingHandlerRemovedTask : PendingHandlerCallback
        {
            public PendingHandlerRemovedTask(DefaultChannelPipeline pipeline, AbstractChannelHandlerContext ctx)
                : base(pipeline, ctx)
            {
            }

            public override void Run() => this.Pipeline.CallHandlerRemoved0(this.Ctx);

            internal override void Execute()
            {
                IEventExecutor executor = this.Ctx.Executor;
                if (executor.InEventLoop)
                {
                    this.Pipeline.CallHandlerRemoved0(this.Ctx);
                }
                else
                {
                    try
                    {
                        executor.Execute(this);
                    }
                    catch (RejectedExecutionException e)
                    {
                        if (Logger.WarnEnabled)
                        {
                            Logger.Warn(
                                "Can't invoke handlerRemoved() as the EventExecutor {} rejected it," +
                                    " removing handler {}.", executor, this.Ctx.Name, e);
                        }
                        // remove0(...) was call before so just call AbstractChannelHandlerContext.setRemoved().
                        this.Ctx.Removed = true;
                    }
                }
            }
        }
    }
}