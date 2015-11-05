// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Tests.Frame
{
    using System;
    using System.Linq;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class LengthFieldPrependerTest
    {
        IByteBuffer msg;

        public LengthFieldPrependerTest()
        {
            Encoding iso = Encoding.GetEncoding("ISO-8859-1");
            byte[] charBytes = iso.GetBytes("A");
            msg = Unpooled.CopiedBuffer(charBytes);
        }

        [Fact]
        public void TestPrependLength()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new LengthFieldPrepender(4));
            ch.WriteOutbound(msg);
            IByteBuffer buf = ch.ReadOutbound<IByteBuffer>();
            Assert.Equal(4, buf.ReadableBytes);
            Assert.Equal(msg.ReadableBytes, buf.ReadInt());
            buf.Release();

            buf = ch.ReadOutbound<IByteBuffer>();
            Assert.Same(buf, msg);
            buf.Release();
        }

        [Fact]
        public void TestPrependLengthIncludesLengthFieldLength()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new LengthFieldPrepender(4, true));
            ch.WriteOutbound(msg);
            IByteBuffer buf = ch.ReadOutbound<IByteBuffer>();
            Assert.Equal(4, buf.ReadableBytes);
            Assert.Equal(5, buf.ReadInt());
            buf.Release();

            buf = ch.ReadOutbound<IByteBuffer>();
            Assert.Same(buf, msg);
            buf.Release();
        }

        [Fact]
        public void TestPrependAdjustedLength()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new LengthFieldPrepender(4, -1));
            ch.WriteOutbound(msg);
            IByteBuffer buf = ch.ReadOutbound<IByteBuffer>();
            Assert.Equal(4, buf.ReadableBytes);
            Assert.Equal(msg.ReadableBytes - 1, buf.ReadInt());
            buf.Release();

            buf = ch.ReadOutbound<IByteBuffer>();
            Assert.Same(buf, msg);
            buf.Release();
        }

        [Fact]
        public void TestPrependAdjustedLengthLessThanZero()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new LengthFieldPrepender(4, -2));
            AggregateException ex = Assert.Throws<AggregateException>(() =>
            {
                ch.WriteOutbound(msg);
                Assert.True(false, typeof(EncoderException).Name + " must be raised.");
            });

            Assert.IsType<EncoderException>(ex.InnerExceptions.Single());
        }

        [Fact]
        public void TestPrependLengthInLittleEndian()
        {
            EmbeddedChannel ch = new EmbeddedChannel(new LengthFieldPrepender(ByteOrder.LittleEndian, 4, 0, false));
            ch.WriteOutbound(msg);
            IByteBuffer buf = ch.ReadOutbound<IByteBuffer>();
            Assert.Equal(4, buf.ReadableBytes);
            byte[] writtenBytes = new byte[buf.ReadableBytes];
            buf.GetBytes(0, writtenBytes);
            Assert.Equal(1, writtenBytes[0]);
            Assert.Equal(0, writtenBytes[1]);
            Assert.Equal(0, writtenBytes[2]);
            Assert.Equal(0, writtenBytes[3]);
            buf.Release();

            buf = ch.ReadOutbound<IByteBuffer>();
            Assert.Same(buf, msg);
            buf.Release();
            Assert.False(ch.Finish(), "The channel must have been completely read");
        }
    }
}