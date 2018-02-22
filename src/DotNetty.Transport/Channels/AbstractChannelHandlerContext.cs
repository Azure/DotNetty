// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;

    abstract class AbstractChannelHandlerContext : IChannelHandlerContext, IResourceLeakHint
    {
        static readonly Action<object> InvokeChannelReadCompleteAction = ctx => ((AbstractChannelHandlerContext)ctx).InvokeChannelReadComplete();
        static readonly Action<object> InvokeReadAction = ctx => ((AbstractChannelHandlerContext)ctx).InvokeRead();
        static readonly Action<object> InvokeChannelWritabilityChangedAction = ctx => ((AbstractChannelHandlerContext)ctx).InvokeChannelWritabilityChanged();
        static readonly Action<object> InvokeFlushAction = ctx => ((AbstractChannelHandlerContext)ctx).InvokeFlush();
        static readonly Action<object, object> InvokeUserEventTriggeredAction = (ctx, evt) => ((AbstractChannelHandlerContext)ctx).InvokeUserEventTriggered(evt);
        static readonly Action<object, object> InvokeChannelReadAction = (ctx, msg) => ((AbstractChannelHandlerContext)ctx).InvokeChannelRead(msg);

        [Flags]
        protected internal enum SkipFlags
        {
            HandlerAdded = 1,
            HandlerRemoved = 1 << 1,
            ExceptionCaught = 1 << 2,
            ChannelRegistered = 1 << 3,
            ChannelUnregistered = 1 << 4,
            ChannelActive = 1 << 5,
            ChannelInactive = 1 << 6,
            ChannelRead = 1 << 7,
            ChannelReadComplete = 1 << 8,
            ChannelWritabilityChanged = 1 << 9,
            UserEventTriggered = 1 << 10,
            Bind = 1 << 11,
            Connect = 1 << 12,
            Disconnect = 1 << 13,
            Close = 1 << 14,
            Deregister = 1 << 15,
            Read = 1 << 16,
            Write = 1 << 17,
            Flush = 1 << 18,

            Inbound = ExceptionCaught |
                ChannelRegistered |
                ChannelUnregistered |
                ChannelActive |
                ChannelInactive |
                ChannelRead |
                ChannelReadComplete |
                ChannelWritabilityChanged |
                UserEventTriggered,

            Outbound = Bind |
                Connect |
                Disconnect |
                Close |
                Deregister |
                Read |
                Write |
                Flush,
        }

        static readonly ConditionalWeakTable<Type, Tuple<SkipFlags>> SkipTable = new ConditionalWeakTable<Type, Tuple<SkipFlags>>();

        protected static SkipFlags GetSkipPropagationFlags(IChannelHandler handler)
        {
            Tuple<SkipFlags> skipDirection = SkipTable.GetValue(
                handler.GetType(),
                handlerType => Tuple.Create(CalculateSkipPropagationFlags(handlerType)));

            return skipDirection?.Item1 ?? 0;
        }

        protected static SkipFlags CalculateSkipPropagationFlags(Type handlerType)
        {
            SkipFlags flags = 0;

            // this method should never throw
            if (IsSkippable(handlerType, nameof(IChannelHandler.HandlerAdded)))
            {
                flags |= SkipFlags.HandlerAdded;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.HandlerRemoved)))
            {
                flags |= SkipFlags.HandlerRemoved;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ExceptionCaught), typeof(Exception)))
            {
                flags |= SkipFlags.ExceptionCaught;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelRegistered)))
            {
                flags |= SkipFlags.ChannelRegistered;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelUnregistered)))
            {
                flags |= SkipFlags.ChannelUnregistered;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelActive)))
            {
                flags |= SkipFlags.ChannelActive;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelInactive)))
            {
                flags |= SkipFlags.ChannelInactive;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelRead), typeof(object)))
            {
                flags |= SkipFlags.ChannelRead;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelReadComplete)))
            {
                flags |= SkipFlags.ChannelReadComplete;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelWritabilityChanged)))
            {
                flags |= SkipFlags.ChannelWritabilityChanged;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.UserEventTriggered), typeof(object)))
            {
                flags |= SkipFlags.UserEventTriggered;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.BindAsync), typeof(EndPoint)))
            {
                flags |= SkipFlags.Bind;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ConnectAsync), typeof(EndPoint), typeof(EndPoint)))
            {
                flags |= SkipFlags.Connect;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.DisconnectAsync)))
            {
                flags |= SkipFlags.Disconnect;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.CloseAsync)))
            {
                flags |= SkipFlags.Close;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.DeregisterAsync)))
            {
                flags |= SkipFlags.Deregister;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.Read)))
            {
                flags |= SkipFlags.Read;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.WriteAsync), typeof(object)))
            {
                flags |= SkipFlags.Write;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.Flush)))
            {
                flags |= SkipFlags.Flush;
            }
            return flags;
        }

        protected static bool IsSkippable(Type handlerType, string methodName) => IsSkippable(handlerType, methodName, Type.EmptyTypes);

        protected static bool IsSkippable(Type handlerType, string methodName, params Type[] paramTypes)
        {
            var newParamTypes = new Type[paramTypes.Length + 1];
            newParamTypes[0] = typeof(IChannelHandlerContext);
            Array.Copy(paramTypes, 0, newParamTypes, 1, paramTypes.Length);
            return handlerType.GetMethod(methodName, newParamTypes).GetCustomAttribute<SkipAttribute>(false) != null;
        }

        internal volatile AbstractChannelHandlerContext Next;
        internal volatile AbstractChannelHandlerContext Prev;

        internal readonly SkipFlags SkipPropagationFlags;

        enum HandlerState
        {
            /// <summary>Neither <see cref="IChannelHandler.HandlerAdded"/> nor <see cref="IChannelHandler.HandlerRemoved"/> was called.</summary>
            Init = 0,
            /// <summary><see cref="IChannelHandler.HandlerAdded"/> was called.</summary>
            Added = 1,
            /// <summary><see cref="IChannelHandler.HandlerRemoved"/> was called.</summary>
            Removed = 2
        }

        internal readonly DefaultChannelPipeline pipeline;

        // Will be set to null if no child executor should be used, otherwise it will be set to the
        // child executor.
        internal readonly IEventExecutor executor;
        HandlerState handlerState = HandlerState.Init;

        protected AbstractChannelHandlerContext(DefaultChannelPipeline pipeline, IEventExecutor executor,
            string name, SkipFlags skipPropagationDirections)
        {
            Contract.Requires(pipeline != null);
            Contract.Requires(name != null);

            this.pipeline = pipeline;
            this.Name = name;
            this.executor = executor;
            this.SkipPropagationFlags = skipPropagationDirections;
        }

        public IChannel Channel => this.pipeline.Channel;

        public IByteBufferAllocator Allocator => this.Channel.Allocator;

        public abstract IChannelHandler Handler { get; }

        /// <summary>
        ///     Makes best possible effort to detect if <see cref="IChannelHandler.HandlerAdded(IChannelHandlerContext)" /> was
        ///     called
        ///     yet. If not return <c>false</c> and if called or could not detect return <c>true</c>.
        ///     If this method returns <c>true</c> we will not invoke the <see cref="IChannelHandler" /> but just forward the
        ///     event.
        ///     This is needed as <see cref="DefaultChannelPipeline" /> may already put the <see cref="IChannelHandler" /> in the
        ///     linked-list
        ///     but not called
        /// </summary>
        public bool Added => handlerState == HandlerState.Added;

        public bool Removed => handlerState == HandlerState.Removed;

        internal void SetAdded() => handlerState = HandlerState.Added;

        internal void SetRemoved() => handlerState = HandlerState.Removed;

        public IEventExecutor Executor => this.executor ?? this.Channel.EventLoop;

        public string Name { get; }

        public IAttribute<T> GetAttribute<T>(AttributeKey<T> key)
            where T : class
        {
            return this.Channel.GetAttribute(key);
        }

        public bool HasAttribute<T>(AttributeKey<T> key)
            where T : class
        {
            return this.Channel.HasAttribute(key);
        }
        public IChannelHandlerContext FireChannelRegistered()
        {
            InvokeChannelRegistered(this.FindContextInbound());
            return this;
        }

        internal static void InvokeChannelRegistered(AbstractChannelHandlerContext next)
        {
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeChannelRegistered();
            }
            else
            {
                nextExecutor.Execute(c => ((AbstractChannelHandlerContext)c).InvokeChannelRegistered(), next);
            }
        }

        void InvokeChannelRegistered()
        {
            if (this.Added)
            {
                try
                {
                    this.Handler.ChannelRegistered(this);
                }
                catch (Exception ex)
                {
                    this.NotifyHandlerException(ex);
                }
            }
            else
            {
                this.FireChannelRegistered();
            }
        }

        public IChannelHandlerContext FireChannelUnregistered()
        {
            InvokeChannelUnregistered(this.FindContextInbound());
            return this;
        }

        internal static void InvokeChannelUnregistered(AbstractChannelHandlerContext next)
        {
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeChannelUnregistered();
            }
            else
            {
                nextExecutor.Execute(c => ((AbstractChannelHandlerContext)c).InvokeChannelUnregistered(), next);
            }
        }

        void InvokeChannelUnregistered()
        {
            if (this.Added)
            {
                try
                {
                    this.Handler.ChannelUnregistered(this);
                }
                catch (Exception t)
                {
                    this.NotifyHandlerException(t);
                }
            }
            else
            {
                this.FireChannelUnregistered();
            }
        }

        public IChannelHandlerContext FireChannelActive()
        {
            InvokeChannelActive(this.FindContextInbound());
            return this;
        }

        internal static void InvokeChannelActive(AbstractChannelHandlerContext next)
        {
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeChannelActive();
            }
            else
            {
                nextExecutor.Execute(c => ((AbstractChannelHandlerContext)c).InvokeChannelActive(), next);
            }
        }

        void InvokeChannelActive()
        {
            if (this.Added)
            {
                try
                {
                    (this.Handler).ChannelActive(this);
                }
                catch (Exception ex)
                {
                    this.NotifyHandlerException(ex);
                }
            }
            else
            {
                this.FireChannelActive();
            }
        }

        public IChannelHandlerContext FireChannelInactive()
        {
            InvokeChannelInactive(this.FindContextInbound());
            return this;
        }

        internal static void InvokeChannelInactive(AbstractChannelHandlerContext next)
        {
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeChannelInactive();
            }
            else
            {
                nextExecutor.Execute(c => ((AbstractChannelHandlerContext)c).InvokeChannelInactive(), next);
            }
        }

        void InvokeChannelInactive()
        {
            if (this.Added)
            {
                try
                {
                    this.Handler.ChannelInactive(this);
                }
                catch (Exception ex)
                {
                    this.NotifyHandlerException(ex);
                }
            }
            else
            {
                this.FireChannelInactive();
            }
        }

        public virtual IChannelHandlerContext FireExceptionCaught(Exception cause)
        {
            InvokeExceptionCaught(this.FindContextInbound(), cause);
            return this;
        }

        internal static void InvokeExceptionCaught(AbstractChannelHandlerContext next, Exception cause)
        {
            Contract.Requires(cause != null);

            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeExceptionCaught(cause);
            }
            else
            {
                try
                {
                    nextExecutor.Execute((c, e) => ((AbstractChannelHandlerContext)c).InvokeExceptionCaught((Exception)e), next, cause);
                }
                catch (Exception t)
                {
                    if (DefaultChannelPipeline.Logger.WarnEnabled)
                    {
                        DefaultChannelPipeline.Logger.Warn("Failed to submit an ExceptionCaught() event.", t);
                        DefaultChannelPipeline.Logger.Warn("The ExceptionCaught() event that was failed to submit was:", cause);
                    }
                }
            }
        }

        void InvokeExceptionCaught(Exception cause)
        {
            if (this.Added)
            {
                try
                {
                    this.Handler.ExceptionCaught(this, cause);
                }
                catch (Exception t)
                {
                    if (DefaultChannelPipeline.Logger.WarnEnabled)
                    {
                        DefaultChannelPipeline.Logger.Warn("Failed to submit an ExceptionCaught() event.", t);
                        DefaultChannelPipeline.Logger.Warn(
                                "An exception was thrown by a user handler's " +
                                        "ExceptionCaught() method while handling the following exception:", cause);
                    }
                }
            }
            else
            {
                this.FireExceptionCaught(cause);
            }
        }

        public IChannelHandlerContext FireUserEventTriggered(object evt)
        {
            InvokeUserEventTriggered(this.FindContextInbound(), evt);
            return this;
        }

        internal static void InvokeUserEventTriggered(AbstractChannelHandlerContext next, object evt)
        {
            Contract.Requires(evt != null);
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeUserEventTriggered(evt);
            }
            else
            {
                nextExecutor.Execute(InvokeUserEventTriggeredAction, next, evt);
            }
        }

        void InvokeUserEventTriggered(object evt)
        {
            if (this.Added)
            {
                try
                {
                    this.Handler.UserEventTriggered(this, evt);
                }
                catch (Exception ex)
                {
                    this.NotifyHandlerException(ex);
                }
            }
            else
            {
                this.FireUserEventTriggered(evt);
            }
        }

        public IChannelHandlerContext FireChannelRead(object msg)
        {
            InvokeChannelRead(this.FindContextInbound(), msg);
            return this;
        }

        internal static void InvokeChannelRead(AbstractChannelHandlerContext next, object msg)
        {
            Contract.Requires(msg != null);

            object m = next.pipeline.Touch(msg, next);
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeChannelRead(m);
            }
            else
            {
                nextExecutor.Execute(InvokeChannelReadAction, next, msg);
            }
        }

        void InvokeChannelRead(object msg)
        {
            if (this.Added)
            {
                try
                {
                    this.Handler.ChannelRead(this, msg);
                }
                catch (Exception ex)
                {
                    this.NotifyHandlerException(ex);
                }
            }
            else
            {
                this.FireChannelRead(msg);
            }
        }

        public IChannelHandlerContext FireChannelReadComplete()
        {
            InvokeChannelReadComplete(this.FindContextInbound());
            return this;
        }

        internal static void InvokeChannelReadComplete(AbstractChannelHandlerContext next) {
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeChannelReadComplete();
            }
            else
            {
                // todo: consider caching task
                nextExecutor.Execute(InvokeChannelReadCompleteAction, next);
            }
        }

        void InvokeChannelReadComplete()
        {
            if (this.Added)
            {
                try
                {
                    (this.Handler).ChannelReadComplete(this);
                }
                catch (Exception ex)
                {
                    this.NotifyHandlerException(ex);
                }
            }
            else
            {
                this.FireChannelReadComplete();
            }
        }

        public IChannelHandlerContext FireChannelWritabilityChanged()
        {
            InvokeChannelWritabilityChanged(this.FindContextInbound());
            return this;
        }

        internal static void InvokeChannelWritabilityChanged(AbstractChannelHandlerContext next)
        {
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeChannelWritabilityChanged();
            }
            else
            {
                // todo: consider caching task
                nextExecutor.Execute(InvokeChannelWritabilityChangedAction, next);
            }
        }

        void InvokeChannelWritabilityChanged()
        {
            if (this.Added)
            {
                try
                {
                    this.Handler.ChannelWritabilityChanged(this);
                }
                catch (Exception ex)
                {
                    this.NotifyHandlerException(ex);
                }
            }
            else
            {
                this.FireChannelWritabilityChanged();
            }
        }

        public Task BindAsync(EndPoint localAddress)
        {
            Contract.Requires(localAddress != null);
            // todo: check for cancellation
            //if (!validatePromise(ctx, promise, false)) {
            //    // promise cancelled
            //    return;
            //}

            AbstractChannelHandlerContext next = this.FindContextOutbound();
            IEventExecutor nextExecutor = next.Executor;
            return nextExecutor.InEventLoop 
                ? next.InvokeBindAsync(localAddress) 
                : SafeExecuteOutboundAsync(nextExecutor, () => next.InvokeBindAsync(localAddress));
        }

        Task InvokeBindAsync(EndPoint localAddress)
        {
            if (this.Added)
            {
                try
                {
                    return this.Handler.BindAsync(this, localAddress);
                }
                catch (Exception ex)
                {
                    return ComposeExceptionTask(ex);
                }
            }

            return this.BindAsync(localAddress);
        }

        public Task ConnectAsync(EndPoint remoteAddress) => this.ConnectAsync(remoteAddress, null);

        public Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
        {
            AbstractChannelHandlerContext next = this.FindContextOutbound();
            Contract.Requires(remoteAddress != null);
            // todo: check for cancellation

            IEventExecutor nextExecutor = next.Executor;
            return nextExecutor.InEventLoop
                ? next.InvokeConnectAsync(remoteAddress, localAddress)
                : SafeExecuteOutboundAsync(nextExecutor, () => next.InvokeConnectAsync(remoteAddress, localAddress));
        }

        Task InvokeConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
        {
            if (this.Added)
            {
                try
                {
                    return this.Handler.ConnectAsync(this, remoteAddress, localAddress);
                }
                catch (Exception ex)
                {
                    return ComposeExceptionTask(ex);
                }
            }

            return this.ConnectAsync(remoteAddress, localAddress);
        }

        public Task DisconnectAsync()
        {
            if (!this.Channel.Metadata.HasDisconnect)
            {
                return this.CloseAsync();
            }

            // todo: check for cancellation
            AbstractChannelHandlerContext next = this.FindContextOutbound();
            IEventExecutor nextExecutor = next.Executor;
            return nextExecutor.InEventLoop
                ? next.InvokeDisconnectAsync()
                : SafeExecuteOutboundAsync(nextExecutor, () => next.InvokeDisconnectAsync());
        }

        Task InvokeDisconnectAsync()
        {
            if (this.Added)
            {
                try
                {
                    return this.Handler.DisconnectAsync(this);
                }
                catch (Exception ex)
                {
                    return ComposeExceptionTask(ex);
                }
            }
            return this.DisconnectAsync();
        }

        public Task CloseAsync()
        {
            // todo: check for cancellation
            AbstractChannelHandlerContext next = this.FindContextOutbound();
            IEventExecutor nextExecutor = next.Executor;
            return nextExecutor.InEventLoop
                ? next.InvokeCloseAsync()
                : SafeExecuteOutboundAsync(nextExecutor, () => next.InvokeCloseAsync());
        }

        Task InvokeCloseAsync()
        {
            if (this.Added)
            {
                try
                {
                    return this.Handler.CloseAsync(this);
                }
                catch (Exception ex)
                {
                    return ComposeExceptionTask(ex);
                }
            }
            return this.CloseAsync();
        }

        public Task DeregisterAsync()
        {
            // todo: check for cancellation
            AbstractChannelHandlerContext next = this.FindContextOutbound();
            IEventExecutor nextExecutor = next.Executor;
            return nextExecutor.InEventLoop
                ? next.InvokeDeregisterAsync()
                : SafeExecuteOutboundAsync(nextExecutor, () => next.InvokeDeregisterAsync());
        }

        Task InvokeDeregisterAsync()
        {
            if (this.Added)
            {
                try
                {
                    return this.Handler.DeregisterAsync(this);
                }
                catch (Exception ex)
                {
                    return ComposeExceptionTask(ex);
                }
            }
            return this.DeregisterAsync();
        }

        public IChannelHandlerContext Read()
        {
            AbstractChannelHandlerContext next = this.FindContextOutbound();
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeRead();
            }
            else
            {
                // todo: consider caching task
                nextExecutor.Execute(InvokeReadAction, next);
            }
            return this;
        }

        void InvokeRead()
        {
            if (this.Added)
            {
                try
                {
                    this.Handler.Read(this);
                }
                catch (Exception ex)
                {
                    this.NotifyHandlerException(ex);
                }
            }
            else
            {
                this.Read();
            }
        }

        public ChannelFuture WriteAsync(object msg)
        {
            Contract.Requires(msg != null);
            // todo: check for cancellation
            return this.WriteAsync(msg, false);
        }

        ChannelFuture InvokeWriteAsync(object msg) => this.Added ? this.InvokeWriteAsync0(msg) : this.WriteAsync(msg);

        ChannelFuture InvokeWriteAsync0(object msg)
        {
            try
            {
                return this.Handler.WriteAsync(this, msg);
            }
            catch (Exception ex)
            {
                return ChannelFuture.FromException(ex);
            }
        }

        public IChannelHandlerContext Flush()
        {
            AbstractChannelHandlerContext next = this.FindContextOutbound();
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                next.InvokeFlush();
            }
            else
            {
                nextExecutor.Execute(InvokeFlushAction, next);
            }
            return this;
        }

        void InvokeFlush()
        {
            if (this.Added)
            {
                this.InvokeFlush0();
            }
            else
            {
                this.Flush();
            }
        }

        void InvokeFlush0()
        {
            try
            {
                this.Handler.Flush(this);
            }
            catch (Exception ex)
            {
                this.NotifyHandlerException(ex);
            }
        }

        public ChannelFuture WriteAndFlushAsync(object message)
        {
            Contract.Requires(message != null);
            // todo: check for cancellation

            return this.WriteAsync(message, true);
        }

        ChannelFuture InvokeWriteAndFlushAsync(object msg)
        {
            if (this.Added)
            {
                ChannelFuture task = this.InvokeWriteAsync0(msg);
                this.InvokeFlush0();
                return task;
            }
            return this.WriteAndFlushAsync(msg);
        }

        ChannelFuture WriteAsync(object msg, bool flush)
        {
            AbstractChannelHandlerContext next = this.FindContextOutbound();
            object m = this.pipeline.Touch(msg, next);
            IEventExecutor nextExecutor = next.Executor;
            if (nextExecutor.InEventLoop)
            {
                return flush
                    ? next.InvokeWriteAndFlushAsync(m)
                    : next.InvokeWriteAsync(m);
            }
            else
            {
                AbstractWriteTask task = flush 
                    ? WriteAndFlushTask.NewInstance(next, m)
                    : (AbstractWriteTask)WriteTask.NewInstance(next, m);
                SafeExecuteOutbound(nextExecutor, task, msg);
                return task;
            }
        }

        void NotifyHandlerException(Exception cause)
        {
            if (InExceptionCaught(cause))
            {
                if (DefaultChannelPipeline.Logger.WarnEnabled)
                {
                    DefaultChannelPipeline.Logger.Warn(
                        "An exception was thrown by a user handler " +
                            "while handling an exceptionCaught event", cause);
                }
                return;
            }

            this.InvokeExceptionCaught(cause);
        }

        static Task ComposeExceptionTask(Exception cause) => TaskEx.FromException(cause);

        const string ExceptionCaughtMethodName = nameof(IChannelHandler.ExceptionCaught);

        static bool InExceptionCaught(Exception cause) => cause.StackTrace.IndexOf("." + ExceptionCaughtMethodName + "(", StringComparison.Ordinal) >= 0;

        AbstractChannelHandlerContext FindContextInbound()
        {
            AbstractChannelHandlerContext ctx = this;
            do
            {
                ctx = ctx.Next;
            }
            while ((ctx.SkipPropagationFlags & SkipFlags.Inbound) == SkipFlags.Inbound);
            return ctx;
        }

        AbstractChannelHandlerContext FindContextOutbound()
        {
            AbstractChannelHandlerContext ctx = this;
            do
            {
                ctx = ctx.Prev;
            }
            while ((ctx.SkipPropagationFlags & SkipFlags.Outbound) == SkipFlags.Outbound);
            return ctx;
        }

        static Task SafeExecuteOutboundAsync(IEventExecutor executor, Func<Task> function)
        {
            var promise = new TaskCompletionSource();
            try
            {
                executor.Execute((p, func) => ((Func<Task>)func)().LinkOutcome((TaskCompletionSource)p), promise, function);
            }
            catch (Exception cause)
            {
                promise.TrySetException(cause);
            }
            return promise.Task;
        }

        static void SafeExecuteOutbound(IEventExecutor executor, AbstractWriteTask task, object msg)
        {
            try
            {
                executor.Execute(task);
            }
            catch (Exception cause)
            {
                try
                {
                    task.TryComplete(cause);
                }
                finally
                {
                    ReferenceCountUtil.Release(msg);
                }
            }
        }

        public string ToHintString() => $"\'{this.Name}\' will handle the message from this point.";

        public override string ToString() => $"{typeof(IChannelHandlerContext).Name} ({this.Name}, {this.Channel})";


        abstract class AbstractWriteTask : AbstractRecyclableChannelPromise, IRunnable
        {
            static readonly bool EstimateTaskSizeOnSubmit =
                SystemPropertyUtil.GetBoolean("io.netty.transport.estimateSizeOnSubmit", true);

            // Assuming a 64-bit .NET VM, 16 bytes object header, 4 reference fields and 2 int field
            static readonly int WriteTaskOverhead =
                SystemPropertyUtil.GetInt("io.netty.transport.writeTaskSizeOverhead", 56);

            AbstractChannelHandlerContext ctx;
            object msg;
            int size;

            protected static void Init(AbstractWriteTask task, AbstractChannelHandlerContext ctx, object msg)
            {
                task.Init(ctx.Executor);
                task.ctx = ctx;
                task.msg = msg;

                if (EstimateTaskSizeOnSubmit)
                {
                    ChannelOutboundBuffer buffer = ctx.Channel.Unsafe.OutboundBuffer;

                    // Check for null as it may be set to null if the channel is closed already
                    if (buffer != null)
                    {
                        task.size = ctx.pipeline.EstimatorHandle.Size(msg) + WriteTaskOverhead;
                        buffer.IncrementPendingOutboundBytes(task.size);
                    }
                    else
                    {
                        task.size = 0;
                    }
                }
                else
                {
                    task.size = 0;
                }
            }

            protected AbstractWriteTask(ThreadLocalPool.Handle handle) : base(handle)
            {

            }

            public void Run()
            {
                try
                {
                    ChannelOutboundBuffer buffer = this.ctx.Channel.Unsafe.OutboundBuffer;
                    // Check for null as it may be set to null if the channel is closed already
                    if (EstimateTaskSizeOnSubmit)
                    {
                        buffer?.DecrementPendingOutboundBytes(this.size);
                    }

                    this.WriteAsync(this.ctx, this.msg).LinkOutcome(this);
                }
                catch (Exception ex)
                {
                    this.TryComplete(ex);
                }
                finally
                {
                    // Set to null so the GC can collect them directly
                    this.ctx = null;
                    this.msg = null;
                    
                    //this.Recycle();
                    //this.handle.Release(this);
                }
            }

            protected virtual ChannelFuture WriteAsync(AbstractChannelHandlerContext ctx, object msg) => ctx.InvokeWriteAsync(msg);

            /*public override void Recycle()
            {
                base.Recycle();
                this.handle.Release(this);
            }*/
        }
        
        
        sealed class WriteTask : AbstractWriteTask 
        {
            static readonly ThreadLocalPool<WriteTask> Recycler = new ThreadLocalPool<WriteTask>(handle => new WriteTask(handle));

            public static WriteTask NewInstance(AbstractChannelHandlerContext ctx, object msg)
            {
                WriteTask task = Recycler.Take();
                Init(task, ctx, msg);
                return task;
            }

            WriteTask(ThreadLocalPool.Handle handle)
                : base(handle)
            {
            }
        }

        sealed class WriteAndFlushTask : AbstractWriteTask
        {
            static readonly ThreadLocalPool<WriteAndFlushTask> Recycler = new ThreadLocalPool<WriteAndFlushTask>(handle => new WriteAndFlushTask(handle));

            public static WriteAndFlushTask NewInstance(AbstractChannelHandlerContext ctx, object msg) 
            {
                WriteAndFlushTask task = Recycler.Take();
                Init(task, ctx, msg);
                return task;
            }

            WriteAndFlushTask(ThreadLocalPool.Handle handle)
                : base(handle)
            {
            }

            protected override ChannelFuture WriteAsync(AbstractChannelHandlerContext ctx, object msg)
            {
                ChannelFuture result = base.WriteAsync(ctx, msg);
                ctx.InvokeFlush();
                return result;
            }
        }
    }
}