// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class HttpServerUpgradeHandlerTest
    {
        sealed class TestUpgradeCodec : HttpServerUpgradeHandler.IUpgradeCodec
        {
            public ICollection<AsciiString> RequiredUpgradeHeaders => new List<AsciiString>();

            public bool PrepareUpgradeResponse(IChannelHandlerContext ctx, IFullHttpRequest upgradeRequest, HttpHeaders upgradeHeaders) => true;

            public void UpgradeTo(IChannelHandlerContext ctx, IFullHttpRequest upgradeRequest)
            {
                // Ensure that the HttpServerUpgradeHandler is still installed when this is called
                Assert.Equal(ctx.Channel.Pipeline.Context<HttpServerUpgradeHandler>(), ctx);
                Assert.NotNull(ctx.Channel.Pipeline.Get<HttpServerUpgradeHandler>());

                // Add a marker handler to signal that the upgrade has happened
                ctx.Channel.Pipeline.AddAfter(ctx.Name, "marker", new ChannelHandlerAdapter());
            }
        }

        sealed class UpgradeFactory : HttpServerUpgradeHandler.IUpgradeCodecFactory
        {
            public HttpServerUpgradeHandler.IUpgradeCodec NewUpgradeCodec(ICharSequence protocol) =>
                new TestUpgradeCodec();
        }

        sealed class ChannelHandler : ChannelDuplexHandler
        {
            // marker boolean to signal that we're in the `channelRead` method
            bool inReadCall;
            bool writeUpgradeMessage;
            bool writeFlushed;

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                Assert.False(this.inReadCall);
                Assert.False(this.writeUpgradeMessage);

                this.inReadCall = true;
                try
                {
                    base.ChannelRead(ctx, msg);
                    // All in the same call stack, the upgrade codec should receive the message,
                    // written the upgrade response, and upgraded the pipeline.
                    Assert.True(this.writeUpgradeMessage);
                    Assert.False(this.writeFlushed);
                    //Assert.Null(ctx.Channel.Pipeline.Get<HttpServerCodec>());
                    //Assert.NotNull(ctx.Channel.Pipeline.Get("marker"));
                }
                finally
                {
                    this.inReadCall = false;
                }
            }

            public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                // We ensure that we're in the read call and defer the write so we can
                // make sure the pipeline was reformed irrespective of the flush completing.
                Assert.True(this.inReadCall);
                this.writeUpgradeMessage = true;

                var completion = new TaskCompletionSource();
                ctx.Channel.EventLoop.Execute(() =>
                {
                    ctx.WriteAsync(msg)
                       .ContinueWith(t =>
                        {
                            if (t.Status == TaskStatus.RanToCompletion)
                            {
                                this.writeFlushed = true;
                                completion.TryComplete();
                                return;
                            }
                            completion.TrySetException(new InvalidOperationException($"Invalid WriteAsync task status {t.Status}"));
                        },
                        TaskContinuationOptions.ExecuteSynchronously);
                });
                return completion.Task;
            }
        }

        [Fact]
        public void UpgradesPipelineInSameMethodInvocation()
        {
            var httpServerCodec = new HttpServerCodec();
            var factory = new UpgradeFactory();
            var testInStackFrame = new ChannelHandler();

            var upgradeHandler = new HttpServerUpgradeHandler(httpServerCodec, factory);
            var channel = new EmbeddedChannel(testInStackFrame, httpServerCodec, upgradeHandler);

            const string UpgradeString = "GET / HTTP/1.1\r\n" +
                "Host: example.com\r\n" +
                "Connection: Upgrade, HTTP2-Settings\r\n" +
                "Upgrade: nextprotocol\r\n" +
                "HTTP2-Settings: AAMAAABkAAQAAP__\r\n\r\n";
            IByteBuffer upgrade = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(UpgradeString));

            Assert.False(channel.WriteInbound(upgrade));
            //Assert.Null(channel.Pipeline.Get<HttpServerCodec>());
            //Assert.NotNull(channel.Pipeline.Get("marker"));

            channel.Flush();
            Assert.Null(channel.Pipeline.Get<HttpServerCodec>());
            Assert.NotNull(channel.Pipeline.Get("marker"));

            var upgradeMessage = channel.ReadOutbound<IByteBuffer>();
            const string ExpectedHttpResponse = "HTTP/1.1 101 Switching Protocols\r\n" +
                "connection: upgrade\r\n" +
                "upgrade: nextprotocol\r\n\r\n";
            Assert.Equal(ExpectedHttpResponse, upgradeMessage.ToString(Encoding.ASCII));
            Assert.True(upgradeMessage.Release());
            Assert.False(channel.FinishAndReleaseAll());
        }
    }
}
