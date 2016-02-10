// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;

    public class DefaultChannelHandlerInvoker : IChannelHandlerInvoker
    {
        static readonly Action<object> InvokeChannelReadCompleteAction = ctx => ChannelHandlerInvokerUtil.InvokeChannelReadCompleteNow((IChannelHandlerContext)ctx);
        static readonly Action<object> InvokeReadAction = ctx => ChannelHandlerInvokerUtil.InvokeReadNow((IChannelHandlerContext)ctx);
        static readonly Action<object> InvokeChannelWritabilityChangedAction = ctx => ChannelHandlerInvokerUtil.InvokeChannelWritabilityChangedNow((IChannelHandlerContext)ctx);
        static readonly Action<object> InvokeFlushAction = ctx => ChannelHandlerInvokerUtil.InvokeFlushNow((IChannelHandlerContext)ctx);
        static readonly Action<object, object> InvokeUserEventTriggeredAction = (ctx, evt) => ChannelHandlerInvokerUtil.InvokeUserEventTriggeredNow((IChannelHandlerContext)ctx, evt);
        static readonly Action<object, object> InvokeChannelReadAction = (ctx, msg) => ChannelHandlerInvokerUtil.InvokeChannelReadNow((IChannelHandlerContext)ctx, msg);

        static readonly Action<object, object> InvokeWriteAsyncAction = (p, msg) =>
        {
            var promise = (TaskCompletionSource)p;
            var context = (IChannelHandlerContext)promise.Task.AsyncState;
            var channel = (AbstractChannel)context.Channel;
            // todo: size is counted twice. is that a problem?
            int size = channel.EstimatorHandle.Size(msg);
            if (size > 0)
            {
                ChannelOutboundBuffer buffer = channel.Unsafe.OutboundBuffer;
                // Check for null as it may be set to null if the channel is closed already
                if (buffer != null)
                {
                    buffer.DecrementPendingOutboundBytes(size);
                }
            }
            ChannelHandlerInvokerUtil.InvokeWriteAsyncNow(context, msg).LinkOutcome(promise);
        };

        readonly IEventExecutor executor;

        public DefaultChannelHandlerInvoker(IEventExecutor executor)
        {
            Contract.Requires(executor != null);

            this.executor = executor;
        }

        public IEventExecutor Executor
        {
            get { return this.executor; }
        }

        public void InvokeChannelRegistered(IChannelHandlerContext ctx)
        {
            if (this.executor.InEventLoop)
            {
                ChannelHandlerInvokerUtil.InvokeChannelRegisteredNow(ctx);
            }
            else
            {
                this.executor.Execute(c => ChannelHandlerInvokerUtil.InvokeChannelRegisteredNow((IChannelHandlerContext)c), ctx);
            }
        }

        public void InvokeChannelUnregistered(IChannelHandlerContext ctx)
        {
            if (this.executor.InEventLoop)
            {
                ChannelHandlerInvokerUtil.InvokeChannelUnregisteredNow(ctx);
            }
            else
            {
                this.executor.Execute(c => ChannelHandlerInvokerUtil.InvokeChannelUnregisteredNow((IChannelHandlerContext)c), ctx);
            }
        }

        public void InvokeChannelActive(IChannelHandlerContext ctx)
        {
            if (this.executor.InEventLoop)
            {
                ChannelHandlerInvokerUtil.InvokeChannelActiveNow(ctx);
            }
            else
            {
                this.executor.Execute(c => ChannelHandlerInvokerUtil.InvokeChannelActiveNow((IChannelHandlerContext)c), ctx);
            }
        }

        public void InvokeChannelInactive(IChannelHandlerContext ctx)
        {
            if (this.executor.InEventLoop)
            {
                ChannelHandlerInvokerUtil.InvokeChannelInactiveNow(ctx);
            }
            else
            {
                this.executor.Execute(c => ChannelHandlerInvokerUtil.InvokeChannelInactiveNow((IChannelHandlerContext)c), ctx);
            }
        }

        public void InvokeExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            Contract.Requires(cause != null);

            if (this.executor.InEventLoop)
            {
                ChannelHandlerInvokerUtil.InvokeExceptionCaughtNow(ctx, cause);
            }
            else
            {
                try
                {
                    this.executor.Execute((c, e) => ChannelHandlerInvokerUtil.InvokeExceptionCaughtNow((IChannelHandlerContext)c, (Exception)e), ctx, cause);
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

        public void InvokeUserEventTriggered(IChannelHandlerContext ctx, object evt)
        {
            Contract.Requires(evt != null);

            if (this.executor.InEventLoop)
            {
                ChannelHandlerInvokerUtil.InvokeUserEventTriggeredNow(ctx, evt);
            }
            else
            {
                this.SafeProcessInboundMessage(InvokeUserEventTriggeredAction, ctx, evt);
            }
        }

        public void InvokeChannelRead(IChannelHandlerContext ctx, object msg)
        {
            Contract.Requires(msg != null);

            if (this.executor.InEventLoop)
            {
                ChannelHandlerInvokerUtil.InvokeChannelReadNow(ctx, msg);
            }
            else
            {
                this.SafeProcessInboundMessage(InvokeChannelReadAction, ctx, msg);
            }
        }

        public void InvokeChannelReadComplete(IChannelHandlerContext ctx)
        {
            if (this.executor.InEventLoop)
            {
                ChannelHandlerInvokerUtil.InvokeChannelReadCompleteNow(ctx);
            }
            else
            {
                this.executor.Execute(InvokeChannelReadCompleteAction, ctx);
            }
        }

        public void InvokeChannelWritabilityChanged(IChannelHandlerContext ctx)
        {
            if (this.executor.InEventLoop)
            {
                ChannelHandlerInvokerUtil.InvokeChannelWritabilityChangedNow(ctx);
            }
            else
            {
                this.executor.Execute(InvokeChannelWritabilityChangedAction, ctx);
            }
        }

        public Task InvokeBindAsync(
            IChannelHandlerContext ctx, EndPoint localAddress)
        {
            Contract.Requires(localAddress != null);
            // todo: check for cancellation
            //if (!validatePromise(ctx, promise, false)) {
            //    // promise cancelled
            //    return;
            //}

            if (this.executor.InEventLoop)
            {
                return ChannelHandlerInvokerUtil.InvokeBindAsyncNow(ctx, localAddress);
            }
            else
            {
                return this.SafeExecuteOutboundAsync(() => ChannelHandlerInvokerUtil.InvokeBindAsyncNow(ctx, localAddress));
            }
        }

        public Task InvokeConnectAsync(
            IChannelHandlerContext ctx,
            EndPoint remoteAddress, EndPoint localAddress)
        {
            Contract.Requires(remoteAddress != null);
            // todo: check for cancellation
            //if (!validatePromise(ctx, promise, false)) {
            //    // promise cancelled
            //    return;
            //}

            if (this.executor.InEventLoop)
            {
                return ChannelHandlerInvokerUtil.InvokeConnectAsyncNow(ctx, remoteAddress, localAddress);
            }
            else
            {
                return this.SafeExecuteOutboundAsync(() => ChannelHandlerInvokerUtil.InvokeConnectAsyncNow(ctx, remoteAddress, localAddress));
            }
        }

        public Task InvokeDisconnectAsync(IChannelHandlerContext ctx)
        {
            // todo: check for cancellation
            //if (!validatePromise(ctx, promise, false)) {
            //    // promise cancelled
            //    return;
            //}

            if (this.executor.InEventLoop)
            {
                return ChannelHandlerInvokerUtil.InvokeDisconnectAsyncNow(ctx);
            }
            else
            {
                return this.SafeExecuteOutboundAsync(() => ChannelHandlerInvokerUtil.InvokeDisconnectAsyncNow(ctx));
            }
        }

        public Task InvokeCloseAsync(IChannelHandlerContext ctx)
        {
            // todo: check for cancellation
            //if (!validatePromise(ctx, promise, false)) {
            //    // promise cancelled
            //    return;
            //}

            if (this.executor.InEventLoop)
            {
                return ChannelHandlerInvokerUtil.InvokeCloseAsyncNow(ctx);
            }
            else
            {
                return this.SafeExecuteOutboundAsync(() => ChannelHandlerInvokerUtil.InvokeCloseAsyncNow(ctx));
            }
        }

        public Task InvokeDeregisterAsync(IChannelHandlerContext ctx)
        {
            // todo: check for cancellation
            //if (!validatePromise(ctx, promise, false)) {
            //    // promise cancelled
            //    return;
            //}

            if (this.executor.InEventLoop)
            {
                return ChannelHandlerInvokerUtil.InvokeDeregisterAsyncNow(ctx);
            }
            else
            {
                return this.SafeExecuteOutboundAsync(() => ChannelHandlerInvokerUtil.InvokeDeregisterAsyncNow(ctx));
            }
        }

        public void InvokeRead(IChannelHandlerContext ctx)
        {
            if (this.executor.InEventLoop)
            {
                ChannelHandlerInvokerUtil.InvokeReadNow(ctx);
            }
            else
            {
                this.executor.Execute(InvokeReadAction, ctx);
            }
        }

        public Task InvokeWriteAsync(IChannelHandlerContext ctx, object msg)
        {
            Contract.Requires(msg != null);
            // todo: check for cancellation
            //if (!validatePromise(ctx, promise, false)) {
            //    // promise cancelled
            //    return;
            //}

            if (this.executor.InEventLoop)
            {
                return ChannelHandlerInvokerUtil.InvokeWriteAsyncNow(ctx, msg);
            }
            else
            {
                var channel = (AbstractChannel)ctx.Channel;
                var promise = new TaskCompletionSource(ctx);

                try
                {
                    int size = channel.EstimatorHandle.Size(msg);
                    if (size > 0)
                    {
                        ChannelOutboundBuffer buffer = channel.Unsafe.OutboundBuffer;
                        // Check for null as it may be set to null if the channel is closed already
                        if (buffer != null)
                        {
                            buffer.IncrementPendingOutboundBytes(size);
                        }
                    }

                    this.executor.Execute(InvokeWriteAsyncAction, promise, msg);
                }
                catch (Exception cause)
                {
                    ReferenceCountUtil.Release(msg); // todo: safe release?
                    promise.TrySetException(cause);
                }
                return promise.Task;
            }
        }

        public void InvokeFlush(IChannelHandlerContext ctx)
        {
            if (this.executor.InEventLoop)
            {
                ChannelHandlerInvokerUtil.InvokeFlushNow(ctx);
            }
            else
            {
                this.executor.Execute(InvokeFlushAction, ctx);
            }
        }

        void SafeProcessInboundMessage(Action<object, object> action, object state, object msg)
        {
            bool success = false;
            try
            {
                this.executor.Execute(action, state, msg);
                success = true;
            }
            finally
            {
                if (!success)
                {
                    ReferenceCountUtil.Release(msg);
                }
            }
        }

        Task SafeExecuteOutboundAsync(Func<Task> function)
        {
            var promise = new TaskCompletionSource();
            try
            {
                this.executor.Execute((p, func) => ((Func<Task>)func)().LinkOutcome((TaskCompletionSource)p), promise, function);
            }
            catch (Exception cause)
            {
                promise.TrySetException(cause);
            }
            return promise.Task;
        }
    }
}