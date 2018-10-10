// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class WebSocketServerExtensionHandler : ChannelHandlerAdapter
    {
        readonly List<IWebSocketServerExtensionHandshaker> extensionHandshakers;

        List<IWebSocketServerExtension> validExtensions;
        Action<Task, object> upgradeCompletedContinuation;

        public WebSocketServerExtensionHandler(params IWebSocketServerExtensionHandshaker[] extensionHandshakers)
        {
            Contract.Requires(extensionHandshakers != null && extensionHandshakers.Length > 0);
            this.extensionHandshakers = new List<IWebSocketServerExtensionHandshaker>(extensionHandshakers);
            this.upgradeCompletedContinuation = this.OnUpgradeCompleted;
        }

        public override void ChannelRead(IChannelHandlerContext ctx, object msg)
        {
            if (msg is IHttpRequest request)
            {
                if (WebSocketExtensionUtil.IsWebsocketUpgrade(request.Headers))
                {
                    if (request.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value)
                        && value != null)
                    {
                        string extensionsHeader = value.ToString();
                        List<WebSocketExtensionData> extensions =
                            WebSocketExtensionUtil.ExtractExtensions(extensionsHeader);
                        int rsv = 0;

                        foreach (WebSocketExtensionData extensionData in extensions)
                        {
                            IWebSocketServerExtension validExtension = null;
                            foreach (IWebSocketServerExtensionHandshaker extensionHandshaker in this.extensionHandshakers)
                            {
                                validExtension = extensionHandshaker.HandshakeExtension(extensionData);
                                if (validExtension != null)
                                {
                                    break;
                                }
                            }

                            if (validExtension != null && (validExtension.Rsv & rsv) == 0)
                            {
                                if (this.validExtensions == null)
                                {
                                    this.validExtensions = new List<IWebSocketServerExtension>(1);
                                }

                                rsv = rsv | validExtension.Rsv;
                                this.validExtensions.Add(validExtension);
                            }
                        }
                    }
                }
            }

            base.ChannelRead(ctx, msg);
        }

        public override ValueTask WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            HttpHeaders responseHeaders;
            string headerValue = null;
            
            if (msg is IHttpResponse response 
                && WebSocketExtensionUtil.IsWebsocketUpgrade(responseHeaders = response.Headers) 
                && this.validExtensions != null)
            {
                if (responseHeaders.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value))
                {
                    headerValue = value?.ToString();
                }

                foreach (IWebSocketServerExtension extension in this.validExtensions)
                {
                    WebSocketExtensionData extensionData = extension.NewReponseData();
                    headerValue = WebSocketExtensionUtil.AppendExtension(headerValue,
                        extensionData.Name, extensionData.Parameters);
                }

                if (headerValue != null)
                {
                    responseHeaders.Set(HttpHeaderNames.SecWebsocketExtensions, headerValue);
                }

                Task task = base.WriteAsync(ctx, msg).AsTask();
                task.ContinueWith(this.upgradeCompletedContinuation, ctx, TaskContinuationOptions.ExecuteSynchronously);
                return new ValueTask(task);
            }

            return base.WriteAsync(ctx, msg);
        }

        void OnUpgradeCompleted(Task task, object state)
        {
            var ctx = (IChannelHandlerContext)state;
            if (task.Status == TaskStatus.RanToCompletion)
            {
                foreach (IWebSocketServerExtension extension in this.validExtensions)
                {
                    WebSocketExtensionDecoder decoder = extension.NewExtensionDecoder();
                    WebSocketExtensionEncoder encoder = extension.NewExtensionEncoder();
                    ctx.Channel.Pipeline.AddAfter(ctx.Name, decoder.GetType().Name, decoder);
                    ctx.Channel.Pipeline.AddAfter(ctx.Name, encoder.GetType().Name, encoder);
                }
            }
            ctx.Channel.Pipeline.Remove(ctx.Name);
        }
    }
}
