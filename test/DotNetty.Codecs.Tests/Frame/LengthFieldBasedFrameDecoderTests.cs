// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Tests.Frame
{
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class LengthFieldBasedFrameDecoderTests
    {
        [Fact]
        public void FailSlowTooLongFrameRecovery()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new LengthFieldBasedFrameDecoder(5, 0, 4, 0, 4, false));
            for (int i = 0; i < 2; i++)
            {
                Assert.False(ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { 0, 0, 0, 2 })));
                Assert.Throws<TooLongFrameException>(() =>
                {
                    Assert.True(ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { 0, 0 })));
                    Assert.True(false, typeof(DecoderException).Name + " must be raised.");
                });
                ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { 0, 0, 0, 1, (byte)'A' }));
                IByteBuffer buf = ch.ReadInbound<IByteBuffer>();
                Encoding iso = Encoding.GetEncoding("ISO-8859-1");
                Assert.Equal("A", iso.GetString(buf.ToArray()));
                buf.Release();
            }
        }

        [Fact]
        public void TestFailFastTooLongFrameRecovery()
        {
            EmbeddedChannel ch = new EmbeddedChannel(
                new LengthFieldBasedFrameDecoder(5, 0, 4, 0, 4));

            for (int i = 0; i < 2; i++)
            {
                Assert.Throws<TooLongFrameException>(() =>
                {
                    Assert.True(ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { 0, 0, 0, 2 })));
                    Assert.True(false, typeof(DecoderException).Name + " must be raised.");
                });

                ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { 0, 0, 0, 0, 0, 1, (byte)'A' }));
                IByteBuffer buf = ch.ReadInbound<IByteBuffer>();
                Encoding iso = Encoding.GetEncoding("ISO-8859-1");
                Assert.Equal("A", iso.GetString(buf.ToArray()));
                buf.Release();
            }
        }
    }
}