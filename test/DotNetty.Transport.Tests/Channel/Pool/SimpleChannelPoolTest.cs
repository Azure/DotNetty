// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Channel.Pool
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Local;
    using DotNetty.Transport.Channels.Pool;
    using Xunit;

    public class SimpleChannelPoolTest
    {
        static readonly string LOCAL_ADDR_ID = "test.id";

        [Fact]
        public async Task TestAcquire()
        {
            var group = new MultithreadEventLoopGroup();
            var addr = new LocalAddress(LOCAL_ADDR_ID);
            var cb = new Bootstrap().RemoteAddress(addr).Group(group).Channel<LocalChannel>();

            var sb = new ServerBootstrap()
                .Group(group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);
            var handler = new CountingChannelPoolHandler();

            var pool = new SimpleChannelPool(cb, handler);

            IChannel channel = await pool.AcquireAsync();

            await pool.ReleaseAsync(channel);

            IChannel channel2 = await pool.AcquireAsync();
            Assert.Same(channel, channel2);
            Assert.Equal(1, handler.ChannelCount);
            await pool.ReleaseAsync(channel2);

            // Should fail on multiple release calls.
            await Assert.ThrowsAsync<ArgumentException>(() => pool.ReleaseAsync(channel2));
            Assert.False(channel.Active);
            try
            {
                await pool.ReleaseAsync(channel2);
                Assert.True(false, "release should fail");
            }
            catch (ArgumentException)
            {
                // expected
                Assert.False(channel.Active);
            }

            Assert.Equal(1, handler.AcquiredCount);
            Assert.Equal(2, handler.ReleasedCount);

            await sc.CloseAsync();
            group.ShutdownGracefullyAsync();
        }

        [Fact]
        public async Task TestBoundedChannelPoolSegment()
        {
            var group = new MultithreadEventLoopGroup();
            var addr = new LocalAddress(LOCAL_ADDR_ID);
            Bootstrap cb = new Bootstrap().RemoteAddress(addr).Group(group).Channel<LocalChannel>();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);
            var handler = new CountingChannelPoolHandler();

            var pool = new SingleChannelPool(cb, handler);

            IChannel channel = await pool.AcquireAsync();
            IChannel channel2 = await pool.AcquireAsync();

            await pool.ReleaseAsync(channel);
            await Assert.ThrowsAsync<InvalidOperationException>(() => pool.ReleaseAsync(channel2));

            await channel2.CloseAsync();

            Assert.Equal(2, handler.ChannelCount);
            Assert.Equal(0, handler.AcquiredCount);
            Assert.Equal(1, handler.ReleasedCount);
            await sc.CloseAsync();
            await channel.CloseAsync();
            await channel2.CloseAsync();
            group.ShutdownGracefullyAsync();
        }

        /**
         * Tests that if channel was unhealthy it is not offered back to the pool.
         *
         * @throws Exception
         */
        [Fact]
        public async Task TestUnhealthyChannelIsNotOffered()
        {
            var group = new MultithreadEventLoopGroup();
            var addr = new LocalAddress(LOCAL_ADDR_ID);
            Bootstrap cb = new Bootstrap().RemoteAddress(addr).Group(group).Channel<LocalChannel>();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);
            var handler = new CountingChannelPoolHandler();
            var pool = new SimpleChannelPool(cb, handler);
            IChannel channel1 = await pool.AcquireAsync();
            await pool.ReleaseAsync(channel1);
            IChannel channel2 = await pool.AcquireAsync();
            //first check that when returned healthy then it actually offered back to the pool.
            Assert.Same(channel1, channel2);

            await channel1.CloseAsync();

            await pool.ReleaseAsync(channel1);
            IChannel channel3 = await pool.AcquireAsync();
            //channel1 was not healthy anymore so it should not get acquired anymore.
            Assert.NotSame(channel1, channel3);
            await sc.CloseAsync();
            await channel3.CloseAsync();
            group.ShutdownGracefullyAsync();
        }

        /**
         * Tests that if channel was unhealthy it is was offered back to the pool because
         * it was requested not to validate channel health on release.
         *
         * @throws Exception
         */
        [Fact]
        public async Task TestUnhealthyChannelIsOfferedWhenNoHealthCheckRequested()
        {
            var group = new MultithreadEventLoopGroup();
            var addr = new LocalAddress(LOCAL_ADDR_ID);
            Bootstrap cb = new Bootstrap().RemoteAddress(addr).Group(group).Channel<LocalChannel>();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);
            var handler = new CountingChannelPoolHandler();
            var pool = new SimpleChannelPool(cb, handler, ChannelActiveHealthChecker.Instance, false);
            IChannel channel1 = await pool.AcquireAsync();
            await channel1.CloseAsync();
            await pool.ReleaseAsync(channel1);

            IChannel channel2 = await pool.AcquireAsync();
            //verifying that in fact the channel2 is different that means is not pulled from the pool
            Assert.NotSame(channel1, channel2);
            await sc.CloseAsync();
            await channel2.CloseAsync();
            group.ShutdownGracefullyAsync();
        }

        [Fact]
        public void TestBootstrap()
        {
            using (var pool = new SimpleChannelPool(new Bootstrap(), new CountingChannelPoolHandler()))
            {
                Assert.NotNull(pool.Bootstrap);
            }
        }

        [Fact]
        public void TestHandler()
        {
            var handler = new CountingChannelPoolHandler();
            using (var pool = new SimpleChannelPool(new Bootstrap(), handler))
            {
                Assert.Same(handler, pool.Handler);
            }
        }

        [Fact]
        public void TestHealthChecker()
        {
            IChannelHealthChecker healthChecker = ChannelActiveHealthChecker.Instance;
            using (var pool = new SimpleChannelPool(new Bootstrap(), new CountingChannelPoolHandler(), healthChecker))
            {
                Assert.Same(healthChecker, pool.HealthChecker);
            }
        }

        [Fact]
        public void TestReleaseHealthCheck()
        {
            using (var healthCheckOnReleasePool = new SimpleChannelPool(new Bootstrap(), new CountingChannelPoolHandler(), ChannelActiveHealthChecker.Instance, true))
            {
                Assert.True(healthCheckOnReleasePool.ReleaseHealthCheck);
            }

            using (var noHealthCheckOnReleasePool = new SimpleChannelPool(new Bootstrap(), new CountingChannelPoolHandler(), ChannelActiveHealthChecker.Instance, false))
            {
                Assert.False(noHealthCheckOnReleasePool.ReleaseHealthCheck);
            }
        }

        class SingleChannelPool : SimpleChannelPool
        {
            IChannel channel;

            internal SingleChannelPool(Bootstrap bootstrap, IChannelPoolHandler handler)
                : base(bootstrap, handler)
            {
            }

            protected override bool TryPollChannel(out IChannel channel)
                => (channel = Interlocked.Exchange(ref this.channel, null)) != null;

            protected override bool TryOfferChannel(IChannel channel)
                => Interlocked.CompareExchange(ref this.channel, channel, null) == null;
        }
    }
}