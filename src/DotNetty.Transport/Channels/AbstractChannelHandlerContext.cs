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
    using DotNetty.Common.Utilities;

    abstract class AbstractChannelHandlerContext : IChannelHandlerContext, IResourceLeakHint
    {
        internal const int MASK_HANDLER_ADDED = 1;
        internal const int MASK_HANDLER_REMOVED = 1 << 1;

        internal const int MASK_EXCEPTION_CAUGHT = 1 << 2;
        internal const int MASK_CHANNEL_REGISTERED = 1 << 3;
        internal const int MASK_CHANNEL_UNREGISTERED = 1 << 4;
        internal const int MASK_CHANNEL_ACTIVE = 1 << 5;
        internal const int MASK_CHANNEL_INACTIVE = 1 << 6;
        internal const int MASK_CHANNEL_READ = 1 << 7;
        internal const int MASK_CHANNEL_READ_COMPLETE = 1 << 8;
        internal const int MASK_CHANNEL_WRITABILITY_CHANGED = 1 << 9;
        internal const int MASK_USER_EVENT_TRIGGERED = 1 << 10;

        internal const int MASK_BIND = 1 << 11;
        internal const int MASK_CONNECT = 1 << 12;
        internal const int MASK_DISCONNECT = 1 << 13;
        internal const int MASK_CLOSE = 1 << 14;
        internal const int MASK_DEREGISTER = 1 << 15;
        internal const int MASK_READ = 1 << 16;
        internal const int MASK_WRITE = 1 << 17;
        internal const int MASK_FLUSH = 1 << 18;

        internal const int MASKGROUP_INBOUND = MASK_EXCEPTION_CAUGHT |
            MASK_CHANNEL_REGISTERED |
            MASK_CHANNEL_UNREGISTERED |
            MASK_CHANNEL_ACTIVE |
            MASK_CHANNEL_INACTIVE |
            MASK_CHANNEL_READ |
            MASK_CHANNEL_READ_COMPLETE |
            MASK_CHANNEL_WRITABILITY_CHANGED |
            MASK_USER_EVENT_TRIGGERED;

        internal const int MASKGROUP_OUTBOUND = MASK_BIND |
            MASK_CONNECT |
            MASK_DISCONNECT |
            MASK_CLOSE |
            MASK_DEREGISTER |
            MASK_READ |
            MASK_WRITE |
            MASK_FLUSH;

        static readonly ConditionalWeakTable<Type, Tuple<int>> SkipTable = new ConditionalWeakTable<Type, Tuple<int>>();

        protected static int GetSkipPropagationFlags(IChannelHandler handler)
        {
            Tuple<int> skipDirection = SkipTable.GetValue(
                handler.GetType(),
                handlerType => Tuple.Create(CalculateSkipPropagationFlags(handlerType)));

            return skipDirection?.Item1 ?? 0;
        }

        protected static int CalculateSkipPropagationFlags(Type handlerType)
        {
            int flags = 0;

            // this method should never throw
            if (IsSkippable(handlerType, "HandlerAdded"))
            {
                flags |= MASK_HANDLER_ADDED;
            }
            if (IsSkippable(handlerType, "HandlerRemoved"))
            {
                flags |= MASK_HANDLER_REMOVED;
            }
            if (IsSkippable(handlerType, "ExceptionCaught", typeof(Exception)))
            {
                flags |= MASK_EXCEPTION_CAUGHT;
            }
            if (IsSkippable(handlerType, "ChannelRegistered"))
            {
                flags |= MASK_CHANNEL_REGISTERED;
            }
            if (IsSkippable(handlerType, "ChannelUnregistered"))
            {
                flags |= MASK_CHANNEL_UNREGISTERED;
            }
            if (IsSkippable(handlerType, "ChannelActive"))
            {
                flags |= MASK_CHANNEL_ACTIVE;
            }
            if (IsSkippable(handlerType, "ChannelInactive"))
            {
                flags |= MASK_CHANNEL_INACTIVE;
            }
            if (IsSkippable(handlerType, "ChannelRead", typeof(object)))
            {
                flags |= MASK_CHANNEL_READ;
            }
            if (IsSkippable(handlerType, "ChannelReadComplete"))
            {
                flags |= MASK_CHANNEL_READ_COMPLETE;
            }
            if (IsSkippable(handlerType, "ChannelWritabilityChanged"))
            {
                flags |= MASK_CHANNEL_WRITABILITY_CHANGED;
            }
            if (IsSkippable(handlerType, "UserEventTriggered", typeof(object)))
            {
                flags |= MASK_USER_EVENT_TRIGGERED;
            }
            if (IsSkippable(handlerType, "BindAsync", typeof(EndPoint)))
            {
                flags |= MASK_BIND;
            }
            if (IsSkippable(handlerType, "ConnectAsync", typeof(EndPoint), typeof(EndPoint)))
            {
                flags |= MASK_CONNECT;
            }
            if (IsSkippable(handlerType, "DisconnectAsync"))
            {
                flags |= MASK_DISCONNECT;
            }
            if (IsSkippable(handlerType, "CloseAsync"))
            {
                flags |= MASK_CLOSE;
            }
            if (IsSkippable(handlerType, "DeregisterAsync"))
            {
                flags |= MASK_DEREGISTER;
            }
            if (IsSkippable(handlerType, "Read"))
            {
                flags |= MASK_READ;
            }
            if (IsSkippable(handlerType, "WriteAsync", typeof(object)))
            {
                flags |= MASK_WRITE;
            }
            if (IsSkippable(handlerType, "Flush"))
            {
                flags |= MASK_FLUSH;
            }
            return flags;
        }

        protected static bool IsSkippable(Type handlerType, string methodName, params Type[] paramTypes)
        {
            var newParamTypes = new Type[paramTypes.Length + 1];
            newParamTypes[0] = typeof(IChannelHandlerContext);
            Array.Copy(paramTypes, 0, newParamTypes, 1, paramTypes.Length);
            return handlerType.GetMethod(methodName, newParamTypes).GetCustomAttribute<SkipAttribute>(false) != null;
        }

        internal volatile AbstractChannelHandlerContext Next;
        internal volatile AbstractChannelHandlerContext Prev;

        internal readonly int SkipPropagationFlags;

        readonly DefaultChannelPipeline pipeline;
        internal readonly IChannelHandlerInvoker invoker;

        protected AbstractChannelHandlerContext(DefaultChannelPipeline pipeline, IChannelHandlerInvoker invoker,
            string name, int skipPropagationDirections)
        {
            Contract.Requires(pipeline != null);
            Contract.Requires(name != null);

            this.pipeline = pipeline;
            this.Name = name;
            this.invoker = invoker;
            this.SkipPropagationFlags = skipPropagationDirections;
        }

        public IChannel Channel => this.pipeline.Channel;

        public IByteBufferAllocator Allocator => this.Channel.Allocator;

        public bool Removed { get; internal set; }

        public IEventExecutor Executor => this.Invoker.Executor;

        public string Name { get; }

        public IChannelHandlerInvoker Invoker => this.invoker ?? this.Channel.EventLoop.Invoker;

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
            target.Invoker.InvokeChannelRead(target, this.pipeline.Touch(msg, target));
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
            return target.Invoker.InvokeWriteAsync(target, this.pipeline.Touch(msg, target));
        }

        public IChannelHandlerContext Flush()
        {
            AbstractChannelHandlerContext target = this.FindContextOutbound();
            target.Invoker.InvokeFlush(target);
            return this;
        }

        public Task WriteAndFlushAsync(object message) // todo: cancellationToken?
        {
            AbstractChannelHandlerContext target = this.FindContextOutbound();
            Task writeFuture = target.Invoker.InvokeWriteAsync(target, this.pipeline.Touch(message, target));
            target = this.FindContextOutbound();
            target.Invoker.InvokeFlush(target);
            return writeFuture;
        }

        public Task BindAsync(EndPoint localAddress)
        {
            AbstractChannelHandlerContext next = this.FindContextOutbound();
            return next.Invoker.InvokeBindAsync(next, localAddress);
        }

        public Task ConnectAsync(EndPoint remoteAddress) => this.ConnectAsync(remoteAddress, null);

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
            while ((ctx.SkipPropagationFlags & MASKGROUP_INBOUND) == MASKGROUP_INBOUND);
            return ctx;
        }

        AbstractChannelHandlerContext FindContextOutbound()
        {
            AbstractChannelHandlerContext ctx = this;
            do
            {
                ctx = ctx.Prev;
            }
            while ((ctx.SkipPropagationFlags & MASKGROUP_OUTBOUND) == MASKGROUP_OUTBOUND);
            return ctx;
        }

        public string ToHintString() => $"\'{this.Name}\' will handle the message from this point.";

        public override string ToString() => $"{typeof(IChannelHandlerContext).Name} ({this.Name}, {this.Channel})";
    }
}