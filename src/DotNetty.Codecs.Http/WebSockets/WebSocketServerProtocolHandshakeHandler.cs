// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Channels;

    using static HttpUtil;
    using static HttpMethod;
    using static HttpVersion;
    using static HttpResponseStatus;

    class WebSocketServerProtocolHandshakeHandler : ChannelHandlerAdapter
    {
        readonly string websocketPath;
        readonly string subprotocols;
        readonly bool allowExtensions;
        readonly int maxFramePayloadSize;
        readonly bool allowMaskMismatch;
        readonly bool checkStartsWith;

        internal WebSocketServerProtocolHandshakeHandler(string websocketPath, string subprotocols,
            bool allowExtensions, int maxFrameSize, bool allowMaskMismatch)
            : this(websocketPath, subprotocols, allowExtensions, maxFrameSize, allowMaskMismatch, false)
        {
        }

        internal WebSocketServerProtocolHandshakeHandler(string websocketPath, string subprotocols,
            bool allowExtensions, int maxFrameSize, bool allowMaskMismatch, bool checkStartsWith)
        {
            this.websocketPath = websocketPath;
            this.subprotocols = subprotocols;
            this.allowExtensions = allowExtensions;
            this.maxFramePayloadSize = maxFrameSize;
            this.allowMaskMismatch = allowMaskMismatch;
            this.checkStartsWith = checkStartsWith;
        }

        public override void ChannelRead(IChannelHandlerContext ctx, object msg)
        {
            var req = (IFullHttpRequest)msg;
            if (this.IsNotWebSocketPath(req))
            {
                ctx.FireChannelRead(msg);
                return;
            }

            try
            {
                if (!Equals(req.Method, Get))
                {
                    SendHttpResponse(ctx, req, new DefaultFullHttpResponse(Http11, Forbidden));
                    return;
                }

                var wsFactory = new WebSocketServerHandshakerFactory(
                    GetWebSocketLocation(ctx.Channel.Pipeline, req, this.websocketPath), this.subprotocols,
                    this.allowExtensions, this.maxFramePayloadSize, this.allowMaskMismatch);
                WebSocketServerHandshaker handshaker = wsFactory.NewHandshaker(req);
                if (handshaker == null)
                {
                    WebSocketServerHandshakerFactory.SendUnsupportedVersionResponse(ctx.Channel);
                }
                else
                {
                    Task task = handshaker.HandshakeAsync(ctx.Channel, req);
                    task.ContinueWith(t =>
                    {
                        if (t.Status != TaskStatus.RanToCompletion)
                        {
                            ctx.FireExceptionCaught(t.Exception);
                        }
                        else
                        {
                            ctx.FireUserEventTriggered(new WebSocketServerProtocolHandler.HandshakeComplete(
                                req.Uri, req.Headers, handshaker.SelectedSubprotocol));
                        }
                    }, 
                    TaskContinuationOptions.ExecuteSynchronously);

                    WebSocketServerProtocolHandler.SetHandshaker(ctx.Channel, handshaker);
                    ctx.Channel.Pipeline.Replace(this, "WS403Responder",
                        WebSocketServerProtocolHandler.ForbiddenHttpRequestResponder());
                }
            }
            finally
            {
                req.Release();
            }
        }

        bool IsNotWebSocketPath(IFullHttpRequest req) => this.checkStartsWith 
            ? !req.Uri.StartsWith(this.websocketPath) 
            : !req.Uri.Equals(this.websocketPath);

        static void SendHttpResponse(IChannelHandlerContext ctx, IHttpRequest req, IHttpResponse res)
        {
            Task task = ctx.Channel.WriteAndFlushAsync(res);
            if (!IsKeepAlive(req) || res.Status.Code != 200)
            {
                task.ContinueWith((t, c) => ((IChannel)c).CloseAsync(),
                    ctx.Channel, TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        static string GetWebSocketLocation(IChannelPipeline cp, IHttpRequest req, string path)
        {
            string protocol = "ws";
            if (cp.Get<TlsHandler>() != null)
            {
                // SSL in use so use Secure WebSockets
                protocol = "wss";
            }

            string host = null;
            if (req.Headers.TryGet(HttpHeaderNames.Host, out ICharSequence value))
            {
                host = value.ToString();
            }
            return $"{protocol}://{host}{path}";
        }
    }
}
