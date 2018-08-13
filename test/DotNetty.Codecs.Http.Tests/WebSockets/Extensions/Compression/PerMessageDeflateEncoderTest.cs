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

    public sealed class PerMessageDeflateEncoderTest
    {
        readonly Random random;

        public PerMessageDeflateEncoderTest()
        {
            this.random = new Random();
        }

        [Fact]
        public void CompressedFrame()
        {
            var encoderChannel = new EmbeddedChannel(new PerMessageDeflateEncoder(9, 15, false));
            var decoderChannel = new EmbeddedChannel(
                ZlibCodecFactory.NewZlibDecoder(ZlibWrapper.None));

            var payload = new byte[300];
            this.random.NextBytes(payload);
            var frame = new BinaryWebSocketFrame(true,
                WebSocketRsv.Rsv3, Unpooled.WrappedBuffer(payload));

            encoderChannel.WriteOutbound(frame);
            var compressedFrame = encoderChannel.ReadOutbound<BinaryWebSocketFrame>();

            Assert.NotNull(compressedFrame);
            Assert.NotNull(compressedFrame.Content);
            Assert.IsType<BinaryWebSocketFrame>(compressedFrame);
            Assert.Equal(WebSocketRsv.Rsv1 | WebSocketRsv.Rsv3, compressedFrame.Rsv);

            decoderChannel.WriteInbound(compressedFrame.Content);
            decoderChannel.WriteInbound(DeflateDecoder.FrameTail);
            var uncompressedPayload = decoderChannel.ReadInbound<IByteBuffer>();
            Assert.Equal(300, uncompressedPayload.ReadableBytes);

            var finalPayload = new byte[300];
            uncompressedPayload.ReadBytes(finalPayload);
            Assert.Equal(payload, finalPayload);
            uncompressedPayload.Release();
        }

        [Fact]
        public void AlreadyCompressedFrame()
        {
            var encoderChannel = new EmbeddedChannel(new PerMessageDeflateEncoder(9, 15, false));

            var payload = new byte[300];
            this.random.NextBytes(payload);

            var frame = new BinaryWebSocketFrame(true,
                WebSocketRsv.Rsv3 | WebSocketRsv.Rsv1, Unpooled.WrappedBuffer(payload));

            encoderChannel.WriteOutbound(frame);
            var newFrame = encoderChannel.ReadOutbound<BinaryWebSocketFrame>();

            Assert.NotNull(newFrame);
            Assert.NotNull(newFrame.Content);
            Assert.IsType<BinaryWebSocketFrame>(newFrame);
            Assert.Equal(WebSocketRsv.Rsv3 | WebSocketRsv.Rsv1, newFrame.Rsv);
            Assert.Equal(300, newFrame.Content.ReadableBytes);

            var finalPayload = new byte[300];
            newFrame.Content.ReadBytes(finalPayload);
            Assert.Equal(payload, finalPayload);
            newFrame.Release();
        }

        [Fact]
        public void FramementedFrame()
        {
            var encoderChannel = new EmbeddedChannel(new PerMessageDeflateEncoder(9, 15, false));
            var decoderChannel = new EmbeddedChannel(
                    ZlibCodecFactory.NewZlibDecoder(ZlibWrapper.None));

            var payload1 = new byte[100];
            this.random.NextBytes(payload1);
            var payload2 = new byte[100];
            this.random.NextBytes(payload2);
            var payload3 = new byte[100];
            this.random.NextBytes(payload3);

            var frame1 = new BinaryWebSocketFrame(false,
                    WebSocketRsv.Rsv3, Unpooled.WrappedBuffer(payload1));
            var frame2 = new ContinuationWebSocketFrame(false,
                    WebSocketRsv.Rsv3, Unpooled.WrappedBuffer(payload2));
            var frame3 = new ContinuationWebSocketFrame(true,
                    WebSocketRsv.Rsv3, Unpooled.WrappedBuffer(payload3));

            encoderChannel.WriteOutbound(frame1);
            encoderChannel.WriteOutbound(frame2);
            encoderChannel.WriteOutbound(frame3);
            var compressedFrame1 = encoderChannel.ReadOutbound<BinaryWebSocketFrame>();
            var compressedFrame2 = encoderChannel.ReadOutbound<ContinuationWebSocketFrame>();
            var compressedFrame3 = encoderChannel.ReadOutbound<ContinuationWebSocketFrame>();

            Assert.NotNull(compressedFrame1);
            Assert.NotNull(compressedFrame2);
            Assert.NotNull(compressedFrame3);
            Assert.Equal(WebSocketRsv.Rsv1 | WebSocketRsv.Rsv3, compressedFrame1.Rsv);
            Assert.Equal(WebSocketRsv.Rsv3, compressedFrame2.Rsv);
            Assert.Equal(WebSocketRsv.Rsv3, compressedFrame3.Rsv);
            Assert.False(compressedFrame1.IsFinalFragment);
            Assert.False(compressedFrame2.IsFinalFragment);
            Assert.True(compressedFrame3.IsFinalFragment);

            decoderChannel.WriteInbound(compressedFrame1.Content);
            var uncompressedPayload1 = decoderChannel.ReadInbound<IByteBuffer>();
            var finalPayload1 = new byte[100];
            uncompressedPayload1.ReadBytes(finalPayload1);
            Assert.Equal(payload1, finalPayload1);
            uncompressedPayload1.Release();

            decoderChannel.WriteInbound(compressedFrame2.Content);
            var uncompressedPayload2 = decoderChannel.ReadInbound<IByteBuffer>();
            var finalPayload2 = new byte[100];
            uncompressedPayload2.ReadBytes(finalPayload2);
            Assert.Equal(payload2, finalPayload2);
            uncompressedPayload2.Release();

            decoderChannel.WriteInbound(compressedFrame3.Content);
            decoderChannel.WriteInbound(DeflateDecoder.FrameTail);
            var uncompressedPayload3 = decoderChannel.ReadInbound<IByteBuffer>();
            var finalPayload3 = new byte[100];
            uncompressedPayload3.ReadBytes(finalPayload3);
            Assert.Equal(payload3, finalPayload3);
            uncompressedPayload3.Release();
        }
    }
}
