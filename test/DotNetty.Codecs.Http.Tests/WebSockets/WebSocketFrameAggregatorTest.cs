// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class WebSocketFrameAggregatorTest
    {
        readonly byte[] content1 = Encoding.UTF8.GetBytes("Content1");
        readonly byte[] content2 = Encoding.UTF8.GetBytes("Content2");
        readonly byte[] content3 = Encoding.UTF8.GetBytes("Content3");
        readonly byte[] aggregatedContent;

        public WebSocketFrameAggregatorTest()
        {
            this.aggregatedContent = new byte[this.content1.Length + this.content2.Length + this.content3.Length];
            Array.Copy(this.content1, 0, this.aggregatedContent, 0, this.content1.Length);
            Array.Copy(this.content2, 0, this.aggregatedContent, this.content1.Length, this.content2.Length);
            Array.Copy(this.content3, 0, this.aggregatedContent, this.content1.Length + this.content2.Length, this.content3.Length);
        }

        [Fact]
        public void AggregationBinary()
        {
            var channel = new EmbeddedChannel(new WebSocketFrameAggregator(int.MaxValue));
            channel.WriteInbound(new BinaryWebSocketFrame(true, 1, Unpooled.WrappedBuffer(this.content1)));
            channel.WriteInbound(new BinaryWebSocketFrame(false, 0, Unpooled.WrappedBuffer(this.content1)));
            channel.WriteInbound(new ContinuationWebSocketFrame(false, 0, Unpooled.WrappedBuffer(this.content2)));
            channel.WriteInbound(new PingWebSocketFrame(Unpooled.WrappedBuffer(this.content1)));
            channel.WriteInbound(new PongWebSocketFrame(Unpooled.WrappedBuffer(this.content1)));
            channel.WriteInbound(new ContinuationWebSocketFrame(true, 0, Unpooled.WrappedBuffer(this.content3)));

            Assert.True(channel.Finish());

            var frame = channel.ReadInbound<BinaryWebSocketFrame>();
            Assert.True(frame.IsFinalFragment);
            Assert.Equal(1, frame.Rsv);
            Assert.Equal(this.content1, ToBytes(frame.Content));

            var frame2 = channel.ReadInbound<PingWebSocketFrame>();
            Assert.True(frame2.IsFinalFragment);
            Assert.Equal(0, frame2.Rsv);
            Assert.Equal(this.content1, ToBytes(frame2.Content));

            var frame3 = channel.ReadInbound<PongWebSocketFrame>();
            Assert.True(frame3.IsFinalFragment);
            Assert.Equal(0, frame3.Rsv);
            Assert.Equal(this.content1, ToBytes(frame3.Content));

            var frame4 = channel.ReadInbound<BinaryWebSocketFrame>();
            Assert.True(frame4.IsFinalFragment);
            Assert.Equal(0, frame4.Rsv);
            Assert.Equal(this.aggregatedContent, ToBytes(frame4.Content));

            Assert.Null(channel.ReadInbound<object>());
        }

        [Fact]
        public void AggregationText()
        {
            var channel = new EmbeddedChannel(new WebSocketFrameAggregator(int.MaxValue));
            channel.WriteInbound(new TextWebSocketFrame(true, 1, Unpooled.WrappedBuffer(this.content1)));
            channel.WriteInbound(new TextWebSocketFrame(false, 0, Unpooled.WrappedBuffer(this.content1)));
            channel.WriteInbound(new ContinuationWebSocketFrame(false, 0, Unpooled.WrappedBuffer(this.content2)));
            channel.WriteInbound(new PingWebSocketFrame(Unpooled.WrappedBuffer(this.content1)));
            channel.WriteInbound(new PongWebSocketFrame(Unpooled.WrappedBuffer(this.content1)));
            channel.WriteInbound(new ContinuationWebSocketFrame(true, 0, Unpooled.WrappedBuffer(this.content3)));

            Assert.True(channel.Finish());

            var frame = channel.ReadInbound<TextWebSocketFrame>();
            Assert.True(frame.IsFinalFragment);
            Assert.Equal(1, frame.Rsv);
            Assert.Equal(this.content1, ToBytes(frame.Content));

            var frame2 = channel.ReadInbound<PingWebSocketFrame>();
            Assert.True(frame2.IsFinalFragment);
            Assert.Equal(0, frame2.Rsv);
            Assert.Equal(this.content1, ToBytes(frame2.Content));

            var frame3 = channel.ReadInbound<PongWebSocketFrame>();
            Assert.True(frame3.IsFinalFragment);
            Assert.Equal(0, frame3.Rsv);
            Assert.Equal(this.content1, ToBytes(frame3.Content));

            var frame4 = channel.ReadInbound<TextWebSocketFrame>();
            Assert.True(frame4.IsFinalFragment);
            Assert.Equal(0, frame4.Rsv);
            Assert.Equal(this.aggregatedContent, ToBytes(frame4.Content));

            Assert.Null(channel.ReadInbound<object>());
        }

        [Fact]
        public void TextFrameTooBig()
        {
            var channel = new EmbeddedChannel(new WebSocketFrameAggregator(8));
            channel.WriteInbound(new BinaryWebSocketFrame(true, 1, Unpooled.WrappedBuffer(this.content1)));
            channel.WriteInbound(new BinaryWebSocketFrame(false, 0, Unpooled.WrappedBuffer(this.content1)));
            Assert.Throws<TooLongFrameException>(() => 
                channel.WriteInbound(new ContinuationWebSocketFrame(false, 0, Unpooled.WrappedBuffer(this.content2))));

            channel.WriteInbound(new ContinuationWebSocketFrame(false, 0, Unpooled.WrappedBuffer(this.content2)));
            channel.WriteInbound(new ContinuationWebSocketFrame(true, 0, Unpooled.WrappedBuffer(this.content2)));

            channel.WriteInbound(new BinaryWebSocketFrame(true, 1, Unpooled.WrappedBuffer(this.content1)));
            channel.WriteInbound(new BinaryWebSocketFrame(false, 0, Unpooled.WrappedBuffer(this.content1)));
            Assert.Throws<TooLongFrameException>(() => 
                channel.WriteInbound(new ContinuationWebSocketFrame(false, 0, Unpooled.WrappedBuffer(this.content2))));

            channel.WriteInbound(new ContinuationWebSocketFrame(false, 0, Unpooled.WrappedBuffer(this.content2)));
            channel.WriteInbound(new ContinuationWebSocketFrame(true, 0, Unpooled.WrappedBuffer(this.content2)));
            for (;;)
            {
                var msg = channel.ReadInbound<object>();
                if (msg == null)
                {
                    break;
                }
                ReferenceCountUtil.Release(msg);
            }
            channel.Finish();
        }

        static byte[] ToBytes(IByteBuffer buf)
        {
            var bytes = new byte[buf.ReadableBytes];
            buf.ReadBytes(bytes);
            buf.Release();
            return bytes;
        }
    }
}
