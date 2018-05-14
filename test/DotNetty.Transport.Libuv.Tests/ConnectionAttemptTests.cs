// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;
    using Xunit;

    using static TestUtil;

    [Collection(LibuvTransport)]
    public sealed class ConnectionAttemptTests : IDisposable
    {
        // See /etc/services
        const int UnassignedPort = 4;

        readonly IEventLoopGroup group;

        public ConnectionAttemptTests()
        {
            this.group = new EventLoopGroup(1);
        }

        [Fact]
        public void ConnectTimeout()
        {
            Bootstrap cb = new Bootstrap()
                .Group(this.group)
                .Channel<TcpChannel>();
            ConnectTimeout0(cb);
        }

        static void ConnectTimeout0(Bootstrap cb)
        {
            var handler = new TestHandler();
            cb.Handler(handler)
              .Option(ChannelOption.ConnectTimeout, TimeSpan.FromSeconds(2));

            IPAddress ipAddress = IPAddress.Parse("198.51.100.254");
            var badAddress = new IPEndPoint(ipAddress, 65535);
            Task<IChannel> task = cb.ConnectAsync(badAddress);
            var error = Assert.Throws<AggregateException>(() => task.Wait(DefaultTimeout));

            Assert.Single(error.InnerExceptions);
            Assert.IsType<ConnectTimeoutException>(error.InnerException);
            Assert.Equal(0, handler.Active);
            Assert.Null(handler.Error);
        }

        [Fact]
        public void ConnectRefused()
        {
            Bootstrap cb = new Bootstrap()
                .Group(this.group)
                .Channel<TcpChannel>();
            ConnectRefused0(cb);
        }

        static void ConnectRefused0(Bootstrap cb)
        {
            var handler = new TestHandler();
            cb.Handler(handler);
            var badAddress = new IPEndPoint(IPAddress.Loopback, UnassignedPort);
            Task<IChannel> task = cb.ConnectAsync(badAddress);
            var error = Assert.Throws<AggregateException>(() => task.Wait(DefaultTimeout));

            Assert.Single(error.InnerExceptions);
            Assert.IsType<ChannelException>(error.InnerException);
            var exception = (ChannelException)error.InnerException;
            Assert.IsType<OperationException>(exception.InnerException);
            var operationException = (OperationException)exception.InnerException;
            Assert.Equal(ErrorCode.ECONNREFUSED, operationException.ErrorCode);
            Assert.Equal(0, handler.Active);
            Assert.Null(handler.Error);
        }

        sealed class TestHandler : ChannelHandlerAdapter
        {
            public Exception Error { get; private set; }

            public int Active { get; private set; }

            public override void ChannelActive(IChannelHandlerContext context)
            {
                this.Active++;
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                this.Error = exception;
            }
        }

        public void Dispose()
        {
            this.group.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero).Wait(DefaultTimeout);
        }
    }
}
