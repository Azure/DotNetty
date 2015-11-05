// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Net;
    using System.Threading.Tasks;

    public interface IChannelHandler
    {
        /// <summary>
        /// The {@link Channel} of the {@link ChannelHandlerContext} was registered with its {@link EventLoop}
        /// </summary>
        void ChannelRegistered(IChannelHandlerContext context);

        /// <summary>
        /// The {@link Channel} of the {@link ChannelHandlerContext} was unregistered from its {@link EventLoop}
        /// </summary>
        void ChannelUnregistered(IChannelHandlerContext context);

        void ChannelActive(IChannelHandlerContext context);

        void ChannelInactive(IChannelHandlerContext context);

        void ChannelRead(IChannelHandlerContext context, object message);

        void ChannelReadComplete(IChannelHandlerContext context);

        /// <summary>
        /// Gets called once the writable state of a {@link Channel} changed. You can check the state with
        /// {@link Channel#isWritable()}.
        /// </summary>
        void ChannelWritabilityChanged(IChannelHandlerContext context);

        void HandlerAdded(IChannelHandlerContext context);

        void HandlerRemoved(IChannelHandlerContext context);

        Task WriteAsync(IChannelHandlerContext context, object message);
        void Flush(IChannelHandlerContext context);

        /// <summary>
        /// Called once a bind operation is made.
        ///
        /// @param context           the {@link ChannelHandlerContext} for which the bind operation is made
        /// @param localAddress  the {@link java.net.SocketAddress} to which it should bound
        /// @param promise       the {@link ChannelPromise} to notify once the operation completes
        /// @throws Exception    thrown if an error accour
        /// </summary>
        Task BindAsync(IChannelHandlerContext context, EndPoint localAddress);

        /// <summary>
        /// Called once a connect operation is made.
        ///
        /// @param context               the {@link ChannelHandlerContext} for which the connect operation is made
        /// @param remoteAddress     the {@link SocketAddress} to which it should connect
        /// @param localAddress      the {@link SocketAddress} which is used as source on connect
        /// @param promise           the {@link ChannelPromise} to notify once the operation completes
        /// @throws Exception        thrown if an error accour
        /// </summary>
        Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress);

        /// <summary>
        /// Called once a disconnect operation is made.
        ///
        /// @param context               the {@link ChannelHandlerContext} for which the disconnect operation is made
        /// @param promise           the {@link ChannelPromise} to notify once the operation completes
        /// @throws Exception        thrown if an error accour
        /// </summary>
        Task DisconnectAsync(IChannelHandlerContext context);
        Task CloseAsync(IChannelHandlerContext context);

        void ExceptionCaught(IChannelHandlerContext context, Exception exception);

        Task DeregisterAsync(IChannelHandlerContext context);

        void Read(IChannelHandlerContext context);

        void UserEventTriggered(IChannelHandlerContext context, object evt);
    }
}