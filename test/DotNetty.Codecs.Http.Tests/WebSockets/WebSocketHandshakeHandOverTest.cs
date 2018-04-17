// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class WebSocketHandshakeHandOverTest
    {
        bool serverReceivedHandshake;
        WebSocketServerProtocolHandler.HandshakeComplete serverHandshakeComplete;
        bool clientReceivedHandshake;
        bool clientReceivedMessage;

        public WebSocketHandshakeHandOverTest()
        {
            this.serverReceivedHandshake = false;
            this.serverHandshakeComplete = null;
            this.clientReceivedHandshake = false;
            this.clientReceivedMessage = false;
        }

        [Fact]
        public void Handover()
        {
            var serverHandler = new ServerHandler(this);
            EmbeddedChannel serverChannel = CreateServerChannel(serverHandler);
            EmbeddedChannel clientChannel = CreateClientChannel(new ClientHandler(this));

            // Transfer the handshake from the client to the server
            TransferAllDataWithMerge(clientChannel, serverChannel);
            Assert.True(serverHandler.Completion.Wait(TimeSpan.FromSeconds(1)));

            Assert.True(this.serverReceivedHandshake);
            Assert.NotNull(this.serverHandshakeComplete);
            Assert.Equal("/test", this.serverHandshakeComplete.RequestUri);
            Assert.Equal(8, this.serverHandshakeComplete.RequestHeaders.Size);
            Assert.Equal("test-proto-2", this.serverHandshakeComplete.SelectedSubprotocol);

            // Transfer the handshake response and the websocket message to the client
            TransferAllDataWithMerge(serverChannel, clientChannel);
            Assert.True(this.clientReceivedHandshake);
            Assert.True(this.clientReceivedMessage);
        }

        sealed class ServerHandler : SimpleChannelInboundHandler<object>
        {
            readonly WebSocketHandshakeHandOverTest owner;
            readonly TaskCompletionSource completion;

            public ServerHandler(WebSocketHandshakeHandOverTest owner)
            {
                this.owner = owner;
                this.completion = new TaskCompletionSource();
            }

            public override void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
                if (evt is WebSocketServerProtocolHandler.HandshakeComplete complete)
                {
                    this.owner.serverReceivedHandshake = true;
                    this.owner.serverHandshakeComplete = complete;

                    // immediately send a message to the client on connect
                    context.WriteAndFlushAsync(new TextWebSocketFrame("abc"))
                        .LinkOutcome(this.completion);
                }
            }

            public Task Completion => this.completion.Task;

            protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
            {
                // Empty
            }
        }

        sealed class ClientHandler : SimpleChannelInboundHandler<object>
        {
            readonly WebSocketHandshakeHandOverTest owner;

            public ClientHandler(WebSocketHandshakeHandOverTest owner)
            {
                this.owner = owner;
            }

            public override void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
                if (evt is WebSocketClientProtocolHandler.ClientHandshakeStateEvent stateEvent 
                    && stateEvent == WebSocketClientProtocolHandler.ClientHandshakeStateEvent.HandshakeComplete)
                {
                    this.owner.clientReceivedHandshake = true;
                }
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
            {
                if (msg is TextWebSocketFrame)
                {
                    this.owner.clientReceivedMessage = true;
                }
            }
        }

        static void TransferAllDataWithMerge(EmbeddedChannel srcChannel, EmbeddedChannel dstChannel)
        {
            IByteBuffer mergedBuffer = null;
            for (;;)
            {
                var srcData = srcChannel.ReadOutbound<object>();
                if (srcData != null)
                {
                    Assert.IsAssignableFrom<IByteBuffer>(srcData);
                    var srcBuf = (IByteBuffer)srcData;
                    try
                    {
                        if (mergedBuffer == null)
                        {
                            mergedBuffer = Unpooled.Buffer();
                        }
                        mergedBuffer.WriteBytes(srcBuf);
                    }
                    finally
                    {
                        srcBuf.Release();
                    }
                }
                else
                {
                    break;
                }
            }

            if (mergedBuffer != null)
            {
                dstChannel.WriteInbound(mergedBuffer);
            }
        }

        static EmbeddedChannel CreateClientChannel(IChannelHandler handler) => new EmbeddedChannel(
            new HttpClientCodec(),
            new HttpObjectAggregator(8192),
            new WebSocketClientProtocolHandler(
                new Uri("ws://localhost:1234/test"),
                WebSocketVersion.V13,
                "test-proto-2",
                false,
                null,
                65536),
            handler);

        static EmbeddedChannel CreateServerChannel(IChannelHandler handler) => new EmbeddedChannel(
            new HttpServerCodec(),
            new HttpObjectAggregator(8192),
            new WebSocketServerProtocolHandler("/test", "test-proto-1, test-proto-2", false),
            handler);
    }
}
