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
        /// The <see cref="IChannel"/> of the <see cref="IChannelHandlerContext"/> was registered with its
        /// <see cref="IEventLoop"/>.
        /// </summary>
        void ChannelRegistered(IChannelHandlerContext context);

        /// <summary>
        /// The <see cref="IChannel"/> of the <see cref="IChannelHandlerContext"/> was unregistered from its
        /// <see cref="IEventLoop"/>.
        /// </summary>
        void ChannelUnregistered(IChannelHandlerContext context);

        void ChannelActive(IChannelHandlerContext context);

        void ChannelInactive(IChannelHandlerContext context);

        void ChannelRead(IChannelHandlerContext context, object message);

        void ChannelReadComplete(IChannelHandlerContext context);

        /// <summary>
        /// Gets called once the writable state of a <see cref="IChannel"/> changed. You can check the state with
        /// <see cref="IChannel.IsWritable"/>.
        /// </summary>
        void ChannelWritabilityChanged(IChannelHandlerContext context);

        void HandlerAdded(IChannelHandlerContext context);

        void HandlerRemoved(IChannelHandlerContext context);

        Task WriteAsync(IChannelHandlerContext context, object message);

        void Flush(IChannelHandlerContext context);

        /// <summary>
        /// Called once a bind operation is made.
        /// </summary>
        /// <param name="context">
        /// The <see cref="IChannelHandlerContext"/> for which the bind operation is made.
        /// </param>
        /// <param name="localAddress">The <see cref="EndPoint"/> to which it should bind.</param>
        /// <returns>An await-able task.</returns>
        Task BindAsync(IChannelHandlerContext context, EndPoint localAddress);

        /// <summary>
        /// Called once a connect operation is made.
        /// </summary>
        /// <param name="context">
        /// The <see cref="IChannelHandlerContext"/> for which the connect operation is made.
        /// </param>
        /// <param name="remoteAddress">The <see cref="EndPoint"/> to which it should connect.</param>
        /// <param name="localAddress">The <see cref="EndPoint"/> which is used as source on connect.</param>
        /// <returns>An await-able task.</returns>
        Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress);

        /// <summary>
        /// Called once a disconnect operation is made.
        /// </summary>
        /// <param name="context">
        /// The <see cref="IChannelHandlerContext"/> for which the disconnect operation is made.
        /// </param>
        /// <returns>An await-able task.</returns>
        Task DisconnectAsync(IChannelHandlerContext context);

        Task CloseAsync(IChannelHandlerContext context);

        void ExceptionCaught(IChannelHandlerContext context, Exception exception);

        Task DeregisterAsync(IChannelHandlerContext context);

        void Read(IChannelHandlerContext context);

        void UserEventTriggered(IChannelHandlerContext context, object evt);
    }
}