// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Tests.Protobuf
{
    using System;
    using System.Linq;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Protobuf;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class ProtobufVarint32FrameDecoderTest
    {
        [Fact]
        public void TinyDecode()
        {
            byte[] bytes = { 4, 1, 1, 1, 1 };

            IByteBuffer content = Unpooled.WrappedBuffer(bytes, 0, 1);
            var channel = new EmbeddedChannel(new ProtobufVarint32FrameDecoder());
            Assert.False(channel.WriteInbound(content));
            var written = channel.ReadInbound<IByteBuffer>();
            Assert.Null(written);

            content = Unpooled.WrappedBuffer(bytes, 1, 2);
            Assert.False(channel.WriteInbound(content));
            written = channel.ReadInbound<IByteBuffer>();
            Assert.Null(written);

            content = Unpooled.WrappedBuffer(bytes, 3, bytes.Length - 3);
            Assert.True(channel.WriteInbound(content));
            written = channel.ReadInbound<IByteBuffer>();
            Assert.NotNull(written);
            byte[] output = TestUtil.GetReadableBytes(written);
            var expected = new byte[] { 1, 1, 1, 1 };

            Assert.Equal(expected.Length, output.Length);
            Assert.True(output.SequenceEqual(expected));
            written.Release();

            Assert.False(channel.Finish());
        }

        [Fact]
        public void RegularDecode()
        {
            var bytes = new byte[2048];
            for (int i = 2; i < 2048; i++)
            {
                bytes[i] = 1;
            }
            bytes[0] = -2 + 256;
            bytes[1] = 15;

            var channel = new EmbeddedChannel(new ProtobufVarint32FrameDecoder());

            IByteBuffer content = Unpooled.WrappedBuffer(bytes, 0, 1);
            Assert.False(channel.WriteInbound(content));
            var written = channel.ReadInbound<IByteBuffer>();
            Assert.Null(written);

            content = Unpooled.WrappedBuffer(bytes, 1, 127);
            Assert.False(channel.WriteInbound(content));
            written = channel.ReadInbound<IByteBuffer>();
            Assert.Null(written);

            content = Unpooled.WrappedBuffer(bytes, 127, 600);
            Assert.False(channel.WriteInbound(content));
            written = channel.ReadInbound<IByteBuffer>();
            Assert.Null(written);

            content = Unpooled.WrappedBuffer(bytes, 727, bytes.Length - 727);
            Assert.True(channel.WriteInbound(content));
            written = channel.ReadInbound<IByteBuffer>();
            byte[] output = TestUtil.GetReadableBytes(written);
            var expected = new byte[bytes.Length - 2];
            Buffer.BlockCopy(bytes, 2, expected, 0, bytes.Length - 2);

            Assert.Equal(expected.Length, output.Length);
            Assert.True(output.SequenceEqual(expected));
            written.Release();

            Assert.False(channel.Finish());
        }
    }
}
