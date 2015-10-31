// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Diagnostics;
    using System.Linq.Expressions;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;

    public static class ChannelHandlerInvokerUtil
    {
        public static void InvokeChannelRegisteredNow(IChannelHandlerContext ctx)
        {
            try
            {
                ctx.Handler.ChannelRegistered(ctx);
            }
            catch (Exception ex)
            {
                NotifyHandlerException(ctx, ex);
            }
        }

        public static void InvokeChannelUnregisteredNow(IChannelHandlerContext ctx)
        {
            try
            {
                ctx.Handler.ChannelUnregistered(ctx);
            }
            catch (Exception ex)
            {
                NotifyHandlerException(ctx, ex);
            }
        }

        public static void InvokeChannelActiveNow(IChannelHandlerContext ctx)
        {
            try
            {
                ctx.Handler.ChannelActive(ctx);
            }
            catch (Exception ex)
            {
                NotifyHandlerException(ctx, ex);
            }
        }

        public static void InvokeChannelInactiveNow(IChannelHandlerContext ctx)
        {
            try
            {
                ctx.Handler.ChannelInactive(ctx);
            }
            catch (Exception ex)
            {
                NotifyHandlerException(ctx, ex);
            }
        }

        public static void InvokeExceptionCaughtNow(IChannelHandlerContext ctx, Exception cause)
        {
            try
            {
                ctx.Handler.ExceptionCaught(ctx, cause);
            }
            catch (Exception ex)
            {
                if (DefaultChannelPipeline.Logger.WarnEnabled)
                {
                    DefaultChannelPipeline.Logger.Warn("An exception was thrown by a user handler's exceptionCaught() method:", ex);
                    DefaultChannelPipeline.Logger.Warn(".. and the cause of the exceptionCaught() was:", cause);
                }
            }
        }

        public static void InvokeUserEventTriggeredNow(IChannelHandlerContext ctx, object evt)
        {
            try
            {
                ctx.Handler.UserEventTriggered(ctx, evt);
            }
            catch (Exception ex)
            {
                NotifyHandlerException(ctx, ex);
            }
        }

        public static void InvokeChannelReadNow(IChannelHandlerContext ctx, object msg)
        {
            try
            {
                ctx.Handler.ChannelRead(ctx, msg);
            }
            catch (Exception ex)
            {
                NotifyHandlerException(ctx, ex);
            }
        }

        public static void InvokeChannelReadCompleteNow(IChannelHandlerContext ctx)
        {
            try
            {
                ctx.Handler.ChannelReadComplete(ctx);
            }
            catch (Exception ex)
            {
                NotifyHandlerException(ctx, ex);
            }
        }

        public static void InvokeChannelWritabilityChangedNow(IChannelHandlerContext ctx)
        {
            try
            {
                ctx.Handler.ChannelWritabilityChanged(ctx);
            }
            catch (Exception ex)
            {
                NotifyHandlerException(ctx, ex);
            }
        }

        public static Task InvokeBindAsyncNow(
            IChannelHandlerContext ctx, EndPoint localAddress)
        {
            try
            {
                return ctx.Handler.BindAsync(ctx, localAddress);
            }
            catch (Exception ex)
            {
                return ComposeExceptionTask(ex);
            }
        }

        public static Task InvokeConnectAsyncNow(
            IChannelHandlerContext ctx,
            EndPoint remoteAddress, EndPoint localAddress)
        {
            try
            {
                return ctx.Handler.ConnectAsync(ctx, remoteAddress, localAddress);
            }
            catch (Exception ex)
            {
                return ComposeExceptionTask(ex);
            }
        }

        public static Task InvokeDisconnectAsyncNow(IChannelHandlerContext ctx)
        {
            try
            {
                return ctx.Handler.DisconnectAsync(ctx);
            }
            catch (Exception ex)
            {
                return ComposeExceptionTask(ex);
            }
        }

        public static Task InvokeCloseAsyncNow(IChannelHandlerContext ctx)
        {
            try
            {
                return ctx.Handler.CloseAsync(ctx);
            }
            catch (Exception ex)
            {
                return ComposeExceptionTask(ex);
            }
        }

        public static Task InvokeDeregisterAsyncNow(IChannelHandlerContext ctx)
        {
            try
            {
                return ctx.Handler.DeregisterAsync(ctx);
            }
            catch (Exception ex)
            {
                return ComposeExceptionTask(ex);
            }
        }

        public static void InvokeReadNow(IChannelHandlerContext ctx)
        {
            try
            {
                ctx.Handler.Read(ctx);
            }
            catch (Exception ex)
            {
                NotifyHandlerException(ctx, ex);
            }
        }

        public static Task InvokeWriteAsyncNow(IChannelHandlerContext ctx, object msg)
        {
            try
            {
                return ctx.Handler.WriteAsync(ctx, msg);
            }
            catch (Exception ex)
            {
                return ComposeExceptionTask(ex);
            }
        }

        public static void InvokeFlushNow(IChannelHandlerContext ctx)
        {
            try
            {
                ctx.Handler.Flush(ctx);
            }
            catch (Exception ex)
            {
                NotifyHandlerException(ctx, ex);
            }
        }

        static void NotifyHandlerException(IChannelHandlerContext ctx, Exception cause)
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

            InvokeExceptionCaughtNow(ctx, cause);
        }

        static Task ComposeExceptionTask(Exception cause)
        {
            return TaskEx.FromException(cause);
        }

        // todo: use "nameof" once available
        static readonly string ExceptionCaughtMethodName = ((MethodCallExpression)((Expression<Action<IChannelHandler>>)(_ => _.ExceptionCaught(null, null))).Body).Method.Name;

        static bool InExceptionCaught(Exception cause)
        {
            do
            {
                var trace = new StackTrace(cause);
                for (int index = 0; index < trace.FrameCount; index++)
                {
                    StackFrame frame = trace.GetFrame(index);
                    if (frame == null)
                    {
                        break;
                    }

                    if (ExceptionCaughtMethodName.Equals(frame.GetMethod().Name, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                cause = cause.InnerException;
            }
            while (cause != null);

            return false;
        }
    }
}