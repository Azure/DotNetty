// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Transport.Channels.Embedded;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Redis.Messages;
    using Xunit;

    public sealed class RedisEncoderTests
    {
        [Theory]
        [InlineData("simple")]
        public void EncodeSimpleString(string value)
        {
            var channel = new EmbeddedChannel(new RedisEncoder());
            var message = new SimpleStringRedisMessage(value);
            Assert.True(channel.WriteOutbound(message));

            IByteBuffer written = ReadAll(channel);
            byte[] output = written.Bytes();
            byte[] expected = $"+{value}\r\n".Bytes();

            Assert.Equal(expected.Length, output.Length);
            Assert.True(output.SequenceEqual(expected));
            written.Release();
        }

        [Theory]
        [InlineData("error1")]
        public void EncodeError(string value)
        {
            var channel = new EmbeddedChannel(new RedisEncoder());
            var message = new ErrorRedisMessage(value);
            Assert.True(channel.WriteOutbound(message));

            IByteBuffer written = ReadAll(channel);
            byte[] output = written.Bytes();
            byte[] expected = $"-{value}\r\n".Bytes();

            Assert.Equal(expected.Length, output.Length);
            Assert.True(output.SequenceEqual(expected));
            written.Release();
        }

        [Theory]
        [InlineData(1234L)]
        public void EncodeInteger(long value)
        {
            var channel = new EmbeddedChannel(new RedisEncoder());
            var message = new IntegerRedisMessage(value);
            Assert.True(channel.WriteOutbound(message));

            IByteBuffer written = ReadAll(channel);
            byte[] output = written.Bytes();
            byte[] expected = $":{value}\r\n".Bytes();

            Assert.Equal(expected.Length, output.Length);
            Assert.True(output.SequenceEqual(expected));
            written.Release();
        }

        [Fact]
        public void EncodeBulkStringContent()
        {
            var channel = new EmbeddedChannel(new RedisEncoder());

            var header = new BulkStringHeaderRedisMessage(16);

            IByteBuffer buffer1 = "bulk\nstr".Buffer();
            buffer1.Retain();
            var body1 = new BulkStringRedisContent(buffer1);

            IByteBuffer buffer2 = "ing\ntest".Buffer();
            buffer1.Retain();
            var body2 = new LastBulkStringRedisContent(buffer2);

            Assert.True(channel.WriteOutbound(header));
            Assert.True(channel.WriteOutbound(body1));
            Assert.True(channel.WriteOutbound(body2));
            IByteBuffer written = ReadAll(channel);

            byte[] output = written.Bytes();
            byte[] expected = "$16\r\nbulk\nstring\ntest\r\n".Bytes();

            Assert.Equal(expected.Length, output.Length);
            Assert.True(output.SequenceEqual(expected));
            written.Release();
        }

        [Theory]
        [InlineData(@"bulk\nstring\ntest")]
        public void EncodeFullBulkString(string value)
        {
            var channel = new EmbeddedChannel(new RedisEncoder());

            // Content
            IByteBuffer bulkStringBuffer = value.Buffer();
            bulkStringBuffer.Retain();
            int length = bulkStringBuffer.ReadableBytes;
            var message = new FullBulkStringRedisMessage(bulkStringBuffer);
            Assert.True(channel.WriteOutbound(message));

            IByteBuffer written = ReadAll(channel);
            byte[] output = written.Bytes();
            byte[] expected = $"${length}\r\n{value}\r\n".Bytes();

            Assert.Equal(expected.Length, output.Length);
            Assert.True(output.SequenceEqual(expected));
            written.Release();
        }

        [Fact]
        public void EncodeSimpleArray()
        {
            var channel = new EmbeddedChannel(new RedisEncoder());

            var messages = new List<IRedisMessage>();
            IByteBuffer buffer = "foo".Buffer();
            buffer.Retain();
            messages.Add(new FullBulkStringRedisMessage(buffer));

            buffer = "bar".Buffer();
            buffer.Retain();
            messages.Add(new FullBulkStringRedisMessage(buffer));

            IRedisMessage message = new ArrayRedisMessage(messages);
            Assert.True(channel.WriteOutbound(message));

            IByteBuffer written = ReadAll(channel);
            byte[] output = written.Bytes();
            byte[] expected = "*2\r\n$3\r\nfoo\r\n$3\r\nbar\r\n".Bytes();

            Assert.Equal(expected.Length, output.Length);
            Assert.True(output.SequenceEqual(expected));
            written.Release();
        }

        [Fact]
        public void EncodeNullArray()
        {
            var channel = new EmbeddedChannel(new RedisEncoder());
            IRedisMessage message = ArrayRedisMessage.Null;

            Assert.True(channel.WriteOutbound(message));

            IByteBuffer written = ReadAll(channel);
            byte[] output = written.Bytes();
            byte[] expected = "*-1\r\n".Bytes();

            Assert.Equal(expected.Length, output.Length);
            Assert.True(output.SequenceEqual(expected));
            written.Release();
        }

        [Fact]
        public void EncodeEmptyArray()
        {
            var channel = new EmbeddedChannel(new RedisEncoder());
            IRedisMessage message = ArrayRedisMessage.Empty;

            Assert.True(channel.WriteOutbound(message));

            IByteBuffer written = ReadAll(channel);
            byte[] output = written.Bytes();
            byte[] expected = "*0\r\n".Bytes();

            Assert.Equal(expected.Length, output.Length);
            Assert.True(output.SequenceEqual(expected));
            written.Release();
        }

        [Fact]
        public void EncodeNestedArray()
        {
            var channel = new EmbeddedChannel(new RedisEncoder());

            var grandChildren = new List<IRedisMessage>
            {
                new FullBulkStringRedisMessage("bar".Buffer()),
                new IntegerRedisMessage(-1234L)
            };

            var children = new List<IRedisMessage>
            {
                new SimpleStringRedisMessage("foo"),
                new ArrayRedisMessage(grandChildren)
            };

            IRedisMessage message = new ArrayRedisMessage(children);

            Assert.True(channel.WriteOutbound(message));

            IByteBuffer written = ReadAll(channel);
            byte[] output = written.Bytes();
            byte[] expected = "*2\r\n+foo\r\n*2\r\n$3\r\nbar\r\n:-1234\r\n".Bytes();

            Assert.Equal(expected.Length, output.Length);
            Assert.True(output.SequenceEqual(expected));
            written.Release();
        }

        static IByteBuffer ReadAll(EmbeddedChannel channel)
        {
            Assert.NotNull(channel);

            IByteBuffer buffer = Unpooled.Buffer();

            IByteBuffer read;
            while ((read = channel.ReadOutbound<IByteBuffer>()) != null)
            {
                buffer.WriteBytes(read);
            }

            return buffer;
        }
    }
}
