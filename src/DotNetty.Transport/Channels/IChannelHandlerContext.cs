// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;

    public interface IChannelHandlerContext
    {
        IChannel Channel { get; }

        IByteBufferAllocator Allocator { get; }

        /// <summary>
        ///     Returns the {@link EventExecutor} which is used to execute an arbitrary task.
        /// </summary>
        IEventExecutor Executor { get; }

        /// <summary>
        ///     Returns the {@link IChannelHandlerInvoker} which is used to trigger an event for the associated
        ///     {@link IChannelHandler}.
        /// </summary>
        /// <remarks>
        ///     Note that the methods in {@link IChannelHandlerInvoker} are not intended to be called
        ///     by a user. Use this method only to obtain the reference to the {@link IChannelHandlerInvoker}
        ///     (and not calling its methods) unless you know what you are doing.
        /// </remarks>
        IChannelHandlerInvoker Invoker { get; }

        /// <summary>
        ///     The unique name of the {@link IChannelHandlerContext}.
        /// </summary>
        /// <remarks>
        ///     The name was used when the {@link IChannelHandler}
        ///     was added to the {@link IChannelPipeline}. This name can also be used to access the registered
        ///     {@link IChannelHandler} from the {@link IChannelPipeline}.
        /// </remarks>
        string Name { get; }

        IChannelHandler Handler { get; }

        bool Removed { get; }

        /// <summary>
        /// A {@link Channel} was registered to its {@link EventLoop}.
        ///
        /// This will result in having the {@link ChannelHandler#channelRegistered(ChannelHandlerContext)} method
        /// called of the next {@link ChannelHandler} contained in the {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        IChannelHandlerContext FireChannelRegistered();

        /// <summary>
        /// A {@link Channel} was unregistered from its {@link EventLoop}.
        ///
        /// This will result in having the {@link ChannelHandler#channelUnregistered(ChannelHandlerContext)} method
        /// called of the next {@link ChannelHandler} contained in the {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        IChannelHandlerContext FireChannelUnregistered();

        IChannelHandlerContext FireChannelActive();

        IChannelHandlerContext FireChannelInactive();

        IChannelHandlerContext FireChannelRead(object message);

        IChannelHandlerContext FireChannelReadComplete();

        IChannelHandlerContext FireChannelWritabilityChanged();

        IChannelHandlerContext FireExceptionCaught(Exception ex);

        IChannelHandlerContext FireUserEventTriggered(object evt);

        IChannelHandlerContext Read();

        Task WriteAsync(object message); // todo: optimize: add flag saying if handler is interested in task, do not produce task if it isn't needed

        IChannelHandlerContext Flush();

        Task WriteAndFlushAsync(object message);

        /// <summary>
        /// Request to bind to the given {@link SocketAddress} and notify the {@link ChannelFuture} once the operation
        /// completes, either because the operation was successful or because of an error.
        /// <p>
        /// This will result in having the
        /// {@link ChannelHandler#bind(ChannelHandlerContext, SocketAddress, ChannelPromise)} method
        /// called of the next {@link ChannelHandler} contained in the {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        Task BindAsync(EndPoint localAddress);

        /// <summary>
        /// Request to connect to the given {@link SocketAddress} and notify the {@link ChannelFuture} once the operation
        /// completes, either because the operation was successful or because of an error.
        /// <p>
        /// If the connection fails because of a connection timeout, the {@link ChannelFuture} will get failed with
        /// a {@link ConnectTimeoutException}. If it fails because of connection refused a {@link ConnectException}
        /// will be used.
        /// <p>
        /// This will result in having the
        /// {@link ChannelHandler#connect(ChannelHandlerContext, SocketAddress, SocketAddress, ChannelPromise)}
        /// method called of the next {@link ChannelHandler} contained in the {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        Task ConnectAsync(EndPoint remoteAddress);

        /// <summary>
        /// Request to connect to the given {@link SocketAddress} while bind to the localAddress and notify the
        /// {@link ChannelFuture} once the operation completes, either because the operation was successful or because of
        /// an error.
        /// <p>
        /// This will result in having the
        /// {@link ChannelHandler#connect(ChannelHandlerContext, SocketAddress, SocketAddress, ChannelPromise)}
        /// method called of the next {@link ChannelHandler} contained in the {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress);

        /// <summary>
        /// Request to disconnect from the remote peer and notify the {@link ChannelFuture} once the operation completes,
        /// either because the operation was successful or because of an error.
        /// <p>
        /// This will result in having the
        /// {@link ChannelHandler#disconnect(ChannelHandlerContext, ChannelPromise)}
        /// method called of the next {@link ChannelHandler} contained in the {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        Task DisconnectAsync();

        Task CloseAsync();

        /// <summary>
        /// Request to deregister from the previous assigned {@link EventExecutor} and notify the
        /// {@link ChannelFuture} once the operation completes, either because the operation was successful or because of
        /// an error.
        ///
        /// The given {@link ChannelPromise} will be notified.
        /// <p>
        /// This will result in having the
        /// {@link ChannelHandler#deregister(ChannelHandlerContext, ChannelPromise)}
        /// method called of the next {@link ChannelHandler} contained in the {@link ChannelPipeline} of the
        /// {@link Channel}.
        /// </summary>
        Task DeregisterAsync();
    }
}