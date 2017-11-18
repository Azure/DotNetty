// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Channel.Pool
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Local;
    using DotNetty.Transport.Channels.Pool;
    using Xunit;

    public class FixedChannelPoolTest : IDisposable
    {
        static readonly string LOCAL_ADDR_ID = "test.id";

        readonly IEventLoopGroup group;

        public FixedChannelPoolTest()
        {
            this.group = new MultithreadEventLoopGroup();
        }

        public void Dispose()
        {
            this.group?.ShutdownGracefullyAsync();
        }

        [Fact]
        public async Task TestAcquire()
        {
            var addr = new LocalAddress(LOCAL_ADDR_ID);
            Bootstrap cb = new Bootstrap().RemoteAddress(addr).Group(this.group).Channel<LocalChannel>();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);
            var handler = new CountingChannelPoolHandler();

            var pool = new FixedChannelPool(cb, handler, 1);

            IChannel channel = await pool.AcquireAsync();
            Task<IChannel> future = pool.AcquireAsync();
            Assert.False(future.IsCompleted);

            await pool.ReleaseAsync(channel);
            await Task.Delay(TimeSpan.FromSeconds(1));
            Assert.True(future.IsCompleted);

            IChannel channel2 = future.Result;
            Assert.Same(channel, channel2);
            Assert.Equal(1, handler.ChannelCount);
            Assert.Equal(1, handler.AcquiredCount);
            Assert.Equal(1, handler.ReleasedCount);

            await sc.CloseAsync();
            await channel2.CloseAsync();
        }

        [Fact]
        public async Task TestAcquireTimeout()
        {
            var addr = new LocalAddress(LOCAL_ADDR_ID);
            Bootstrap cb = new Bootstrap().RemoteAddress(addr).Group(this.group).Channel<LocalChannel>();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);
            var handler = new TestChannelPoolHandler();
            var pool = new FixedChannelPool(cb, handler, ChannelActiveHealthChecker.Instance, FixedChannelPool.AcquireTimeoutAction.Fail, TimeSpan.FromMilliseconds(500), 1, int.MaxValue);

            IChannel channel = await pool.AcquireAsync();
            try
            {
                await Assert.ThrowsAsync<TimeoutException>(pool.AcquireAsync);
            }
            finally
            {
                await sc.CloseAsync();
                await channel.CloseAsync();
            }
        }

        [Fact]
        public async Task TestAcquireNewConnection()
        {
            var addr = new LocalAddress(LOCAL_ADDR_ID);
            Bootstrap cb = new Bootstrap().RemoteAddress(addr).Group(this.group).Channel<LocalChannel>();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);
            var handler = new TestChannelPoolHandler();
            var pool = new FixedChannelPool(
                cb,
                handler,
                ChannelActiveHealthChecker.Instance,
                FixedChannelPool.AcquireTimeoutAction.New,
                TimeSpan.FromMilliseconds(500),
                1,
                int.MaxValue);

            IChannel channel = await pool.AcquireAsync();
            IChannel channel2 = await pool.AcquireAsync();
            Assert.NotSame(channel, channel2);
            await sc.CloseAsync();
            await channel.CloseAsync();
            await channel2.CloseAsync();
        }

        /**
         * Tests that the acquiredChannelCount is not added up several times for the same channel acquire request.
         * @throws Exception
         */
        [Fact]
        public async Task TestAcquireNewConnectionWhen()
        {
            var addr = new LocalAddress(LOCAL_ADDR_ID);
            Bootstrap cb = new Bootstrap().RemoteAddress(addr).Group(this.group).Channel<LocalChannel>();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);
            var handler = new TestChannelPoolHandler();
            var pool = new FixedChannelPool(cb, handler, 1);
            IChannel channel1 = await pool.AcquireAsync();
            await channel1.CloseAsync();
            pool.ReleaseAsync(channel1);

            IChannel channel2 = await pool.AcquireAsync();

            Assert.NotSame(channel1, channel2);
            await sc.CloseAsync();
            await channel2.CloseAsync();
        }

        [Fact]
        public async Task TestAcquireBoundQueue()
        {
            var addr = new LocalAddress(LOCAL_ADDR_ID);
            Bootstrap cb = new Bootstrap().RemoteAddress(addr).Group(this.group).Channel<LocalChannel>();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);
            var handler = new TestChannelPoolHandler();
            var pool = new FixedChannelPool(cb, handler, 1, 1);

            IChannel channel = await pool.AcquireAsync();
            Task<IChannel> future = pool.AcquireAsync();
            Assert.False(future.IsCompleted);

            try
            {
                await Assert.ThrowsAsync<InvalidOperationException>(pool.AcquireAsync);
            }
            finally
            {
                await sc.CloseAsync();
                await channel.CloseAsync();
            }
        }

        [Fact]
        public async Task TestReleaseDifferentPool()
        {
            var addr = new LocalAddress(LOCAL_ADDR_ID);
            Bootstrap cb = new Bootstrap().RemoteAddress(addr).Group(this.group).Channel<LocalChannel>();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);
            var handler = new TestChannelPoolHandler();
            var pool = new FixedChannelPool(cb, handler, 1, 1);
            var pool2 = new FixedChannelPool(cb, handler, 1, 1);

            IChannel channel = await pool.AcquireAsync();

            try
            {
                await Assert.ThrowsAsync<ArgumentException>(() => pool2.ReleaseAsync(channel));
            }
            finally
            {
                await sc.CloseAsync();
                await channel.CloseAsync();
            }
        }

        [Fact]
        public async Task TestReleaseAfterClosePool()
        {
            var addr = new LocalAddress(LOCAL_ADDR_ID);
            Bootstrap cb = new Bootstrap().RemoteAddress(addr).Group(this.group).Channel<LocalChannel>();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);

            var pool = new FixedChannelPool(cb, new TestChannelPoolHandler(), 2);
            IChannel channel = await pool.AcquireAsync();
            pool.Dispose();

            await group.GetNext().SubmitAsync(() => TaskEx.Completed);
            var e = await Assert.ThrowsAsync<InvalidOperationException>(() => pool.ReleaseAsync(channel));
            Assert.Same(FixedChannelPool.PoolClosedOnReleaseException, e);
            
            // Since the pool is closed, the Channel should have been closed as well.
            await channel.CloseCompletion;
            Assert.False(channel.Open, "Unexpected open channel");
            await sc.CloseAsync();
        }

        [Fact]
        public async Task TestReleaseClosed()
        {
            var addr = new LocalAddress(LOCAL_ADDR_ID);
            Bootstrap cb = new Bootstrap().RemoteAddress(addr).Group(this.group).Channel<LocalChannel>();

            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            IChannel sc = await sb.BindAsync(addr);

            var pool = new FixedChannelPool(cb, new TestChannelPoolHandler(), 2);
            IChannel channel = await pool.AcquireAsync();
            await channel.CloseAsync();
            await pool.ReleaseAsync(channel);

            await sc.CloseAsync();
        }

        sealed class TestChannelPoolHandler : IChannelPoolHandler
        {
            public void ChannelReleased(IChannel channel)
            {
                // NOOP
            }

            public void ChannelAcquired(IChannel channel)
            {
                // NOOP
            }

            public void ChannelCreated(IChannel channel)
            {
                // NOOP
            }
        }
    }
}