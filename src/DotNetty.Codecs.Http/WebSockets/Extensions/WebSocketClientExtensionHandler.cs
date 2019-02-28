// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions
{
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class WebSocketClientExtensionHandler : ChannelHandlerAdapter
    {
        readonly List<IWebSocketClientExtensionHandshaker> extensionHandshakers;

        public WebSocketClientExtensionHandler(params IWebSocketClientExtensionHandshaker[] extensionHandshakers)
        {
            Contract.Requires(extensionHandshakers != null && extensionHandshakers.Length > 0);
            this.extensionHandshakers = new List<IWebSocketClientExtensionHandshaker>(extensionHandshakers);
        }

        public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            if (msg is IHttpRequest request && WebSocketExtensionUtil.IsWebsocketUpgrade(request.Headers))
            {
                string headerValue = null;
                if (request.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value))
                {
                    headerValue = value.ToString();
                }

                foreach (IWebSocketClientExtensionHandshaker extensionHandshaker in this.extensionHandshakers)
                {
                    WebSocketExtensionData extensionData = extensionHandshaker.NewRequestData();
                    headerValue = WebSocketExtensionUtil.AppendExtension(headerValue,
                        extensionData.Name, extensionData.Parameters);
                }

                request.Headers.Set(HttpHeaderNames.SecWebsocketExtensions, headerValue);
            }

            return base.WriteAsync(ctx, msg);
        }

        public override void ChannelRead(IChannelHandlerContext ctx, object msg)
        {
            if (msg is IHttpResponse response 
                && WebSocketExtensionUtil.IsWebsocketUpgrade(response.Headers))
            {
                string extensionsHeader = null;
                if (response.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value))
                {
                    extensionsHeader = value.ToString();
                }

                if (extensionsHeader != null)
                {
                    List<WebSocketExtensionData> extensions =
                        WebSocketExtensionUtil.ExtractExtensions(extensionsHeader);
                    var validExtensions = new List<IWebSocketClientExtension>(extensions.Count);
                    int rsv = 0;

                    foreach (WebSocketExtensionData extensionData in extensions)
                    {
                        IWebSocketClientExtension validExtension = null;
                        foreach (IWebSocketClientExtensionHandshaker extensionHandshaker in this.extensionHandshakers)
                        {
                            validExtension = extensionHandshaker.HandshakeExtension(extensionData);
                            if (validExtension != null)
                            {
                                break;
                            }
                        }

                        if (validExtension != null && (validExtension.Rsv & rsv) == 0)
                        {
                            rsv = rsv | validExtension.Rsv;
                            validExtensions.Add(validExtension);
                        }
                        else
                        {
                            throw new CodecException($"invalid WebSocket Extension handshake for \"{extensionsHeader}\"");
                        }
                    }

                    foreach (IWebSocketClientExtension validExtension in validExtensions)
                    {
                        WebSocketExtensionDecoder decoder = validExtension.NewExtensionDecoder();
                        WebSocketExtensionEncoder encoder = validExtension.NewExtensionEncoder();
                        ctx.Channel.Pipeline.AddAfter(ctx.Name, decoder.GetType().Name, decoder);
                        ctx.Channel.Pipeline.AddAfter(ctx.Name, encoder.GetType().Name, encoder);
                    }
                }

                ctx.Channel.Pipeline.Remove(ctx.Name);
            }

            base.ChannelRead(ctx, msg);
        }
    }
}
