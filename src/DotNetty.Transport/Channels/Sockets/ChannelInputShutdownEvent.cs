// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    /// <summary>
    /// Special event which will be fired and passed to the <see cref="IChannelHandler.UserEventTriggered(IChannelHandlerContext,object)"/>
    /// methods once the input of an <see cref="ISocketChannel"/> was shutdown and the
    /// <see cref="ISocketChannelConfiguration.AllowHalfClosure"/> property returns <c>true</c>.
    /// </summary>
    public sealed class ChannelInputShutdownEvent
    {
        /// <summary>
        /// Singleton instance to use.
        /// </summary>
        public static readonly ChannelInputShutdownEvent Instance = new ChannelInputShutdownEvent();

        ChannelInputShutdownEvent()
        {
        }
    }
}