// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Tests
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Redis.Messages;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    using static RedisCodecTestUtil;

    public sealed class RedisEncoderTests : IDisposable
    {
        readonly EmbeddedChannel channel;

        public RedisEncoderTests()
        {
            this.channel = new EmbeddedChannel(new RedisEncoder());
        }

        [Fact]
        public void EncodeInlineCommand()
        {
            var msg = new InlineCommandRedisMessage("ping");
            Assert.True(this.channel.WriteOutbound(msg));

            IByteBuffer written = ReadAll(this.channel);
            Assert.Equal(BytesOf("ping\r\n"), BytesOf(written));
            written.Release();
        }

        [Fact]
        public void EncodeSimpleString()
        {
            var msg = new SimpleStringRedisMessage("simple");
            Assert.True(this.channel.WriteOutbound(msg));

            IByteBuffer written = ReadAll(this.channel);
            Assert.Equal(BytesOf("+simple\r\n"), BytesOf(written));
            written.Release();
        }

        [Fact]
        public void EncodeError()
        {
            var msg = new ErrorRedisMessage("error1");
            Assert.True(this.channel.WriteOutbound(msg));

            IByteBuffer written = ReadAll(this.channel);
            Assert.Equal(BytesOf("-error1\r\n"), BytesOf(written));
            written.Release();
        }

        [Fact]
        public void EncodeInteger()
        {
            var msg = new IntegerRedisMessage(1234L);
            Assert.True(this.channel.WriteOutbound(msg));

            IByteBuffer written = ReadAll(this.channel);
            Assert.Equal(BytesOf(":1234\r\n"), BytesOf(written));
            written.Release();
        }

        [Fact]
        public void EncodeBulkStringContent()
        {
            var header = new BulkStringHeaderRedisMessage(16);
            var body1 = new DefaultBulkStringRedisContent((IByteBuffer)ByteBufOf("bulk\nstr").Retain());
            var body2 = new DefaultLastBulkStringRedisContent((IByteBuffer)ByteBufOf("ing\ntest").Retain());
            Assert.True(this.channel.WriteOutbound(header));
            Assert.True(this.channel.WriteOutbound(body1));
            Assert.True(this.channel.WriteOutbound(body2));

            IByteBuffer written = ReadAll(this.channel);
            Assert.Equal(BytesOf("$16\r\nbulk\nstring\ntest\r\n"), BytesOf(written));
            written.Release();
        }

        [Fact]
        public void EncodeFullBulkString()
        {
            var bulkString = (IByteBuffer)ByteBufOf("bulk\nstring\ntest").Retain();
            int length = bulkString.ReadableBytes;
            var msg = new FullBulkStringRedisMessage(bulkString);
            Assert.True(this.channel.WriteOutbound(msg));

            IByteBuffer written = ReadAll(this.channel);
            Assert.Equal(BytesOf("$" + length + "\r\nbulk\nstring\ntest\r\n"), BytesOf(written));
            written.Release();
        }

        [Fact]
        public void EncodeSimpleArray()
        {
            var children = new List<IRedisMessage>();
            children.Add(new FullBulkStringRedisMessage((IByteBuffer)ByteBufOf("foo").Retain()));
            children.Add(new FullBulkStringRedisMessage((IByteBuffer)ByteBufOf("bar").Retain()));
            var msg = new ArrayRedisMessage(children);
            Assert.True(this.channel.WriteOutbound(msg));

            IByteBuffer written = ReadAll(this.channel);
            Assert.Equal(BytesOf("*2\r\n$3\r\nfoo\r\n$3\r\nbar\r\n"), BytesOf(written));
            written.Release();
        }

        [Fact]
        public void EncodeNullArray()
        {
            IArrayRedisMessage msg = ArrayRedisMessage.Null;
            Assert.True(this.channel.WriteOutbound(msg));

            IByteBuffer written = ReadAll(this.channel);
            Assert.Equal(BytesOf("*-1\r\n"), BytesOf(written));
            written.Release();
        }

        [Fact]
        public void EncodeEmptyArray()
        {
            IArrayRedisMessage msg = ArrayRedisMessage.Empty;
            Assert.True(this.channel.WriteOutbound(msg));

            IByteBuffer written = ReadAll(this.channel);
            Assert.Equal(BytesOf("*0\r\n"), BytesOf(written));
            written.Release();
        }

        [Fact]
        public void EncodeNestedArray()
        {
            var grandChildren = new List<IRedisMessage>();
            grandChildren.Add(new FullBulkStringRedisMessage(ByteBufOf("bar")));
            grandChildren.Add(new IntegerRedisMessage(-1234L));
            var children = new List<IRedisMessage>();
            children.Add(new SimpleStringRedisMessage("foo"));
            children.Add(new ArrayRedisMessage(grandChildren));
            var msg = new ArrayRedisMessage(children);
            Assert.True(this.channel.WriteOutbound(msg));

            IByteBuffer written = ReadAll(this.channel);
            Assert.Equal(BytesOf("*2\r\n+foo\r\n*2\r\n$3\r\nbar\r\n:-1234\r\n"), BytesOf(written));
            written.Release();
        }

        static IByteBuffer ReadAll(EmbeddedChannel channel)
        {
            IByteBuffer buf = Unpooled.Buffer();
            IByteBuffer read;
            while ((read = channel.ReadOutbound<IByteBuffer>()) != null)
            {
                buf.WriteBytes(read);
                read.Release();
            }
            return buf;
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
