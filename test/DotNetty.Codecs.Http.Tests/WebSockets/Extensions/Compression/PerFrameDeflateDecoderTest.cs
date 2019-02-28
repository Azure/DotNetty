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

    public sealed class PerFrameDeflateDecoderTest
    {
        readonly Random random;

        public PerFrameDeflateDecoderTest()
        {
            this.random = new Random();
        }

        [Fact]
        public void CompressedFrame()
        {
            var encoderChannel = new EmbeddedChannel(
                ZlibCodecFactory.NewZlibEncoder(ZlibWrapper.None, 9, 15, 8));
            var decoderChannel = new EmbeddedChannel(new PerFrameDeflateDecoder(false));

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
            var decoderChannel = new EmbeddedChannel(new PerFrameDeflateDecoder(false));

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

        // See https://github.com/netty/netty/issues/4348
        [Fact]
        public void CompressedEmptyFrame()
        {
            var encoderChannel = new EmbeddedChannel(
                ZlibCodecFactory.NewZlibEncoder(ZlibWrapper.None, 9, 15, 8));
            var decoderChannel = new EmbeddedChannel(new PerFrameDeflateDecoder(false));

            encoderChannel.WriteOutbound(Unpooled.Empty);
            var compressedPayload = encoderChannel.ReadOutbound<IByteBuffer>();
            var compressedFrame =
                new BinaryWebSocketFrame(true, WebSocketRsv.Rsv1 | WebSocketRsv.Rsv3, compressedPayload);

            decoderChannel.WriteInbound(compressedFrame);
            var uncompressedFrame = decoderChannel.ReadInbound<BinaryWebSocketFrame>();

            Assert.NotNull(uncompressedFrame);
            Assert.NotNull(uncompressedFrame.Content);
            Assert.IsType<BinaryWebSocketFrame>(uncompressedFrame);
            Assert.Equal(WebSocketRsv.Rsv3, uncompressedFrame.Rsv);
            Assert.Equal(0, uncompressedFrame.Content.ReadableBytes);
            uncompressedFrame.Release();
        }
    }
}
