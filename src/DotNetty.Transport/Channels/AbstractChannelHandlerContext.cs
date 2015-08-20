// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;

    abstract class AbstractChannelHandlerContext : IChannelHandlerContext
    {
        static readonly ConditionalWeakTable<Type, Tuple<PropagationDirections>> SkipTable = new ConditionalWeakTable<Type, Tuple<PropagationDirections>>();

        protected static PropagationDirections GetSkipPropagationFlags(IChannelHandler handler)
        {
            Tuple<PropagationDirections> skipDirection = SkipTable.GetValue(
                handler.GetType(),
                handlerType => Tuple.Create(CalculateSkipPropagationFlags(handlerType)));

            return skipDirection == null ? PropagationDirections.None : skipDirection.Item1;
        }

        protected static PropagationDirections CalculateSkipPropagationFlags(Type handlerType)
        {
            bool skipOutbound = true;
            bool skipInbound = true;

            InterfaceMapping mapping = handlerType.GetInterfaceMap(typeof(IChannelHandler));
            for (int index = 0; index < mapping.InterfaceMethods.Length && (skipInbound || skipOutbound); index++)
            {
                MethodInfo method = mapping.InterfaceMethods[index];
                var propagationAttribute = method.GetCustomAttribute<PipelinePropagationAttribute>();
                if (propagationAttribute == null)
                {
                    continue;
                }

                MethodInfo implMethod = mapping.TargetMethods[index];
                if (implMethod.GetCustomAttribute<SkipAttribute>(false) == null)
                {
                    switch (propagationAttribute.Direction)
                    {
                        case PropagationDirections.Inbound:
                            skipInbound = false;
                            break;
                        case PropagationDirections.Outbound:
                            skipOutbound = false;
                            break;
                        default:
                            throw new NotSupportedException(string.Format("PropagationDirection value of {0} is not supported.", propagationAttribute.Direction));
                    }
                }
            }

            var result = PropagationDirections.None;
            if (skipInbound)
            {
                result |= PropagationDirections.Inbound;
            }
            if (skipOutbound)
            {
                result |= PropagationDirections.Outbound;
            }
            return result;
        }

        internal volatile AbstractChannelHandlerContext Next;
        internal volatile AbstractChannelHandlerContext Prev;

        internal readonly PropagationDirections SkipPropagationFlags;
        readonly IChannelHandlerInvoker invoker;
        volatile PausableChannelEventExecutor wrappedEventLoop;

        protected AbstractChannelHandlerContext(IChannelPipeline pipeline, IChannelHandlerInvoker invoker,
            string name, PropagationDirections skipPropagationDirections)
        {
            Contract.Requires(pipeline != null);
            Contract.Requires(name != null);

            this.Channel = pipeline.Channel();
            this.invoker = invoker;
            this.SkipPropagationFlags = skipPropagationDirections;
            this.Name = name;
        }

        public IChannel Channel { get; private set; }

        public IByteBufferAllocator Allocator
        {
            get { return this.Channel.Allocator; }
        }

        public bool Removed { get; internal set; }

        public IEventExecutor Executor
        {
            get
            {
                if (this.invoker == null)
                {
                    return this.Channel.EventLoop;
                }
                else
                {
                    return this.WrappedEventLoop;
                }
            }
        }

        public string Name { get; private set; }

        public IChannelHandlerInvoker Invoker
        {
            get
            {
                if (this.invoker == null)
                {
                    return this.Channel.EventLoop.Invoker;
                }
                else
                {
                    throw new NotImplementedException();
                    //return wrappedEventLoop();
                }
            }
        }

        PausableChannelEventExecutor WrappedEventLoop
        {
            get
            {
                PausableChannelEventExecutor wrapped = this.wrappedEventLoop;
                if (wrapped == null)
                {
                    wrapped = new PausableChannelEventExecutor0(this);
#pragma warning disable 420 // does not apply to Interlocked operations
                    if (Interlocked.CompareExchange(ref this.wrappedEventLoop, wrapped, null) != null)
#pragma warning restore 420
                    {
                        // Set in the meantime so we need to issue another volatile read
                        return this.wrappedEventLoop;
                    }
                }
                return wrapped;
            }
        }

        public IChannelHandlerContext FireChannelRegistered()
        {
            AbstractChannelHandlerContext next = this.FindContextInbound();
            next.Invoker.InvokeChannelRegistered(next);
            return this;
        }

        public IChannelHandlerContext FireChannelUnregistered()
        {
            AbstractChannelHandlerContext next = this.FindContextInbound();
            next.Invoker.InvokeChannelUnregistered(next);
            return this;
        }

        public IChannelHandlerContext FireChannelActive()
        {
            AbstractChannelHandlerContext target = this.FindContextInbound();
            target.Invoker.InvokeChannelActive(target);
            return this;
        }

        public IChannelHandlerContext FireChannelInactive()
        {
            AbstractChannelHandlerContext target = this.FindContextInbound();
            target.Invoker.InvokeChannelInactive(target);
            return this;
        }

        public virtual IChannelHandlerContext FireExceptionCaught(Exception cause)
        {
            AbstractChannelHandlerContext target = this.FindContextInbound();
            target.Invoker.InvokeExceptionCaught(target, cause);
            return this;
        }

        public abstract IChannelHandler Handler { get; }

        public IChannelHandlerContext FireChannelRead(object msg)
        {
            AbstractChannelHandlerContext target = this.FindContextInbound();
            target.Invoker.InvokeChannelRead(target, msg);
            return this;
        }

        public IChannelHandlerContext FireChannelReadComplete()
        {
            AbstractChannelHandlerContext target = this.FindContextInbound();
            target.Invoker.InvokeChannelReadComplete(target);
            return this;
        }

        public IChannelHandlerContext FireChannelWritabilityChanged()
        {
            AbstractChannelHandlerContext next = this.FindContextInbound();
            next.Invoker.InvokeChannelWritabilityChanged(next);
            return this;
        }

        public IChannelHandlerContext FireUserEventTriggered(object evt)
        {
            AbstractChannelHandlerContext target = this.FindContextInbound();
            target.Invoker.InvokeUserEventTriggered(target, evt);
            return this;
        }

        public Task DeregisterAsync()
        {
            AbstractChannelHandlerContext next = this.FindContextOutbound();
            return next.Invoker.InvokeDeregisterAsync(next);
        }

        public IChannelHandlerContext Read()
        {
            AbstractChannelHandlerContext target = this.FindContextOutbound();
            target.Invoker.InvokeRead(target);
            return this;
        }

        public Task WriteAsync(object msg) // todo: cancellationToken?
        {
            AbstractChannelHandlerContext target = this.FindContextOutbound();
            return target.Invoker.InvokeWriteAsync(target, msg);
        }

        public IChannelHandlerContext Flush()
        {
            AbstractChannelHandlerContext target = this.FindContextOutbound();
            target.Invoker.InvokeFlush(target);
            return this;
        }

        public Task WriteAndFlushAsync(object message) // todo: cancellationToken?
        {
            AbstractChannelHandlerContext target;
            target = this.FindContextOutbound();
            Task writeFuture = target.Invoker.InvokeWriteAsync(target, message);
            target = this.FindContextOutbound();
            target.Invoker.InvokeFlush(target);
            return writeFuture;
        }

        public Task BindAsync(EndPoint localAddress)
        {
            AbstractChannelHandlerContext next = this.FindContextOutbound();
            return next.Invoker.InvokeBindAsync(next, localAddress);
        }

        public Task ConnectAsync(EndPoint remoteAddress)
        {
            return this.ConnectAsync(remoteAddress, null);
        }

        public Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
        {
            AbstractChannelHandlerContext next = this.FindContextOutbound();
            return next.Invoker.InvokeConnectAsync(next, remoteAddress, localAddress);
        }

        public Task DisconnectAsync()
        {
            if (!this.Channel.DisconnectSupported)
            {
                return this.CloseAsync();
            }

            AbstractChannelHandlerContext next = this.FindContextOutbound();
            return next.Invoker.InvokeDisconnectAsync(next);
        }

        public Task CloseAsync() // todo: cancellationToken?
        {
            AbstractChannelHandlerContext target = this.FindContextOutbound();
            return target.Invoker.InvokeCloseAsync(target);
        }

        AbstractChannelHandlerContext FindContextInbound()
        {
            AbstractChannelHandlerContext ctx = this;
            do
            {
                ctx = ctx.Next;
            }
            while ((ctx.SkipPropagationFlags & PropagationDirections.Inbound) == PropagationDirections.Inbound);
            return ctx;
        }

        AbstractChannelHandlerContext FindContextOutbound()
        {
            AbstractChannelHandlerContext ctx = this;
            do
            {
                ctx = ctx.Prev;
            }
            while ((ctx.SkipPropagationFlags & PropagationDirections.Outbound) == PropagationDirections.Outbound);
            return ctx;
        }

        public override string ToString()
        {
            return string.Format("{0} ({1}, {2})", typeof(IChannelHandlerContext).Name, this.Name, this.Channel);
        }

        class PausableChannelEventExecutor0 : PausableChannelEventExecutor
        {
            readonly AbstractChannelHandlerContext context;

            public PausableChannelEventExecutor0(AbstractChannelHandlerContext context)
            {
                this.context = context;
            }

            public override void RejectNewTasks()
            {
                /**
             * This cast is correct because {@link #channel()} always returns an {@link AbstractChannel} and
             * {@link AbstractChannel#eventLoop()} always returns a {@link PausableChannelEventExecutor}.
             */
                ((PausableChannelEventExecutor)this.Channel.EventLoop).RejectNewTasks();
            }

            public override void AcceptNewTasks()
            {
                ((PausableChannelEventExecutor)this.Channel.EventLoop).AcceptNewTasks();
            }

            public override bool IsAcceptingNewTasks
            {
                get { return ((PausableChannelEventExecutor)this.Channel.EventLoop).IsAcceptingNewTasks; }
            }

            internal override IChannel Channel
            {
                get { return this.context.Channel; }
            }

            public override IEventExecutor Unwrap()
            {
                return this.UnwrapInvoker().Executor;
            }

            public IChannelHandlerInvoker UnwrapInvoker()
            {
                /**
                 * {@link #invoker} can not be {@code null}, because {@link PausableChannelEventExecutor0} will only be
                 * instantiated if {@link #invoker} is not {@code null}.
                 */
                return this.context.invoker;
            }
        }
    }
}