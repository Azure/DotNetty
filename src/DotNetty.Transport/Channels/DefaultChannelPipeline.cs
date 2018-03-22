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
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using Thread = DotNetty.Common.Concurrency.XThread;

    public class DefaultChannelPipeline : IChannelPipeline
    {
        internal static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<DefaultChannelPipeline>();

        static readonly Action<object, object> CallHandlerAddedAction = (self, ctx) => ((DefaultChannelPipeline)self).CallHandlerAdded0((AbstractChannelHandlerContext)ctx);

        static readonly NameCachesLocal NameCaches = new NameCachesLocal();

        class NameCachesLocal : FastThreadLocal<ConditionalWeakTable<Type, string>>
        {
            protected override ConditionalWeakTable<Type, string> GetInitialValue() => new ConditionalWeakTable<Type, string>();
        }

        readonly IChannel channel;

        readonly AbstractChannelHandlerContext head;
        readonly AbstractChannelHandlerContext tail;

        readonly bool touch = ResourceLeakDetector.Enabled;

        private Dictionary<IEventExecutorGroup, IEventExecutor> childExecutors;
        private IMessageSizeEstimatorHandle estimatorHandle;

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

        public DefaultChannelPipeline(IChannel channel)
        {
            Contract.Requires(channel != null);

            this.channel = channel;

            this.tail = new TailContext(this);
            this.head = new HeadContext(this);

            this.head.Next = this.tail;
            this.tail.Prev = this.head;
        }

        internal IMessageSizeEstimatorHandle EstimatorHandle => this.estimatorHandle ?? (this.estimatorHandle = this.channel.Configuration.MessageSizeEstimator.NewHandle());

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

        AbstractChannelHandlerContext NewContext(IEventExecutorGroup group, string name, IChannelHandler handler) => new DefaultChannelHandlerContext(this, this.GetChildExecutor(group), name, handler);

        AbstractChannelHandlerContext NewContext(IEventExecutor executor, string name, IChannelHandler handler) => new DefaultChannelHandlerContext(this, executor, name, handler);

        IEventExecutor GetChildExecutor(IEventExecutorGroup group)
        {
            if (group == null)
            {
                return null;
            }
            // Use size of 4 as most people only use one extra EventExecutor.
            Dictionary<IEventExecutorGroup, IEventExecutor> executorMap = this.childExecutors 
                ?? (this.childExecutors = new Dictionary<IEventExecutorGroup, IEventExecutor>(4, ReferenceEqualityComparer.Default));

            // Pin one of the child executors once and remember it so that the same child executor
            // is used to fire events for the same channel.
            IEventExecutor childExecutor;
            if (!executorMap.TryGetValue(group, out childExecutor))
            {
                childExecutor = group.GetNext();
                executorMap[group] = childExecutor;
            }
            return childExecutor;
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<IChannelHandler>)this).GetEnumerator();

        public IChannelPipeline AddFirst(string name, IChannelHandler handler) => this.AddFirst(null, name, handler);

        public IChannelPipeline AddFirst(IEventExecutorGroup group, string name, IChannelHandler handler)
        {
            Contract.Requires(handler != null);

            AbstractChannelHandlerContext newCtx;
            lock (this)
            {
                CheckMultiplicity(handler);

                newCtx = this.NewContext(group, this.FilterName(name, handler), handler);
                IEventExecutor executor = this.ExecutorSafe(newCtx.executor);

                this.AddFirst0(newCtx);

                // If the executor is null it means that the channel was not registered on an eventloop yet.
                // In this case we add the context to the pipeline and add a task that will call
                // ChannelHandler.handlerAdded(...) once the channel is registered.
                if (executor == null)
                {
                    this.CallHandlerCallbackLater(newCtx, true);
                    return this;
                }

                if (!executor.InEventLoop)
                {
                    executor.Execute(CallHandlerAddedAction, this, newCtx);
                    return this;
                }
            }

            this.CallHandlerAdded0(newCtx);
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

        public IChannelPipeline AddLast(IEventExecutorGroup group, string name, IChannelHandler handler)
        {
            Contract.Requires(handler != null);

            AbstractChannelHandlerContext newCtx;
            lock (this)
            {
                CheckMultiplicity(handler);

                newCtx = this.NewContext(group, this.FilterName(name, handler), handler);
                IEventExecutor executor = this.ExecutorSafe(newCtx.executor);

                this.AddLast0(newCtx);

                // If the executor is null it means that the channel was not registered on an eventloop yet.
                // In this case we add the context to the pipeline and add a task that will call
                // ChannelHandler.handlerAdded(...) once the channel is registered.
                if (executor == null)
                {
                    this.CallHandlerCallbackLater(newCtx, true);
                    return this;
                }

                if (!executor.InEventLoop)
                {
                    executor.Execute(CallHandlerAddedAction, this, newCtx);
                    return this;
                }
            }
            this.CallHandlerAdded0(newCtx);
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

        public IChannelPipeline AddBefore(IEventExecutorGroup group, string baseName, string name, IChannelHandler handler)
        {
            Contract.Requires(handler != null);

            AbstractChannelHandlerContext newCtx;
            lock (this)
            {
                CheckMultiplicity(handler);
                AbstractChannelHandlerContext ctx = this.GetContextOrThrow(baseName);

                newCtx = this.NewContext(group, this.FilterName(name, handler), handler);
                IEventExecutor executor = this.ExecutorSafe(newCtx.executor);

                AddBefore0(ctx, newCtx);

                // If the executor is null it means that the channel was not registered on an eventloop yet.
                // In this case we add the context to the pipeline and add a task that will call
                // ChannelHandler.handlerAdded(...) once the channel is registered.
                if (executor == null)
                {
                    this.CallHandlerCallbackLater(newCtx, true);
                    return this;
                }

                if (!executor.InEventLoop)
                {
                    executor.Execute(CallHandlerAddedAction, this, newCtx);
                    return this;
                }
            }
            this.CallHandlerAdded0(newCtx);
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

        public IChannelPipeline AddAfter(IEventExecutorGroup group, string baseName, string name, IChannelHandler handler)
        {
            Contract.Requires(handler != null);

            AbstractChannelHandlerContext newCtx;

            lock (this)
            {
                CheckMultiplicity(handler);
                AbstractChannelHandlerContext ctx = this.GetContextOrThrow(baseName);

                newCtx = this.NewContext(group, this.FilterName(name, handler), handler);
                IEventExecutor executor = this.ExecutorSafe(newCtx.executor);

                AddAfter0(ctx, newCtx);

                // If the executor is null it means that the channel was not registered on an eventloop yet.
                // In this case we remove the context from the pipeline and add a task that will call
                // ChannelHandler.handlerRemoved(...) once the channel is registered.
                if (executor == null)
                {
                    this.CallHandlerCallbackLater(newCtx, true);
                    return this;
                }

                if (!executor.InEventLoop)
                {
                    executor.Execute(CallHandlerAddedAction, this, newCtx);
                    return this;
                }
            }
            this.CallHandlerAdded0(newCtx);
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

        public IChannelPipeline AddFirst(IEventExecutorGroup group, params IChannelHandler[] handlers)
        {
            Contract.Requires(handlers != null);

            for (int i = handlers.Length - 1; i >= 0; i--)
            {
                IChannelHandler h = handlers[i];
                this.AddFirst(group, (string)null, h);
            }

            return this;
        }

        public IChannelPipeline AddLast(params IChannelHandler[] handlers) => this.AddLast(null, handlers);

        public IChannelPipeline AddLast(IEventExecutorGroup group, params IChannelHandler[] handlers)
        {
            foreach (IChannelHandler h in handlers)
            {
                this.AddLast(group, (string)null, h);
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

            lock (this)
            {
                IEventExecutor executor = this.ExecutorSafe(ctx.executor);

                Remove0(ctx);

                // If the executor is null it means that the channel was not registered on an eventloop yet.
                // In this case we remove the context from the pipeline and add a task that will call
                // ChannelHandler.handlerRemoved(...) once the channel is registered.
                if (executor == null)
                {
                    this.CallHandlerCallbackLater(ctx, false);
                    return ctx;
                }
                if (!executor.InEventLoop)
                {
                    executor.Execute((s, c) => ((DefaultChannelPipeline)s).CallHandlerRemoved0((AbstractChannelHandlerContext)c), this, ctx);
                    return ctx;
                }
            }
            this.CallHandlerRemoved0(ctx);
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
            Contract.Requires(newHandler != null);
            Contract.Assert(ctx != this.head && ctx != this.tail);

            AbstractChannelHandlerContext newCtx;
            lock (this)
            {
                CheckMultiplicity(newHandler);
                if (newName == null)
                {
                    newName = this.GenerateName(newHandler);
                }
                else
                {
                    bool sameName = ctx.Name.Equals(newName, StringComparison.Ordinal);
                    if (!sameName)
                    {
                        this.CheckDuplicateName(newName);
                    }
                }

                newCtx = this.NewContext(ctx.executor, newName, newHandler);
                IEventExecutor executor = this.ExecutorSafe(ctx.executor);

                Replace0(ctx, newCtx);

                // If the executor is null it means that the channel was not registered on an event loop yet.
                // In this case we replace the context in the pipeline
                // and add a task that will signal handler it was added or removed
                // once the channel is registered.
                if (executor == null)
                {
                    this.CallHandlerCallbackLater(newCtx, true);
                    this.CallHandlerCallbackLater(ctx, false);
                    return ctx.Handler;
                }

                if (!executor.InEventLoop)
                {
                    executor.Execute(() =>
                    {
                        // Indicate new handler was added first (i.e. before old handler removed)
                        // because "removed" will trigger ChannelRead() or Flush() on newHandler and
                        // those event handlers must be called after handler was signaled "added".
                        this.CallHandlerAdded0(newCtx);
                        this.CallHandlerRemoved0(ctx);
                    });
                    return ctx.Handler;
                }
            }
            // Indicate new handler was added first (i.e. before old handler removed)
            // because "removed" will trigger ChannelRead() or Flush() on newHandler and
            // those event handlers must be called after handler was signaled "added".
            this.CallHandlerAdded0(newCtx);
            this.CallHandlerRemoved0(ctx);
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
                ctx.SetAdded();
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
                        ctx.SetRemoved();
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
                    ctx.SetRemoved();
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
        public sealed override string ToString()
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
            AbstractChannelHandlerContext.InvokeChannelRegistered(this.head);
            return this;
        }

        public IChannelPipeline FireChannelUnregistered()
        {
            AbstractChannelHandlerContext.InvokeChannelUnregistered(this.head);
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
        void Destroy()
        {
            lock (this)
            {
                this.DestroyUp(this.head.Next, false);
            }
        }

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
            this.CheckDuplicateName(name);
            return name;
        }

        void CheckDuplicateName(string name)
        {
            if (this.Context0(name) != null)
            {
                throw new ArgumentException("Duplicate handler name: " + name, nameof(name));
            }
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

        void CallHandlerAddedForAllHandlers()
        {
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

        IEventExecutor ExecutorSafe(IEventExecutor eventExecutor) => eventExecutor ?? (this.channel.Registered || this.registered ? this.channel.EventLoop : null);

        /// <summary>
        ///     Called once a <see cref="Exception" /> hit the end of the <see cref="IChannelPipeline" /> without been handled by
        ///     the user in <see cref="IChannelHandler.ExceptionCaught(IChannelHandlerContext, Exception)" />.
        /// </summary>
        protected virtual void OnUnhandledInboundException(Exception cause)
        {
            try
            {
                Logger.Warn("An ExceptionCaught() event was fired, and it reached at the tail of the pipeline. " +
                    "It usually means the last handler in the pipeline did not handle the exception.",
                    cause);
            }
            finally
            {
                ReferenceCountUtil.Release(cause);
            }
        }

        /// <summary>
        ///     Called once a message hit the end of the <see cref="IChannelPipeline" /> without been handled by the user
        ///     in <see cref="IChannelHandler.ChannelRead(IChannelHandlerContext, object)" />. This method is responsible
        ///     to call <see cref="ReferenceCountUtil.Release(object)" /> on the given msg at some point.
        /// </summary>
        protected virtual void OnUnhandledInboundMessage(object msg)
        {
            try
            {
                Logger.Debug("Discarded inbound message {} that reached at the tail of the pipeline. " +
                    "Please check your pipeline configuration.",
                    msg);
            }
            finally
            {
                ReferenceCountUtil.Release(msg);
            }
        }

        sealed class TailContext : AbstractChannelHandlerContext, IChannelHandler
        {
            static readonly string TailName = GenerateName0(typeof(TailContext));
            static readonly SkipFlags SkipFlags = CalculateSkipPropagationFlags(typeof(TailContext));

            public TailContext(DefaultChannelPipeline pipeline)
                : base(pipeline, null, TailName, SkipFlags)
            {
                this.SetAdded();
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

            public void ExceptionCaught(IChannelHandlerContext context, Exception exception) => this.pipeline.OnUnhandledInboundException(exception);

            public void ChannelRead(IChannelHandlerContext context, object message) => this.pipeline.OnUnhandledInboundMessage(message);

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
            public Task DeregisterAsync(IChannelHandlerContext context) => context.DeregisterAsync();

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
            static readonly SkipFlags SkipFlags = CalculateSkipPropagationFlags(typeof(HeadContext));

            readonly IChannelUnsafe channelUnsafe;
            bool firstRegistration = true;

            public HeadContext(DefaultChannelPipeline pipeline)
                : base(pipeline, null, HeadName, SkipFlags)
            {
                this.channelUnsafe = pipeline.Channel.Unsafe;
                this.SetAdded();
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
            public void HandlerAdded(IChannelHandlerContext context)
            {
            }

            [Skip]
            public void HandlerRemoved(IChannelHandlerContext context)
            {
            }

            [Skip]
            public void ExceptionCaught(IChannelHandlerContext ctx, Exception exception) => ctx.FireExceptionCaught(exception);

            public void ChannelRegistered(IChannelHandlerContext context)
            {
                if (this.firstRegistration)
                {
                    this.firstRegistration = false;
                    // We are now registered to the EventLoop. It's time to call the callbacks for the ChannelHandlers,
                    // that were added before the registration was done.
                    this.pipeline.CallHandlerAddedForAllHandlers();
                }

                context.FireChannelRegistered();
            }

            public void ChannelUnregistered(IChannelHandlerContext context)
            {
                context.FireChannelUnregistered();

                // Remove all handlers sequentially if channel is closed and unregistered.
                if (!this.pipeline.channel.Open)
                {
                    this.pipeline.Destroy();
                }
            }

            public void ChannelActive(IChannelHandlerContext context)
            {
                context.FireChannelActive();

                this.ReadIfIsAutoRead();
            }

            [Skip]
            public void ChannelInactive(IChannelHandlerContext context) => context.FireChannelInactive();

            [Skip]
            public void ChannelRead(IChannelHandlerContext ctx, object msg) => ctx.FireChannelRead(msg);

            public void ChannelReadComplete(IChannelHandlerContext ctx)
            {
                ctx.FireChannelReadComplete();

                this.ReadIfIsAutoRead();
            }

            void ReadIfIsAutoRead()
            {
                if (this.pipeline.channel.Configuration.AutoRead)
                {
                    this.pipeline.channel.Read();
                }
            }

            [Skip]
            public void UserEventTriggered(IChannelHandlerContext context, object evt) => this.FireUserEventTriggered(evt);

            [Skip]
            public void ChannelWritabilityChanged(IChannelHandlerContext context) => context.FireChannelWritabilityChanged();
        }

        abstract class PendingHandlerCallback : IRunnable
        {
            protected readonly DefaultChannelPipeline Pipeline;
            protected readonly AbstractChannelHandlerContext Ctx;
            internal PendingHandlerCallback Next;

            protected PendingHandlerCallback(DefaultChannelPipeline pipeline, AbstractChannelHandlerContext ctx)
            {
                this.Pipeline = pipeline;
                this.Ctx = ctx;
            }

            public abstract void Run();

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
                        this.Ctx.SetRemoved();
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
                                "Can't invoke HandlerRemoved() as the IEventExecutor {} rejected it," +
                                    " removing handler {}.", executor, this.Ctx.Name, e);
                        }
                        // remove0(...) was call before so just call AbstractChannelHandlerContext.setRemoved().
                        this.Ctx.SetRemoved();
                    }
                }
            }
        }
    }
}