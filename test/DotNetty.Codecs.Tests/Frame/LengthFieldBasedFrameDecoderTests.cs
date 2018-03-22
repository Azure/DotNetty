// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Tests.Frame
{
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class LengthFieldBasedFrameDecoderTests
    {
        [Fact]
        public void FailSlowTooLongFrameRecovery()
        {
            var ch = new EmbeddedChannel(new LengthFieldBasedFrameDecoder(5, 0, 4, 0, 4, false));
            for (int i = 0; i < 2; i++)
            {
                Assert.False(ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { 0, 0, 0, 2 })));
                Assert.Throws<TooLongFrameException>(() => ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { 0, 0 })));
                ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { 0, 0, 0, 1, (byte)'A' }));
                var buf = ch.ReadInbound<IByteBuffer>();
                Assert.Equal("A", buf.ToString(Encoding.UTF8));
                buf.Release();
            }
        }

        [Fact]
        public void TestFailFastTooLongFrameRecovery()
        {
            var ch = new EmbeddedChannel(new LengthFieldBasedFrameDecoder(5, 0, 4, 0, 4));

            for (int i = 0; i < 2; i++)
            {
                Assert.Throws<TooLongFrameException>(() => ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { 0, 0, 0, 2 })));

                ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { 0, 0, 0, 0, 0, 1, (byte)'A' }));
                var buf = ch.ReadInbound<IByteBuffer>();
                Assert.Equal("A", buf.ToString(Encoding.UTF8));
                buf.Release();
            }
        }
    }
}