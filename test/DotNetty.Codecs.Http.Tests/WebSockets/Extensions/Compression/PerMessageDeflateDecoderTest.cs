// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets.Extensions.Compression
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Compression;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Codecs.Http.WebSockets.Extensions;
    using DotNetty.Codecs.Http.WebSockets.Extensions.Compression;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class PerMessageDeflateDecoderTest
    {
        readonly Random random;

        public PerMessageDeflateDecoderTest()
        {
            this.random = new Random();
        }

        [Fact]
        public void CompressedFrame()
        {
            var encoderChannel = new EmbeddedChannel(
                ZlibCodecFactory.NewZlibEncoder(ZlibWrapper.None, 9, 15, 8));
            var decoderChannel = new EmbeddedChannel(new PerMessageDeflateDecoder(false));

            var payload = new byte[300];
            this.random.NextBytes(payload);

            encoderChannel.WriteOutbound(Unpooled.WrappedBuffer(payload));
            var compressedPayload = encoderChannel.ReadOutbound<IByteBuffer>();

            var compressedFrame = new BinaryWebSocketFrame(true,
                WebSocketRsv.Rsv1 | WebSocketRsv.Rsv3,
                compressedPayload.Slice(0, compressedPayload.ReadableBytes - 4));

            decoderChannel.WriteInbound(compressedFrame);
            var uncompressedFrame = decoderChannel.ReadInbound<BinaryWebSocketFrame>();

            Assert.NotNull(uncompressedFrame);
            Assert.NotNull(uncompressedFrame.Content);
            Assert.IsType<BinaryWebSocketFrame>(uncompressedFrame);
            Assert.Equal(WebSocketRsv.Rsv3, uncompressedFrame.Rsv);
            Assert.Equal(300, uncompressedFrame.Content.ReadableBytes);

            var finalPayload = new byte[300];
            uncompressedFrame.Content.ReadBytes(finalPayload);
            Assert.Equal(payload, finalPayload);
            uncompressedFrame.Release();
        }

        [Fact]
        public void NormalFrame()
        {
            var decoderChannel = new EmbeddedChannel(new PerMessageDeflateDecoder(false));

            var payload = new byte[300];
            this.random.NextBytes(payload);

            var frame = new BinaryWebSocketFrame(true,
                WebSocketRsv.Rsv3, Unpooled.WrappedBuffer(payload));

            decoderChannel.WriteInbound(frame);
            var newFrame = decoderChannel.ReadInbound<BinaryWebSocketFrame>();

            Assert.NotNull(newFrame);
            Assert.NotNull(newFrame.Content);
            Assert.IsType<BinaryWebSocketFrame>(newFrame);
            Assert.Equal(WebSocketRsv.Rsv3, newFrame.Rsv);
            Assert.Equal(300, newFrame.Content.ReadableBytes);

            var finalPayload = new byte[300];
            newFrame.Content.ReadBytes(finalPayload);
            Assert.Equal(payload, finalPayload);
            newFrame.Release();
        }

        [Fact]
        public void FramementedFrame()
        {
            var encoderChannel = new EmbeddedChannel(
                    ZlibCodecFactory.NewZlibEncoder(ZlibWrapper.None, 9, 15, 8));
            var decoderChannel = new EmbeddedChannel(new PerMessageDeflateDecoder(false));

            var payload = new byte[300];
            this.random.NextBytes(payload);

            encoderChannel.WriteOutbound(Unpooled.WrappedBuffer(payload));
            var compressedPayload = encoderChannel.ReadOutbound<IByteBuffer>();
            compressedPayload = compressedPayload.Slice(0, compressedPayload.ReadableBytes - 4);

            int oneThird = compressedPayload.ReadableBytes / 3;
            var compressedFrame1 = new BinaryWebSocketFrame(false,
                    WebSocketRsv.Rsv1 | WebSocketRsv.Rsv3,
                    compressedPayload.Slice(0, oneThird));
            var compressedFrame2 = new ContinuationWebSocketFrame(false,
                    WebSocketRsv.Rsv3, compressedPayload.Slice(oneThird, oneThird));
            var compressedFrame3 = new ContinuationWebSocketFrame(true,
                    WebSocketRsv.Rsv3, compressedPayload.Slice(oneThird * 2,
                            compressedPayload.ReadableBytes - oneThird * 2));

            decoderChannel.WriteInbound(compressedFrame1.Retain());
            decoderChannel.WriteInbound(compressedFrame2.Retain());
            decoderChannel.WriteInbound(compressedFrame3);
            var uncompressedFrame1 = decoderChannel.ReadInbound<BinaryWebSocketFrame>();
            var uncompressedFrame2 = decoderChannel.ReadInbound<ContinuationWebSocketFrame>();
            var uncompressedFrame3 = decoderChannel.ReadInbound<ContinuationWebSocketFrame>();

            Assert.NotNull(uncompressedFrame1);
            Assert.NotNull(uncompressedFrame2);
            Assert.NotNull(uncompressedFrame3);
            Assert.Equal(WebSocketRsv.Rsv3, uncompressedFrame1.Rsv);
            Assert.Equal(WebSocketRsv.Rsv3, uncompressedFrame2.Rsv);
            Assert.Equal(WebSocketRsv.Rsv3, uncompressedFrame3.Rsv);

            IByteBuffer finalPayloadWrapped = Unpooled.WrappedBuffer(uncompressedFrame1.Content,
                    uncompressedFrame2.Content, uncompressedFrame3.Content);
            Assert.Equal(300, finalPayloadWrapped.ReadableBytes);

            var finalPayload = new byte[300];
            finalPayloadWrapped.ReadBytes(finalPayload);
            Assert.Equal(payload, finalPayload);
            finalPayloadWrapped.Release();
        }

        [Fact]
        public void MultiCompressedPayloadWithinFrame()
        {
            var encoderChannel = new EmbeddedChannel(
                ZlibCodecFactory.NewZlibEncoder(ZlibWrapper.None, 9, 15, 8));
            var decoderChannel = new EmbeddedChannel(new PerMessageDeflateDecoder(false));

            var payload1 = new byte[100];
            this.random.NextBytes(payload1);
            var payload2 = new byte[100];
            this.random.NextBytes(payload2);

            encoderChannel.WriteOutbound(Unpooled.WrappedBuffer(payload1));
            var compressedPayload1 = encoderChannel.ReadOutbound<IByteBuffer>();
            encoderChannel.WriteOutbound(Unpooled.WrappedBuffer(payload2));
            var compressedPayload2 = encoderChannel.ReadOutbound<IByteBuffer>();

            var compressedFrame = new BinaryWebSocketFrame(true,
                WebSocketRsv.Rsv1 | WebSocketRsv.Rsv3,
                Unpooled.WrappedBuffer(
                    compressedPayload1,
                    compressedPayload2.Slice(0, compressedPayload2.ReadableBytes - 4)));

            decoderChannel.WriteInbound(compressedFrame);
            var uncompressedFrame = decoderChannel.ReadInbound<BinaryWebSocketFrame>();

            Assert.NotNull(uncompressedFrame);
            Assert.NotNull(uncompressedFrame.Content);
            Assert.IsType<BinaryWebSocketFrame>(uncompressedFrame);
            Assert.Equal(WebSocketRsv.Rsv3, uncompressedFrame.Rsv);
            Assert.Equal(200, uncompressedFrame.Content.ReadableBytes);

            var finalPayload1 = new byte[100];
            uncompressedFrame.Content.ReadBytes(finalPayload1);
            Assert.Equal(payload1, finalPayload1);
            var finalPayload2 = new byte[100];
            uncompressedFrame.Content.ReadBytes(finalPayload2);
            Assert.Equal(payload2, finalPayload2);
            uncompressedFrame.Release();
        }
    }
}
