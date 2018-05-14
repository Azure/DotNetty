// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Tests
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Redis.Messages;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    using static RedisCodecTestUtil;

    public sealed class RedisDecoderTest : IDisposable
    {
        readonly EmbeddedChannel channel;

        public RedisDecoderTest()
        {
            this.channel = NewChannel(false);
        }

        static EmbeddedChannel NewChannel(bool decodeInlineCommands) =>
            new EmbeddedChannel(
                new RedisDecoder(decodeInlineCommands),
                new RedisBulkStringAggregator(),
                new RedisArrayAggregator());

        [Fact]
        public void SplitEndOfLine()
        {
            Assert.False(this.channel.WriteInbound(ByteBufOf("$6\r\nfoobar\r")));
            Assert.True(this.channel.WriteInbound(ByteBufOf("\n")));

            var msg = this.channel.ReadInbound<IRedisMessage>();
            Assert.IsAssignableFrom<IFullBulkStringRedisMessage>(msg);
            ReferenceCountUtil.Release(msg);
        }

        [Fact]
        public void DecodeInlineCommandFalse()
        {
            /*
                The following in the original Netty but the exceptoin is thrown
                from the first writeInbound because the RedisMessageType would
                validate decodeInlineCommands upfront.

                Assert.False(this.channel.WriteInbound(ByteBufOf("P")));
                Assert.False(this.channel.WriteInbound(ByteBufOf("I")));
                Assert.False(this.channel.WriteInbound(ByteBufOf("N")));
                Assert.False(this.channel.WriteInbound(ByteBufOf("G")));
                Assert.True(this.channel.WriteInbound(ByteBufOf("\r\n")));
                this.channel.ReadInbound<IRedisMessage>();
            */
            Assert.Throws<DecoderException>(() => this.channel.WriteInbound(ByteBufOf("P")));
        }

        [Fact]
        public void DecodeInlineCommand()
        {
            EmbeddedChannel ch = NewChannel(true);
            try
            {
                Assert.False(ch.WriteInbound(ByteBufOf("P")));
                Assert.False(ch.WriteInbound(ByteBufOf("I")));
                Assert.False(ch.WriteInbound(ByteBufOf("N")));
                Assert.False(ch.WriteInbound(ByteBufOf("G")));
                Assert.True(ch.WriteInbound(ByteBufOf("\r\n")));

                var msg = ch.ReadInbound<InlineCommandRedisMessage>();

                Assert.Equal("PING", msg.Content);
                ReferenceCountUtil.Release(msg);
            }
            finally
            {
                ch.CloseAsync().Wait(TimeSpan.FromSeconds(10));
            }
        }

        [Fact]
        public void DecodeTwoSimpleStrings()
        {
            Assert.False(this.channel.WriteInbound(ByteBufOf("+")));
            Assert.False(this.channel.WriteInbound(ByteBufOf("O")));
            Assert.False(this.channel.WriteInbound(ByteBufOf("K")));
            Assert.True(this.channel.WriteInbound(ByteBufOf("\r\n+SEC")));
            Assert.True(this.channel.WriteInbound(ByteBufOf("OND\r\n")));

            var msg1 = this.channel.ReadInbound<SimpleStringRedisMessage>();
            Assert.Equal("OK", msg1.Content);
            ReferenceCountUtil.Release(msg1);

            var msg2 = this.channel.ReadInbound<SimpleStringRedisMessage>();
            Assert.Equal("SECOND", msg2.Content);
            ReferenceCountUtil.Release(msg2);
        }

        [Fact]
        public void DecodeError()
        {
            const string Content = "ERROR sample message";
            Assert.False(this.channel.WriteInbound(ByteBufOf("-")));
            Assert.False(this.channel.WriteInbound(ByteBufOf(Content)));
            Assert.False(this.channel.WriteInbound(ByteBufOf("\r")));
            Assert.True(this.channel.WriteInbound(ByteBufOf("\n")));

            var msg = this.channel.ReadInbound<ErrorRedisMessage>();
            Assert.Equal(Content, msg.Content);

            ReferenceCountUtil.Release(msg);
        }

        [Fact]
        public void DecodeInteger()
        {
            const long Value = 1234L;
            byte[] content = BytesOf(Value);
            Assert.False(this.channel.WriteInbound(ByteBufOf(":")));
            Assert.False(this.channel.WriteInbound(ByteBufOf(content)));
            Assert.True(this.channel.WriteInbound(ByteBufOf("\r\n")));

            var msg = this.channel.ReadInbound<IntegerRedisMessage>();
            Assert.Equal(Value, msg.Value);

            ReferenceCountUtil.Release(msg);
        }

        [Fact]
        public void DecodeBulkString()
        {
            const string Buf1 = "bulk\nst";
            const string Buf2 = "ring\ntest\n1234";
            byte[] content = BytesOf(Buf1 + Buf2);
            Assert.False(this.channel.WriteInbound(ByteBufOf("$")));
            Assert.False(this.channel.WriteInbound(ByteBufOf(Convert.ToString(content.Length))));
            Assert.False(this.channel.WriteInbound(ByteBufOf("\r\n")));
            Assert.False(this.channel.WriteInbound(ByteBufOf(Buf1)));
            Assert.False(this.channel.WriteInbound(ByteBufOf(Buf2)));
            Assert.True(this.channel.WriteInbound(ByteBufOf("\r\n")));

            var msg = this.channel.ReadInbound<FullBulkStringRedisMessage>();
            Assert.Equal(content, BytesOf(msg.Content));

            ReferenceCountUtil.Release(msg);
        }

        [Fact]
        public void DecodeEmptyBulkString()
        {
            byte[] content = BytesOf("");
            Assert.False(this.channel.WriteInbound(ByteBufOf("$")));
            Assert.False(this.channel.WriteInbound(ByteBufOf(Convert.ToString(content.Length))));
            Assert.False(this.channel.WriteInbound(ByteBufOf("\r\n")));
            Assert.False(this.channel.WriteInbound(ByteBufOf(content)));
            Assert.True(this.channel.WriteInbound(ByteBufOf("\r\n")));

            var msg = this.channel.ReadInbound<IFullBulkStringRedisMessage>();
            Assert.Equal(content, BytesOf(msg.Content));

            ReferenceCountUtil.Release(msg);
        }

        [Fact]
        public void DecodeNullBulkString()
        {
            Assert.False(this.channel.WriteInbound(ByteBufOf("$")));
            Assert.False(this.channel.WriteInbound(ByteBufOf(Convert.ToString(-1))));
            Assert.True(this.channel.WriteInbound(ByteBufOf("\r\n")));

            Assert.True(this.channel.WriteInbound(ByteBufOf("$")));
            Assert.True(this.channel.WriteInbound(ByteBufOf(Convert.ToString(-1))));
            Assert.True(this.channel.WriteInbound(ByteBufOf("\r\n")));

            var msg1 = this.channel.ReadInbound<IFullBulkStringRedisMessage>();
            Assert.True(msg1.IsNull);
            ReferenceCountUtil.Release(msg1);

            var msg2 = this.channel.ReadInbound<IFullBulkStringRedisMessage>();
            Assert.True(msg2.IsNull);
            ReferenceCountUtil.Release(msg2);

            var msg3 = this.channel.ReadInbound<IFullBulkStringRedisMessage>();
            Assert.Null(msg3);
        }

        [Fact]
        public void DecodeSimpleArray()
        {
            Assert.False(this.channel.WriteInbound(ByteBufOf("*3\r\n")));
            Assert.False(this.channel.WriteInbound(ByteBufOf(":1234\r\n")));
            Assert.False(this.channel.WriteInbound(ByteBufOf("+sim")));
            Assert.False(this.channel.WriteInbound(ByteBufOf("ple\r\n-err")));
            Assert.True(this.channel.WriteInbound(ByteBufOf("or\r\n")));

            var msg = this.channel.ReadInbound<IArrayRedisMessage>();
            IList<IRedisMessage> children = msg.Children;

            Assert.Equal(3, msg.Children.Count);

            Assert.IsType<IntegerRedisMessage>(children[0]);
            Assert.Equal(1234L, ((IntegerRedisMessage)children[0]).Value);
            Assert.IsType<SimpleStringRedisMessage>(children[1]);
            Assert.Equal("simple", ((SimpleStringRedisMessage)children[1]).Content);
            Assert.IsType<ErrorRedisMessage>(children[2]);
            Assert.Equal("error", ((ErrorRedisMessage)children[2]).Content);

            ReferenceCountUtil.Release(msg);
        }

        [Fact]
        public void DecodeNestedArray()
        {
            IByteBuffer buf = Unpooled.Buffer();
            buf.WriteBytes(ByteBufOf("*2\r\n"));
            buf.WriteBytes(ByteBufOf("*3\r\n:1\r\n:2\r\n:3\r\n"));
            buf.WriteBytes(ByteBufOf("*2\r\n+Foo\r\n-Bar\r\n"));
            Assert.True(this.channel.WriteInbound(buf));

            var msg = this.channel.ReadInbound<IArrayRedisMessage>();
            IList<IRedisMessage> children = msg.Children;

            Assert.Equal(2, msg.Children.Count);

            var intArray = (IArrayRedisMessage)children[0];
            var strArray = (IArrayRedisMessage)children[1];

            Assert.Equal(3, intArray.Children.Count);
            Assert.Equal(1L, ((IntegerRedisMessage)intArray.Children[0]).Value);
            Assert.Equal(2L, ((IntegerRedisMessage)intArray.Children[1]).Value);
            Assert.Equal(3L, ((IntegerRedisMessage)intArray.Children[2]).Value);

            Assert.Equal(2, strArray.Children.Count);
            Assert.Equal("Foo", ((SimpleStringRedisMessage)strArray.Children[0]).Content);
            Assert.Equal("Bar", ((ErrorRedisMessage)strArray.Children[1]).Content);

            ReferenceCountUtil.Release(msg);
        }

        [Fact]
        public void DoubleReleaseArrayReferenceCounted()
        {
            IByteBuffer buf = Unpooled.Buffer();
            buf.WriteBytes(ByteBufOf("*2\r\n"));
            buf.WriteBytes(ByteBufOf("*3\r\n:1\r\n:2\r\n:3\r\n"));
            buf.WriteBytes(ByteBufOf("*2\r\n+Foo\r\n-Bar\r\n"));
            Assert.True(this.channel.WriteInbound(buf));

            var msg = this.channel.ReadInbound<IArrayRedisMessage>();

            ReferenceCountUtil.Release(msg);
            Assert.Throws<IllegalReferenceCountException>(() => ReferenceCountUtil.Release(msg));
        }

        [Fact]
        public void ReleaseArrayChildReferenceCounted()
        {
            IByteBuffer buf = Unpooled.Buffer();
            buf.WriteBytes(ByteBufOf("*2\r\n"));
            buf.WriteBytes(ByteBufOf("*3\r\n:1\r\n:2\r\n:3\r\n"));
            buf.WriteBytes(ByteBufOf("$3\r\nFoo\r\n"));
            Assert.True(this.channel.WriteInbound(buf));

            var msg = this.channel.ReadInbound<ArrayRedisMessage>();

            IList<IRedisMessage> children = msg.Children;
            ReferenceCountUtil.Release(msg);
            Assert.Throws<IllegalReferenceCountException>(() => ReferenceCountUtil.Release(children[1]));
        }

        [Fact]
        public void ReleasecontentOfArrayChildReferenceCounted()
        {
            IByteBuffer buf = Unpooled.Buffer();
            buf.WriteBytes(ByteBufOf("*2\r\n"));
            buf.WriteBytes(ByteBufOf("$3\r\nFoo\r\n$3\r\nBar\r\n"));
            Assert.True(this.channel.WriteInbound(buf));

            var msg = this.channel.ReadInbound<IArrayRedisMessage>();

            IList<IRedisMessage> children = msg.Children;
            IByteBuffer childBuf = ((FullBulkStringRedisMessage)children[0]).Content;
            ReferenceCountUtil.Release(msg);
            Assert.Throws<IllegalReferenceCountException>(() => ReferenceCountUtil.Release(childBuf));
        }

        public void Dispose()
        {
            try
            {
                Assert.False(this.channel.Finish());
            }
            finally
            {
                this.channel.CloseAsync().Wait(TimeSpan.FromSeconds(10));
            }
        }
    }
}
