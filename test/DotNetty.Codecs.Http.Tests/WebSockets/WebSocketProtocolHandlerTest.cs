// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class WebSocketProtocolHandlerTest
    {
        [Fact]
        public void PingFrame()
        {
            IByteBuffer pingData = Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("Hello, world"));
            var channel = new EmbeddedChannel(new Handler());

            var inputMessage = new PingWebSocketFrame(pingData);
            Assert.False(channel.WriteInbound(inputMessage)); // the message was not propagated inbound

            // a Pong frame was written to the channel
            var response = channel.ReadOutbound<PongWebSocketFrame>();
            Assert.Equal(pingData, response.Content);

            pingData.Release();
            Assert.False(channel.Finish());
        }

        [Fact]
        public void PongFrameDropFrameFalse()
        {
            var channel = new EmbeddedChannel(new Handler(false));

            var pingResponse = new PongWebSocketFrame();
            Assert.True(channel.WriteInbound(pingResponse));

            AssertPropagatedInbound(pingResponse, channel);

            pingResponse.Release();
            Assert.False(channel.Finish());
        }

        [Fact]
        public void PongFrameDropFrameTrue()
        {
            var channel = new EmbeddedChannel(new Handler());

            var pingResponse = new PongWebSocketFrame();
            Assert.False(channel.WriteInbound(pingResponse)); // message was not propagated inbound
        }

        [Fact]
        public void TextFrame()
        {
            var channel = new EmbeddedChannel(new Handler());

            var textFrame = new TextWebSocketFrame();
            Assert.True(channel.WriteInbound(textFrame));

            AssertPropagatedInbound(textFrame, channel);

            textFrame.Release();
            Assert.False(channel.Finish());
        }

        static void AssertPropagatedInbound<T>(T message, EmbeddedChannel channel) 
            where T : WebSocketFrame
        {
            var propagatedResponse = channel.ReadInbound<T>();
            Assert.Equal(message, propagatedResponse);
        }

        sealed class Handler : WebSocketProtocolHandler
        {
            public Handler(bool dropPongFrames = true) : base(dropPongFrames)
            {
            }
        }
    }
}
