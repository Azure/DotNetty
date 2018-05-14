// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System.Collections.Generic;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class HttpClientUpgradeHandlerTest
    {
        sealed class FakeSourceCodec : HttpClientUpgradeHandler.ISourceCodec
        {
            public void PrepareUpgradeFrom(IChannelHandlerContext ctx)
            {
                //NOP
            }

            public void UpgradeFrom(IChannelHandlerContext ctx)
            {
                //NOP
            }
        }

        sealed class FakeUpgradeCodec : HttpClientUpgradeHandler.IUpgradeCodec
        {
            public ICharSequence Protocol => new AsciiString("fancyhttp");

            public ICollection<ICharSequence> SetUpgradeHeaders(IChannelHandlerContext ctx, IHttpRequest upgradeRequest) => 
                new List<ICharSequence>();

            public void UpgradeTo(IChannelHandlerContext ctx, IFullHttpResponse upgradeResponse)
            {
                //NOP
            }
        }

        sealed class UserEventCatcher : ChannelHandlerAdapter
        {
            public object UserEvent { get; private set; }

            public override void UserEventTriggered(IChannelHandlerContext context, object evt) => 
                this.UserEvent = evt;
        }

        [Fact]
        public void SuccessfulUpgrade()
        {
            HttpClientUpgradeHandler.ISourceCodec sourceCodec = new FakeSourceCodec();
            HttpClientUpgradeHandler.IUpgradeCodec upgradeCodec = new FakeUpgradeCodec();
            var handler = new HttpClientUpgradeHandler(sourceCodec, upgradeCodec, 1024);
            var catcher = new UserEventCatcher();
            var channel = new EmbeddedChannel(catcher);
            channel.Pipeline.AddFirst("upgrade", handler);

            Assert.True(channel.WriteOutbound(new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "netty.io")));
            var request = channel.ReadOutbound<IFullHttpRequest>();

            Assert.Equal(2, request.Headers.Size);
            Assert.True(request.Headers.Contains(HttpHeaderNames.Upgrade, (AsciiString)"fancyhttp", false));
            Assert.True(request.Headers.Contains((AsciiString)"connection", (AsciiString)"upgrade", false));
            Assert.True(request.Release());
            Assert.Equal(HttpClientUpgradeHandler.UpgradeEvent.UpgradeIssued, catcher.UserEvent);

            var upgradeResponse = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.SwitchingProtocols);
            upgradeResponse.Headers.Add(HttpHeaderNames.Upgrade, (AsciiString)"fancyhttp");
            Assert.False(channel.WriteInbound(upgradeResponse));
            Assert.False(channel.WriteInbound(EmptyLastHttpContent.Default));

            Assert.Equal(HttpClientUpgradeHandler.UpgradeEvent.UpgradeSuccessful, catcher.UserEvent);
            Assert.Null(channel.Pipeline.Get("upgrade"));

            Assert.True(channel.WriteInbound(new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK)));
            var response = channel.ReadInbound<IFullHttpResponse>();
            Assert.Equal(HttpResponseStatus.OK, response.Status);
            Assert.True(response.Release());
            Assert.False(channel.Finish());
        }

        [Fact]
        public void UpgradeRejected()
        {
            HttpClientUpgradeHandler.ISourceCodec sourceCodec = new FakeSourceCodec();
            HttpClientUpgradeHandler.IUpgradeCodec upgradeCodec = new FakeUpgradeCodec();
            var handler = new HttpClientUpgradeHandler(sourceCodec, upgradeCodec, 1024);
            var catcher = new UserEventCatcher();
            var channel = new EmbeddedChannel(catcher);
            channel.Pipeline.AddFirst("upgrade", handler);

            Assert.True(channel.WriteOutbound(new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "netty.io")));
            var request = channel.ReadOutbound<IFullHttpRequest>();

            Assert.Equal(2, request.Headers.Size);
            Assert.True(request.Headers.Contains(HttpHeaderNames.Upgrade, (AsciiString)"fancyhttp", false));
            Assert.True(request.Headers.Contains((AsciiString)"connection", (AsciiString)"upgrade", false));
            Assert.True(request.Release());
            Assert.Equal(HttpClientUpgradeHandler.UpgradeEvent.UpgradeIssued, catcher.UserEvent);

            var upgradeResponse = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.SwitchingProtocols);
            upgradeResponse.Headers.Add(HttpHeaderNames.Upgrade, (AsciiString)"fancyhttp");
            Assert.True(channel.WriteInbound(new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK)));
            Assert.True(channel.WriteInbound(EmptyLastHttpContent.Default));

            Assert.Equal(HttpClientUpgradeHandler.UpgradeEvent.UpgradeRejected, catcher.UserEvent);
            Assert.Null(channel.Pipeline.Get("upgrade"));

            var response = channel.ReadInbound<IHttpResponse>();
            Assert.Equal(HttpResponseStatus.OK, response.Status);

            var last = channel.ReadInbound<ILastHttpContent>();
            Assert.Equal(EmptyLastHttpContent.Default, last);
            Assert.False(last.Release());
            Assert.False(channel.Finish());
        }

        [Fact]
        public void EarlyBailout()
        {
            HttpClientUpgradeHandler.ISourceCodec sourceCodec = new FakeSourceCodec();
            HttpClientUpgradeHandler.IUpgradeCodec upgradeCodec = new FakeUpgradeCodec();
            var handler = new HttpClientUpgradeHandler(sourceCodec, upgradeCodec, 1024);
            var catcher = new UserEventCatcher();
            var channel = new EmbeddedChannel(catcher);
            channel.Pipeline.AddFirst("upgrade", handler);

            Assert.True(channel.WriteOutbound(new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "netty.io")));
            var request = channel.ReadOutbound<IFullHttpRequest>();

            Assert.Equal(2, request.Headers.Size);
            Assert.True(request.Headers.Contains(HttpHeaderNames.Upgrade, (AsciiString)"fancyhttp", false));
            Assert.True(request.Headers.Contains((AsciiString)"connection", (AsciiString)"upgrade", false));
            Assert.True(request.Release());
            Assert.Equal(HttpClientUpgradeHandler.UpgradeEvent.UpgradeIssued, catcher.UserEvent);

            var upgradeResponse = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.SwitchingProtocols);
            upgradeResponse.Headers.Add(HttpHeaderNames.Upgrade, (AsciiString)"fancyhttp");
            Assert.True(channel.WriteInbound(new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK)));

            Assert.Equal(HttpClientUpgradeHandler.UpgradeEvent.UpgradeRejected, catcher.UserEvent);
            Assert.Null(channel.Pipeline.Get("upgrade"));

            var response = channel.ReadInbound<IHttpResponse>();
            Assert.Equal(HttpResponseStatus.OK, response.Status);
            Assert.False(channel.Finish());
        }
    }
}
