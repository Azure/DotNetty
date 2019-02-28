// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit;

    using HttpVersion = DotNetty.Codecs.Http.HttpVersion;

    public sealed class HttpClientCodecTest
    {
        const string EmptyResponse = "HTTP/1.0 200 OK\r\nContent-Length: 0\r\n\r\n";

        const string Response = "HTTP/1.0 200 OK\r\n"
            + "Date: Fri, 31 Dec 1999 23:59:59 GMT\r\n"
            + "Content-Type: text/html\r\n" + "Content-Length: 28\r\n" + "\r\n"
            + "<html><body></body></html>\r\n";

        const string IncompleteChunkedResponse = "HTTP/1.1 200 OK\r\n"
            + "Content-Type: text/plain\r\n"
            + "Transfer-Encoding: chunked\r\n" + "\r\n"
            + "5\r\n" + "first\r\n" + "6\r\n" + "second\r\n" + "0\r\n";

        const string ChunkedResponse = IncompleteChunkedResponse + "\r\n";

        [Fact]
        public void ConnectWithResponseContent()
        {
            var codec = new HttpClientCodec(4096, 8192, 8192, true);
            var ch = new EmbeddedChannel(codec);

            SendRequestAndReadResponse(ch, HttpMethod.Connect, Response);
            ch.Finish();
        }

        [Fact]
        public void FailsNotOnRequestResponseChunked()
        {
            var codec = new HttpClientCodec(4096, 8192, 8192, true);
            var ch = new EmbeddedChannel(codec);

            SendRequestAndReadResponse(ch, HttpMethod.Get, ChunkedResponse);
            ch.Finish();
        }

        [Fact]
        public void FailsOnMissingResponse()
        {
            var codec = new HttpClientCodec(4096, 8192, 8192, true);
            var ch = new EmbeddedChannel(codec);

            Assert.True(ch.WriteOutbound(new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "http://localhost/")));
            var buffer = ch.ReadOutbound<IByteBuffer>();
            Assert.NotNull(buffer);
            buffer.Release();

            Assert.Throws<PrematureChannelClosureException>(() => ch.Finish());
        }

        [Fact]
        public void FailsOnIncompleteChunkedResponse()
        {
            var codec = new HttpClientCodec(4096, 8192, 8192, true);
            var ch = new EmbeddedChannel(codec);

            ch.WriteOutbound(new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "http://localhost/"));
            var buffer = ch.ReadOutbound<IByteBuffer>();
            Assert.NotNull(buffer);
            buffer.Release();
            Assert.Null(ch.ReadInbound<IByteBuffer>());
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(IncompleteChunkedResponse)));
            var response = ch.ReadInbound<IHttpResponse>();
            Assert.NotNull(response);
            var content = ch.ReadInbound<IHttpContent>();
            Assert.NotNull(content); // Chunk 'first'
            content.Release();

            content = ch.ReadInbound<IHttpContent>();
            Assert.NotNull(content); // Chunk 'second'
            content.Release();

            content = ch.ReadInbound<IHttpContent>();
            Assert.Null(content);

            Assert.Throws<PrematureChannelClosureException>(() => ch.Finish());
        }

        [Fact]
        public void ServerCloseSocketInputProvidesData()
        {
            var clientGroup = new MultithreadEventLoopGroup(1);
            var serverGroup = new MultithreadEventLoopGroup(1);
            try
            {
                var serverCompletion = new TaskCompletionSource();

                var serverHandler = new ServerHandler();
                ServerBootstrap sb = new ServerBootstrap()
                    .Group(serverGroup)
                    .Channel<TcpServerSocketChannel>()
                    .ChildHandler(
                        new ActionChannelInitializer<ISocketChannel>(
                            ch =>
                            {
                                // Don't use the HttpServerCodec, because we don't want to have content-length or anything added.
                                ch.Pipeline.AddLast(new HttpRequestDecoder(4096, 8192, 8192, true));
                                ch.Pipeline.AddLast(new HttpObjectAggregator(4096));
                                ch.Pipeline.AddLast(serverHandler);
                                serverCompletion.TryComplete();
                            }));

                var clientHandler = new ClientHandler();
                Bootstrap cb = new Bootstrap()
                    .Group(clientGroup)
                    .Channel<TcpSocketChannel>()
                    .Handler(
                        new ActionChannelInitializer<ISocketChannel>(
                            ch =>
                            {
                                ch.Pipeline.AddLast(new HttpClientCodec(4096, 8192, 8192, true));
                                ch.Pipeline.AddLast(new HttpObjectAggregator(4096));
                                ch.Pipeline.AddLast(clientHandler);
                            }));

                Task<IChannel> task = sb.BindAsync(IPAddress.Loopback, IPEndPoint.MinPort);
                task.Wait(TimeSpan.FromSeconds(5));
                Assert.True(task.Status == TaskStatus.RanToCompletion);
                IChannel serverChannel = task.Result;
                int port = ((IPEndPoint)serverChannel.LocalAddress).Port;

                task = cb.ConnectAsync(IPAddress.Loopback, port);
                task.Wait(TimeSpan.FromSeconds(5));
                Assert.True(task.Status == TaskStatus.RanToCompletion);
                IChannel clientChannel = task.Result;

                serverCompletion.Task.Wait(TimeSpan.FromSeconds(5));
                clientChannel.WriteAndFlushAsync(new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/")).Wait(TimeSpan.FromSeconds(1));
                Assert.True(serverHandler.WaitForCompletion());
                Assert.True(clientHandler.WaitForCompletion());
            }
            finally
            {
                Task.WaitAll(
                    clientGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
                    serverGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)));
            }
        }

        class ClientHandler : SimpleChannelInboundHandler<IFullHttpResponse>
        {
            readonly TaskCompletionSource completion = new TaskCompletionSource();

            public bool WaitForCompletion()
            {
                this.completion.Task.Wait(TimeSpan.FromSeconds(5));
                return this.completion.Task.Status == TaskStatus.RanToCompletion;
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, IFullHttpResponse msg) =>
                this.completion.TryComplete();

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) =>
                this.completion.TrySetException(exception);
        }

        class ServerHandler : SimpleChannelInboundHandler<IFullHttpRequest>
        {
            readonly TaskCompletionSource completion = new TaskCompletionSource();

            public bool WaitForCompletion()
            {
                this.completion.Task.Wait(TimeSpan.FromSeconds(5));
                return this.completion.Task.Status == TaskStatus.RanToCompletion;
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, IFullHttpRequest msg)
            {
                // This is just a simple demo...don't block in IO
                Assert.IsAssignableFrom<ISocketChannel>(ctx.Channel);

                var sChannel = (ISocketChannel)ctx.Channel;
                /**
                 * The point of this test is to not add any content-length or content-encoding headers
                 * and the client should still handle this.
                 * See <a href="https://tools.ietf.org/html/rfc7230#section-3.3.3">RFC 7230, 3.3.3</a>.
                 */

                sChannel.WriteAndFlushAsync(Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes("HTTP/1.0 200 OK\r\n" + "Date: Fri, 31 Dec 1999 23:59:59 GMT\r\n" + "Content-Type: text/html\r\n\r\n")));
                sChannel.WriteAndFlushAsync(Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes("<html><body>hello half closed!</body></html>\r\n")));
                sChannel.CloseAsync();

                sChannel.CloseCompletion.LinkOutcome(this.completion);
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) => this.completion.TrySetException(exception);
        }

        [Fact]
        public void ContinueParsingAfterConnect() => AfterConnect(true);

        [Fact]
        public void PassThroughAfterConnect() => AfterConnect(false);

        static void AfterConnect(bool parseAfterConnect)
        {
            var ch = new EmbeddedChannel(new HttpClientCodec(4096, 8192, 8192, true, true, parseAfterConnect));
            var connectResponseConsumer = new Consumer();
            SendRequestAndReadResponse(ch, HttpMethod.Connect, EmptyResponse, connectResponseConsumer);

            Assert.True(connectResponseConsumer.ReceivedCount > 0, "No connect response messages received.");

            void Handler(object m)
            {
                if (parseAfterConnect)
                {
                    Assert.True(m is IHttpObject, "Unexpected response message type.");
                }
                else
                {
                    Assert.False(m is IHttpObject, "Unexpected response message type.");
                }
            }

            var responseConsumer = new Consumer(Handler);

            SendRequestAndReadResponse(ch, HttpMethod.Get, Response, responseConsumer);
            Assert.True(responseConsumer.ReceivedCount > 0, "No response messages received.");
            Assert.False(ch.Finish(), "Channel finish failed.");
        }

        static void SendRequestAndReadResponse(EmbeddedChannel ch, HttpMethod httpMethod, string response) =>
            SendRequestAndReadResponse(ch, httpMethod, response, new Consumer());

        static void SendRequestAndReadResponse(
            EmbeddedChannel ch,
            HttpMethod httpMethod,
            string response,
            Consumer responseConsumer)
        {
            Assert.True(
                ch.WriteOutbound(new DefaultFullHttpRequest(HttpVersion.Http11, httpMethod, "http://localhost/")),
                "Channel outbound write failed.");
            Assert.True(
                ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(response))),
                "Channel inbound write failed.");

            for (;;)
            {
                var msg = ch.ReadOutbound<object>();
                if (msg == null)
                {
                    break;
                }
                ReferenceCountUtil.Release(msg);
            }
            for (;;)
            {
                var msg = ch.ReadInbound<object>();
                if (msg == null)
                {
                    break;
                }
                responseConsumer.OnResponse(msg);
                ReferenceCountUtil.Release(msg);
            }
        }

        sealed class Consumer
        {
            readonly Action<object> handler;

            public Consumer(Action<object> handler = null)
            {
                this.handler = handler;
            }

            public void OnResponse(object response)
            {
                this.ReceivedCount++;
                this.Accept(response);
            }

            void Accept(object response)
            {
                this.handler?.Invoke(response);
            }

            public int ReceivedCount { get; private set; }
        }

        [Fact]
        public void DecodesFinalResponseAfterSwitchingProtocols()
        {
            const string SwitchingProtocolsResponse = "HTTP/1.1 101 Switching Protocols\r\n" +
                "Connection: Upgrade\r\n" +
                "Upgrade: TLS/1.2, HTTP/1.1\r\n\r\n";

            var codec = new HttpClientCodec(4096, 8192, 8192, true);
            var ch = new EmbeddedChannel(codec, new HttpObjectAggregator(1024));

            IHttpRequest request = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "http://localhost/");
            request.Headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.Upgrade);
            request.Headers.Set(HttpHeaderNames.Upgrade, "TLS/1.2");
            Assert.True(ch.WriteOutbound(request), "Channel outbound write failed.");

            Assert.True(
                ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(SwitchingProtocolsResponse))),
                "Channel inbound write failed.");
            var switchingProtocolsResponse = ch.ReadInbound<IFullHttpResponse>();
            Assert.NotNull(switchingProtocolsResponse);
            switchingProtocolsResponse.Release();

            Assert.True(
                ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(Response))),
                "Channel inbound write failed");
            var finalResponse = ch.ReadInbound<IFullHttpResponse>();
            Assert.NotNull(finalResponse);
            finalResponse.Release();
            Assert.True(ch.FinishAndReleaseAll(), "Channel finish failed");
        }

        [Fact]
        public void WebSocket00Response()
        {
            byte[] data = Encoding.UTF8.GetBytes("HTTP/1.1 101 WebSocket Protocol Handshake\r\n" +
                "Upgrade: WebSocket\r\n" +
                "Connection: Upgrade\r\n" +
                "Sec-WebSocket-Origin: http://localhost:8080\r\n" +
                "Sec-WebSocket-Location: ws://localhost/some/path\r\n" +
                "\r\n" +
                "1234567812345678");
            var ch = new EmbeddedChannel(new HttpClientCodec());
            Assert.True(ch.WriteInbound(Unpooled.WrappedBuffer(data)));

            var res = ch.ReadInbound<IHttpResponse>();
            Assert.Same(HttpVersion.Http11, res.ProtocolVersion);
            Assert.Equal(HttpResponseStatus.SwitchingProtocols, res.Status);
            var content = ch.ReadInbound<IHttpContent>();
            Assert.Equal(16, content.Content.ReadableBytes);
            content.Release();

            Assert.False(ch.Finish());
            var next = ch.ReadInbound<object>();
            Assert.Null(next);
        }
    }
}
