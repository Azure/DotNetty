// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class HttpServerExpectContinueHandlerTest
    {
        sealed class ContinueHandler : HttpServerExpectContinueHandler
        {
            protected override IHttpResponse AcceptMessage(IHttpRequest request)
            {
                var response = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.Continue);
                response.Headers.Set((AsciiString)"foo", (AsciiString)"bar");
                return response;
            }
        }

        [Fact]
        public void ShouldRespondToExpectedHeader()
        {
            var channel = new EmbeddedChannel(new ContinueHandler());
            var request = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/");
            HttpUtil.Set100ContinueExpected(request, true);

            channel.WriteInbound(request);
            var response = channel.ReadOutbound<IHttpResponse>();

            Assert.Equal(HttpResponseStatus.Continue, response.Status);
            Assert.Equal((AsciiString)"bar", response.Headers.Get((AsciiString)"foo", null));
            ReferenceCountUtil.Release(response);

            var processedRequest = channel.ReadInbound<IHttpRequest>();
            Assert.NotNull(processedRequest);
            Assert.False(processedRequest.Headers.Contains(HttpHeaderNames.Expect));
            ReferenceCountUtil.Release(processedRequest);
            Assert.False(channel.FinishAndReleaseAll());
        }

        sealed class CustomHandler : HttpServerExpectContinueHandler
        {
            protected override IHttpResponse AcceptMessage(IHttpRequest request) => null;

            protected override IHttpResponse RejectResponse(IHttpRequest request) => 
                new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.RequestEntityTooLarge);
        }

        [Fact]
        public void ShouldAllowCustomResponses()
        {
            var channel = new EmbeddedChannel(new CustomHandler());

            var request = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/");
            HttpUtil.Set100ContinueExpected(request, true);

            channel.WriteInbound(request);
            var response = channel.ReadOutbound<IHttpResponse>();

            Assert.Equal(HttpResponseStatus.RequestEntityTooLarge, response.Status);
            ReferenceCountUtil.Release(response);

            // request was swallowed
            Assert.Empty(channel.InboundMessages);
            Assert.False(channel.FinishAndReleaseAll());
        }
    }
}
