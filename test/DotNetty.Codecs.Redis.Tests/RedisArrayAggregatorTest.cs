// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Tests
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Redis.Messages;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class RedisArrayAggregatorTest
    {
        [Fact]
        public void RejectsArrayLargerThanConfiguredMaximum()
        {
            var channel = new EmbeddedChannel(new RedisArrayAggregator(100, 10));

            DecoderException exception = Assert.Throws<DecoderException>(
                () => channel.WriteInbound(new ArrayHeaderRedisMessage(101)));

            Assert.Contains("100", Assert.IsType<CodecException>(exception.InnerException).Message);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void RejectsExcessiveNestedArrayDepth()
        {
            var channel = new EmbeddedChannel(new RedisArrayAggregator(100, 2));

            Assert.False(channel.WriteInbound(new ArrayHeaderRedisMessage(1)));
            Assert.False(channel.WriteInbound(new ArrayHeaderRedisMessage(1)));
            DecoderException exception = Assert.Throws<DecoderException>(
                () => channel.WriteInbound(new ArrayHeaderRedisMessage(1)));

            Assert.Contains("2", Assert.IsType<CodecException>(exception.InnerException).Message);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void RejectsCumulativeNestedElementCount()
        {
            var channel = new EmbeddedChannel(new RedisArrayAggregator(100, 10));

            Assert.False(channel.WriteInbound(new ArrayHeaderRedisMessage(60)));
            DecoderException exception = Assert.Throws<DecoderException>(
                () => channel.WriteInbound(new ArrayHeaderRedisMessage(60)));

            Assert.Contains("100", Assert.IsType<CodecException>(exception.InnerException).Message);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void ReclaimsElementBudgetAfterArrayCompletes()
        {
            var channel = new EmbeddedChannel(new RedisArrayAggregator(2, 10));

            Assert.False(channel.WriteInbound(new ArrayHeaderRedisMessage(1)));
            Assert.True(channel.WriteInbound(new IntegerRedisMessage(1)));
            ReferenceCountUtil.Release(channel.ReadInbound<IArrayRedisMessage>());

            Assert.False(channel.WriteInbound(new ArrayHeaderRedisMessage(2)));
            channel.Pipeline.Remove<RedisArrayAggregator>();
            Assert.False(channel.Finish());
        }

        [Fact]
        public void KeepsNestedArraysChargedUntilTopLevelCompletes()
        {
            var channel = new EmbeddedChannel(new RedisArrayAggregator(5, 10));

            Assert.False(channel.WriteInbound(new ArrayHeaderRedisMessage(2)));
            Assert.False(channel.WriteInbound(new ArrayHeaderRedisMessage(2)));
            Assert.False(channel.WriteInbound(new IntegerRedisMessage(1)));
            Assert.False(channel.WriteInbound(new IntegerRedisMessage(2)));

            DecoderException exception = Assert.Throws<DecoderException>(
                () => channel.WriteInbound(new ArrayHeaderRedisMessage(2)));

            Assert.Contains("5", Assert.IsType<CodecException>(exception.InnerException).Message);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void ReleasesPartialArrayOnRemoval()
        {
            var channel = new EmbeddedChannel(new RedisArrayAggregator());
            var message = new FullBulkStringRedisMessage(Unpooled.Buffer(0));

            Assert.False(channel.WriteInbound(new ArrayHeaderRedisMessage(2)));
            Assert.False(channel.WriteInbound(message));
            Assert.Equal(1, message.ReferenceCount);

            channel.Pipeline.Remove<RedisArrayAggregator>();

            Assert.Equal(0, message.ReferenceCount);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void ReleasesPartialArrayOnChannelInactive()
        {
            var channel = new EmbeddedChannel(new RedisArrayAggregator());
            var message = new FullBulkStringRedisMessage(Unpooled.Buffer(0));

            Assert.False(channel.WriteInbound(new ArrayHeaderRedisMessage(2)));
            Assert.False(channel.WriteInbound(message));
            Assert.Equal(1, message.ReferenceCount);

            Assert.Throws<PrematureChannelClosureException>(() => channel.Finish());

            Assert.Equal(0, message.ReferenceCount);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        public void RejectsNonPositiveLimits(int maxElements, int maxNestedArrayDepth)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new RedisArrayAggregator(maxElements, maxNestedArrayDepth));
        }
    }
}
