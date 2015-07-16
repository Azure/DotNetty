// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    /// <summary>
    /// Special event which will be fired and passed to the
    /// {@link ChannelHandler#userEventTriggered(ChannelHandlerContext, Object)} methods once the input of
    /// a {@link SocketChannel} was shutdown and the {@link SocketChannelConfig#isAllowHalfClosure()} method returns
    /// {@code true}.
    /// </summary>
    public sealed class ChannelInputShutdownEvent
    {
        /// <summary>
        /// Instance to use
        /// </summary>
        public static readonly ChannelInputShutdownEvent Instance = new ChannelInputShutdownEvent();

        ChannelInputShutdownEvent()
        {
        }
    }
}