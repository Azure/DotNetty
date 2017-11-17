// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Channel.Pool
{
    using System;
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
            var cb = new Bootstrap();
            cb.RemoteAddress(addr);
            cb.Group(group).Channel<LocalChannel>();

            var sb = new ServerBootstrap();
            sb.Group(group)
                .Channel<LocalServerChannel>()
                .ChildHandler(
                    new ActionChannelInitializer<LocalChannel>(
                        ch => ch.Pipeline.AddLast(new ChannelHandlerAdapter()))
                );

            // Start server
            var sc = await sb.BindAsync(addr);
            var handler = new CountingChannelPoolHandler();

            var pool = new SimpleChannelPool(cb, handler);

            var channel = await pool.AcquireAsync();

            await pool.ReleaseAsync(channel);

            var channel2 = await pool.AcquireAsync();
            Assert.Same(channel, channel2);
            Assert.Equal(1, handler.ChannelCount);
            await pool.ReleaseAsync(channel2);

            // Should fail on multiple release calls.
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
    }
}