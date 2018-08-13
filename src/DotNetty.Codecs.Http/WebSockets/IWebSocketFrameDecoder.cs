// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using DotNetty.Transport.Channels;

    /// <summary>
    ///     Marker interface which all WebSocketFrame decoders need to implement. This makes it 
    ///     easier to access the added encoder later in the <see cref="IChannelPipeline"/>
    /// </summary>
    public interface IWebSocketFrameDecoder : IChannelHandler
    {
    }
}
