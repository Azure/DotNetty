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
        [PipelinePropagation(PropagationDirections.Inbound)]
        void ChannelRegistered(IChannelHandlerContext context);

        /// <summary>
        /// The {@link Channel} of the {@link ChannelHandlerContext} was unregistered from its {@link EventLoop}
        /// </summary>
        [PipelinePropagation(PropagationDirections.Inbound)]
        void ChannelUnregistered(IChannelHandlerContext context);

        [PipelinePropagation(PropagationDirections.Inbound)]
        void ChannelActive(IChannelHandlerContext context);

        [PipelinePropagation(PropagationDirections.Inbound)]
        void ChannelInactive(IChannelHandlerContext context);

        [PipelinePropagation(PropagationDirections.Inbound)]
        void ChannelRead(IChannelHandlerContext context, object message);

        [PipelinePropagation(PropagationDirections.Inbound)]
        void ChannelReadComplete(IChannelHandlerContext context);

        /// <summary>
        /// Gets called once the writable state of a {@link Channel} changed. You can check the state with
        /// {@link Channel#isWritable()}.
        /// </summary>
        [PipelinePropagation(PropagationDirections.Inbound)]
        void ChannelWritabilityChanged(IChannelHandlerContext context);

        void HandlerAdded(IChannelHandlerContext context);

        void HandlerRemoved(IChannelHandlerContext context);

        [PipelinePropagation(PropagationDirections.Outbound)]
        Task WriteAsync(IChannelHandlerContext context, object message);

        [PipelinePropagation(PropagationDirections.Outbound)]
        void Flush(IChannelHandlerContext context);

        /// <summary>
        /// Called once a bind operation is made.
        ///
        /// @param context           the {@link ChannelHandlerContext} for which the bind operation is made
        /// @param localAddress  the {@link java.net.SocketAddress} to which it should bound
        /// @param promise       the {@link ChannelPromise} to notify once the operation completes
        /// @throws Exception    thrown if an error accour
        /// </summary>
        [PipelinePropagation(PropagationDirections.Outbound)]
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
        [PipelinePropagation(PropagationDirections.Outbound)]
        Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress);

        /// <summary>
        /// Called once a disconnect operation is made.
        ///
        /// @param context               the {@link ChannelHandlerContext} for which the disconnect operation is made
        /// @param promise           the {@link ChannelPromise} to notify once the operation completes
        /// @throws Exception        thrown if an error accour
        /// </summary>
        [PipelinePropagation(PropagationDirections.Outbound)]
        Task DisconnectAsync(IChannelHandlerContext context);

        [PipelinePropagation(PropagationDirections.Outbound)]
        Task CloseAsync(IChannelHandlerContext context);

        [PipelinePropagation(PropagationDirections.Inbound)]
        void ExceptionCaught(IChannelHandlerContext context, Exception exception);

        [PipelinePropagation(PropagationDirections.Outbound)]
        Task DeregisterAsync(IChannelHandlerContext context);

        [PipelinePropagation(PropagationDirections.Outbound)]
        void Read(IChannelHandlerContext context);

        [PipelinePropagation(PropagationDirections.Inbound)]
        void UserEventTriggered(IChannelHandlerContext context, object evt);
    }
}