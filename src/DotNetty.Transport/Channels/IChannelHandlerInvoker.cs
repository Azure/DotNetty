// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    /// <summary>
    /// Invokes the event handler methods of {@link ChannelHandler}.
    /// A user can specify a {@link ChannelHandlerInvoker} to implement a custom thread model unsupported by the default
    /// implementation. Note that the methods in this interface are not intended to be called by a user.
    /// </summary>
    public interface IChannelHandlerInvoker
    {
        /// <summary>
        /// Returns the {@link IEventExecutor} which is used to execute an arbitrary task.
        /// </summary>
        IEventExecutor Executor { get; }

        /// <summary>
        /// Invokes {@link ChannelHandler#channelRegistered(ChannelHandlerContext)}. This method is not for a user
        /// but for the internal {@link ChannelHandlerContext} implementation. To trigger an event, use the methods in
        /// {@link ChannelHandlerContext} instead.
        /// </summary>
        void InvokeChannelRegistered(IChannelHandlerContext ctx);

        /// <summary>
        /// Invokes {@link ChannelHandler#channelUnregistered(ChannelHandlerContext)}. This method is not for a user
        /// but for the internal {@link ChannelHandlerContext} implementation. To trigger an event, use the methods in
        /// {@link ChannelHandlerContext} instead.
        /// </summary>
        void InvokeChannelUnregistered(IChannelHandlerContext ctx);

        /// <summary>
        /// Invokes {@link ChannelHandler#channelActive(ChannelHandlerContext)}. This method is not for a user
        /// but for the internal {@link ChannelHandlerContext} implementation. To trigger an event, use the methods in
        /// {@link ChannelHandlerContext} instead.
        /// </summary>
        void InvokeChannelActive(IChannelHandlerContext ctx);

        /// <summary>
        /// Invokes {@link ChannelHandler#channelInactive(ChannelHandlerContext)}. This method is not for a user
        /// but for the internal {@link ChannelHandlerContext} implementation. To trigger an event, use the methods in
        /// {@link ChannelHandlerContext} instead.
        /// </summary>
        void InvokeChannelInactive(IChannelHandlerContext ctx);

        /// <summary>
        /// Invokes {@link ChannelHandler#exceptionCaught(ChannelHandlerContext, Throwable)}. This method is not for a user
        /// but for the internal {@link ChannelHandlerContext} implementation. To trigger an event, use the methods in
        /// {@link ChannelHandlerContext} instead.
        /// </summary>
        void InvokeExceptionCaught(IChannelHandlerContext ctx, Exception cause);

        /// <summary>
        /// Invokes {@link ChannelHandler#userEventTriggered(ChannelHandlerContext, Object)}. This method is not for
        /// a user but for the internal {@link ChannelHandlerContext} implementation. To trigger an event, use the methods in
        /// {@link ChannelHandlerContext} instead.
        /// </summary>
        void InvokeUserEventTriggered(IChannelHandlerContext ctx, object evt);

        /// <summary>
        /// Invokes {@link ChannelHandler#channelRead(ChannelHandlerContext, Object)}. This method is not for a user
        /// but for the internal {@link ChannelHandlerContext} implementation. To trigger an event, use the methods in
        /// {@link ChannelHandlerContext} instead.
        /// </summary>
        void InvokeChannelRead(IChannelHandlerContext ctx, object msg);

        /// <summary>
        /// Invokes {@link ChannelHandler#channelReadComplete(ChannelHandlerContext)}. This method is not for a user
        /// but for the internal {@link ChannelHandlerContext} implementation. To trigger an event, use the methods in
        /// {@link ChannelHandlerContext} instead.
        /// </summary>
        void InvokeChannelReadComplete(IChannelHandlerContext ctx);

        /// <summary>
        /// Invokes {@link ChannelHandler#channelWritabilityChanged(ChannelHandlerContext)}. This method is not for
        /// a user but for the internal {@link ChannelHandlerContext} implementation. To trigger an event, use the methods in
        /// {@link ChannelHandlerContext} instead.
        /// </summary>
        void InvokeChannelWritabilityChanged(IChannelHandlerContext ctx);

        /// <summary>
        /// Invokes {@link ChannelHandler#bind(ChannelHandlerContext, SocketAddress, ChannelPromise)}.
        /// This method is not for a user but for the internal {@link ChannelHandlerContext} implementation.
        /// To trigger an event, use the methods in {@link ChannelHandlerContext} instead.
        /// </summary>
        Task InvokeBindAsync(IChannelHandlerContext ctx, EndPoint localAddress);

        /// <summary>
        /// Invokes
        /// {@link ChannelHandler#connect(ChannelHandlerContext, SocketAddress, SocketAddress, ChannelPromise)}.
        /// This method is not for a user but for the internal {@link ChannelHandlerContext} implementation.
        /// To trigger an event, use the methods in {@link ChannelHandlerContext} instead.
        /// </summary>
        Task InvokeConnectAsync(
            IChannelHandlerContext ctx, EndPoint remoteAddress, EndPoint localAddress);

        /// <summary>
        /// Invokes {@link ChannelHandler#disconnect(ChannelHandlerContext, ChannelPromise)}.
        /// This method is not for a user but for the internal {@link ChannelHandlerContext} implementation.
        /// To trigger an event, use the methods in {@link ChannelHandlerContext} instead.
        /// </summary>
        Task InvokeDisconnectAsync(IChannelHandlerContext ctx);

        /// <summary>
        /// Invokes {@link ChannelHandler#close(ChannelHandlerContext, ChannelPromise)}.
        /// This method is not for a user but for the internal {@link ChannelHandlerContext} implementation.
        /// To trigger an event, use the methods in {@link ChannelHandlerContext} instead.
        /// </summary>
        Task InvokeCloseAsync(IChannelHandlerContext ctx);

        /// <summary>
        /// Invokes {@link ChannelHandler#deregister(ChannelHandlerContext, ChannelPromise)}.
        /// This method is not for a user but for the internal {@link ChannelHandlerContext} implementation.
        /// To trigger an event, use the methods in {@link ChannelHandlerContext} instead.
        /// </summary>
        Task InvokeDeregisterAsync(IChannelHandlerContext ctx);

        /// <summary>
        /// Invokes {@link ChannelHandler#read(ChannelHandlerContext)}.
        /// This method is not for a user but for the internal {@link ChannelHandlerContext} implementation.
        /// To trigger an event, use the methods in {@link ChannelHandlerContext} instead.
        /// </summary>
        void InvokeRead(IChannelHandlerContext ctx);

        /// <summary>
        /// Invokes {@link ChannelHandler#write(ChannelHandlerContext, Object, ChannelPromise)}.
        /// This method is not for a user but for the internal {@link ChannelHandlerContext} implementation.
        /// To trigger an event, use the methods in {@link ChannelHandlerContext} instead.
        /// </summary>
        Task InvokeWriteAsync(IChannelHandlerContext ctx, object msg);

        /// <summary>
        /// Invokes {@link ChannelHandler#flush(ChannelHandlerContext)}.
        /// This method is not for a user but for the internal {@link ChannelHandlerContext} implementation.
        /// To trigger an event, use the methods in {@link ChannelHandlerContext} instead.
        /// </summary>
        void InvokeFlush(IChannelHandlerContext ctx);
    }
}