// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Redis.Messages;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class RedisDecoderTest
    {
        [Theory]
        [InlineData("OK")]
        public void DecodeSimpleString(string value)
        {
            var channel = new EmbeddedChannel(
                new RedisDecoder(),
                new RedisBulkStringAggregator(),
                new RedisArrayAggregator());

            Assert.False(channel.WriteInbound("+".Buffer()));
            foreach (char c in value)
            {
                string charValue = new string(new[] { c });
                Assert.False(channel.WriteInbound(charValue.Buffer()));
            }
            Assert.True(channel.WriteInbound("\r\n".Buffer()));

            var message = channel.ReadInbound<SimpleStringRedisMessage>();
            Assert.NotNull(message);
            Assert.Equal(value, message.Content);

            ReferenceCountUtil.Release(message);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void DecodeTwoSimpleStrings()
        {
            var channel = new EmbeddedChannel(
                new RedisDecoder(),
                new RedisBulkStringAggregator(),
                new RedisArrayAggregator());

            Assert.False(channel.WriteInbound("+".Buffer()));
            Assert.False(channel.WriteInbound("O".Buffer()));
            Assert.False(channel.WriteInbound("K".Buffer()));
            Assert.True(channel.WriteInbound("\r\n+SEC".Buffer()));
            Assert.True(channel.WriteInbound("OND\r\n".Buffer()));

            var message = channel.ReadInbound<SimpleStringRedisMessage>();
            Assert.NotNull(message);
            Assert.Equal("OK", message.Content);
            ReferenceCountUtil.Release(message);

            message = channel.ReadInbound<SimpleStringRedisMessage>();
            Assert.NotNull(message);
            Assert.Equal("SECOND", message.Content);
            ReferenceCountUtil.Release(message);

            Assert.False(channel.Finish());
        }

        [Theory]
        [InlineData("ERROR sample message")]
        public void DecodeError(string value)
        {
            var channel = new EmbeddedChannel(
                new RedisDecoder(),
                new RedisBulkStringAggregator(),
                new RedisArrayAggregator());

            Assert.False(channel.WriteInbound("-".Buffer()));
            Assert.False(channel.WriteInbound(value.Buffer()));
            Assert.False(channel.WriteInbound("\r".Buffer()));
            Assert.True(channel.WriteInbound("\n".Buffer()));

            var message = channel.ReadInbound<ErrorRedisMessage>();
            Assert.Equal(value, message.Content);

            ReferenceCountUtil.Release(message);
            Assert.False(channel.Finish());
        }

        [Theory]
        [InlineData(1234L)]
        public void DecodeInteger(long value)
        {
            var channel = new EmbeddedChannel(
                new RedisDecoder(),
                new RedisBulkStringAggregator(),
                new RedisArrayAggregator());

            Assert.False(channel.WriteInbound(":".Buffer()));
            Assert.False(channel.WriteInbound(value.Buffer()));
            Assert.True(channel.WriteInbound("\r\n".Buffer()));

            var message = channel.ReadInbound<IntegerRedisMessage>();
            Assert.Equal(value, message.Value);

            ReferenceCountUtil.Release(message);
            Assert.False(channel.Finish());
        }

        [Theory]
        [InlineData("bulk\nst", "ring\ntest\n1234")]
        public void DecodeBulkString(string value1, string value2)
        {
            var channel = new EmbeddedChannel(
                new RedisDecoder(),
                new RedisBulkStringAggregator(),
                new RedisArrayAggregator());

            byte[] content = (value1 + value2).Bytes();

            Assert.False(channel.WriteInbound("$".Buffer()));
            Assert.False(channel.WriteInbound(content.Length.ToString().Buffer()));
            Assert.False(channel.WriteInbound("\r\n".Buffer()));
            Assert.False(channel.WriteInbound(value1.Buffer()));
            Assert.False(channel.WriteInbound(value2.Buffer()));
            Assert.True(channel.WriteInbound("\r\n".Buffer()));

            var message = channel.ReadInbound<FullBulkStringRedisMessage>();
            byte[] output = message.Content.Bytes();

            Assert.Equal(content.Length, output.Length);
            Assert.True(content.SequenceEqual(output));

            ReferenceCountUtil.Release(message);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void DecodeEmptyBulkString()
        {
            var channel = new EmbeddedChannel(
                new RedisDecoder(),
                new RedisBulkStringAggregator(),
                new RedisArrayAggregator());

            byte[] content = "".Bytes();
            Assert.False(channel.WriteInbound("$".Buffer()));
            Assert.False(channel.WriteInbound(content.Length.ToString().Buffer()));
            Assert.False(channel.WriteInbound("\r\n".Buffer()));
            Assert.False(channel.WriteInbound(content.Buffer()));
            Assert.True(channel.WriteInbound("\r\n".Buffer()));

            var message = channel.ReadInbound<IFullBulkStringRedisMessage>();

            byte[] output = message.Content.Bytes();

            Assert.Equal(content.Length, output.Length);
            Assert.True(content.SequenceEqual(output));

            ReferenceCountUtil.Release(message);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void DecodeNullBulkString()
        {
            var channel = new EmbeddedChannel(
                new RedisDecoder(),
                new RedisBulkStringAggregator(),
                new RedisArrayAggregator());

            const long NullValue = -1;
            Assert.False(channel.WriteInbound("$".Buffer()));
            Assert.False(channel.WriteInbound(NullValue.Buffer()));
            Assert.True(channel.WriteInbound("\r\n".Buffer()));

            Assert.True(channel.WriteInbound("$".Buffer()));
            Assert.True(channel.WriteInbound(NullValue.Buffer()));
            Assert.True(channel.WriteInbound("\r\n".Buffer()));

            var message = channel.ReadInbound<IFullBulkStringRedisMessage>();
            Assert.NotNull(message);
            Assert.True(message.IsNull);
            ReferenceCountUtil.Release(message);

            message = channel.ReadInbound<IFullBulkStringRedisMessage>();
            Assert.NotNull(message);
            Assert.True(message.IsNull);
            ReferenceCountUtil.Release(message);

            message = channel.ReadInbound<IFullBulkStringRedisMessage>();
            Assert.Null(message);

            Assert.False(channel.Finish());
        }

        [Fact]
        public void DecodeSimpleArray()
        {
            var channel = new EmbeddedChannel(
                new RedisDecoder(),
                new RedisBulkStringAggregator(),
                new RedisArrayAggregator());

            Assert.False(channel.WriteInbound("*3\r\n".Buffer()));
            Assert.False(channel.WriteInbound(":1234\r\n".Buffer()));
            Assert.False(channel.WriteInbound("+sim".Buffer()));
            Assert.False(channel.WriteInbound("ple\r\n-err".Buffer()));
            Assert.True(channel.WriteInbound("or\r\n".Buffer()));

            var message = channel.ReadInbound<ArrayRedisMessage>();
            Assert.NotNull(message);
            IList<IRedisMessage> children = message.Children;
            Assert.NotNull(children);
            Assert.Equal(3, children.Count);

            Assert.IsType<IntegerRedisMessage>(children[0]);
            Assert.Equal(1234L, ((IntegerRedisMessage)children[0]).Value);

            Assert.IsType<SimpleStringRedisMessage>(children[1]);
            Assert.Equal("simple", ((SimpleStringRedisMessage)children[1]).Content);

            Assert.IsType<ErrorRedisMessage>(children[2]);
            Assert.Equal("error", ((ErrorRedisMessage)children[2]).Content);

            ReferenceCountUtil.Release(message);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void DecodeNestedArray()
        {
            var channel = new EmbeddedChannel(
                new RedisDecoder(),
                new RedisBulkStringAggregator(),
                new RedisArrayAggregator());

            IByteBuffer buffer = Unpooled.Buffer();
            buffer.WriteBytes("*2\r\n".Buffer());
            buffer.WriteBytes("*3\r\n:1\r\n:2\r\n:3\r\n".Buffer());
            buffer.WriteBytes("*2\r\n+Foo\r\n-Bar\r\n".Buffer());
            Assert.True(channel.WriteInbound(buffer));

            var message = channel.ReadInbound<ArrayRedisMessage>();
            Assert.NotNull(message);
            IList<IRedisMessage> children = message.Children;
            Assert.NotNull(children);
            Assert.Equal(2, children.Count);

            var intArray = (ArrayRedisMessage)children[0];
            var strArray = (ArrayRedisMessage)children[1];

            Assert.Equal(3, intArray.Children.Count);
            Assert.Equal(1L, ((IntegerRedisMessage)intArray.Children[0]).Value);
            Assert.Equal(2L, ((IntegerRedisMessage)intArray.Children[1]).Value);
            Assert.Equal(3L, ((IntegerRedisMessage)intArray.Children[2]).Value);

            Assert.Equal(2, strArray.Children.Count);
            Assert.Equal("Foo", ((SimpleStringRedisMessage)strArray.Children[0]).Content);
            Assert.Equal("Bar", ((ErrorRedisMessage)strArray.Children[1]).Content);

            Assert.False(channel.Finish());
        }

        [Fact]
        public void ErrorOnDoubleReleaseArrayReferenceCounted()
        {
            var channel = new EmbeddedChannel(
                new RedisDecoder(),
                new RedisBulkStringAggregator(),
                new RedisArrayAggregator());

            IByteBuffer buffer = Unpooled.Buffer();
            buffer.WriteBytes("*2\r\n".Buffer());
            buffer.WriteBytes("*3\r\n:1\r\n:2\r\n:3\r\n".Buffer());
            buffer.WriteBytes("*2\r\n+Foo\r\n-Bar\r\n".Buffer());
            Assert.True(channel.WriteInbound(buffer));

            var message = channel.ReadInbound<ArrayRedisMessage>();
            ReferenceCountUtil.Release(message);

            Assert.Throws<InvalidOperationException>(() => ReferenceCountUtil.Release(message));
            Assert.False(channel.Finish());
        }

        [Fact]
        public void ErrorOnReleaseArrayChildReferenceCounted()
        {
            var channel = new EmbeddedChannel(
                new RedisDecoder(),
                new RedisBulkStringAggregator(),
                new RedisArrayAggregator());

            IByteBuffer buffer = Unpooled.Buffer();
            buffer.WriteBytes("*2\r\n".Buffer());
            buffer.WriteBytes("*3\r\n:1\r\n:2\r\n:3\r\n".Buffer());
            buffer.WriteBytes("$3\r\nFoo\r\n".Buffer());
            Assert.True(channel.WriteInbound(buffer));

            var message = channel.ReadInbound<ArrayRedisMessage>();
            IList<IRedisMessage> children = message.Children;
            ReferenceCountUtil.Release(message);

            Assert.Throws<InvalidOperationException>(() => ReferenceCountUtil.Release(children[0]));
            Assert.False(channel.Finish());
        }

        [Fact]
        public void ErrorOnReleasecontentOfArrayChildReferenceCounted()
        {
            var channel = new EmbeddedChannel(
                new RedisDecoder(),
                new RedisBulkStringAggregator(),
                new RedisArrayAggregator());

            IByteBuffer buffer = Unpooled.Buffer();
            buffer.WriteBytes("*2\r\n".Buffer());
            buffer.WriteBytes("$3\r\nFoo\r\n$3\r\nBar\r\n".Buffer());
            Assert.True(channel.WriteInbound(buffer));

            var message = channel.ReadInbound<ArrayRedisMessage>();
            IList<IRedisMessage> children = message.Children;
            IByteBuffer contentBuffer = ((FullBulkStringRedisMessage)children[0]).Content;

            ReferenceCountUtil.Release(message);
            Assert.Throws<IllegalReferenceCountException>(() => ReferenceCountUtil.Release(contentBuffer));
            Assert.False(channel.Finish());
        }
    }
}
