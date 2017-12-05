// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;
    using Xunit;
    using static TestUtil;

    [Collection(LibuvTransport)]
    public sealed class WriteBeforeRegisteredTests : IDisposable
    {
        readonly IEventLoopGroup group;
        IChannel clientChannel;

        public WriteBeforeRegisteredTests()
        {
            this.group = new EventLoopGroup(1);
        }

        [Fact]
        public void WriteBeforeConnect()
        {
            Bootstrap cb = new Bootstrap()
                .Group(this.group)
                .Channel<TcpChannel>();
            this.WriteBeforeConnect(cb);
        }

        void WriteBeforeConnect(Bootstrap cb)
        {
            var h = new TestHandler();
            cb.Handler(h);

            var task = (Task<IChannel>)cb.RegisterAsync();
            Assert.True(task.Wait(DefaultTimeout));
            this.clientChannel = task.Result;

            Task connectTask = this.clientChannel.ConnectAsync(LoopbackAnyPort);
            Task writeTask = this.clientChannel.WriteAndFlushAsync(Unpooled.WrappedBuffer(new byte[] { 1 }));
            var error = Assert.Throws<AggregateException>(() => writeTask.Wait(DefaultTimeout));

            Assert.Equal(1, error.InnerExceptions.Count);
            Assert.IsType<NotYetConnectedException>(error.InnerException);
            Assert.Null(h.Error);

            // Connect task should fail
            error = Assert.Throws<AggregateException>(() => connectTask.Wait(DefaultTimeout));
            Assert.Equal(1, error.InnerExceptions.Count);
            Assert.IsType<OperationException>(error.InnerException);
            var exception = (OperationException)error.InnerException;
            Assert.Equal("EADDRNOTAVAIL", exception.Name); // address not available (port : 0)
        }

        sealed class TestHandler : ChannelHandlerAdapter
        {
            internal Exception Error;

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception exception)
            {
                this.Error = exception;
            }
        }

        public void Dispose()
        {
            this.clientChannel?.CloseAsync().Wait(DefaultTimeout);
            this.group.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero).Wait(DefaultTimeout);
        }
    }
}
