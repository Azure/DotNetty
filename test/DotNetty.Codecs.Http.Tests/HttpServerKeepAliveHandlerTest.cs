// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    using static HttpResponseStatus;

    public sealed class HttpServerKeepAliveHandlerTest
    {
        const string RequestKeepAlive = "REQUEST_KEEP_ALIVE";
        const int NotSelfDefinedMsgLength = 0;
        const int SetResponseLength = 1;
        const int SetMultipart = 2;
        const int SetChunked = 4;

        public static IEnumerable<object[]> GetKeepAliveCases() => new[]
        {
            new object[] { true, HttpVersion.Http10, OK, RequestKeepAlive, SetResponseLength, HttpHeaderValues.KeepAlive }, //  0
            new object[] { true, HttpVersion.Http10, OK, RequestKeepAlive, SetMultipart, HttpHeaderValues.KeepAlive },      //  1
            new object[] { false, HttpVersion.Http10, OK, null, SetResponseLength, null },                                  //  2
            new object[] { true, HttpVersion.Http11, OK, RequestKeepAlive, SetResponseLength, null },                       //  3
            new object[] { false, HttpVersion.Http11, OK, RequestKeepAlive, SetResponseLength, HttpHeaderValues.Close },    //  4
            new object[] { true, HttpVersion.Http11, OK, RequestKeepAlive, SetMultipart, null },                            //  5
            new object[] { true, HttpVersion.Http11, OK, RequestKeepAlive, SetChunked, null },                              //  6
            new object[] { false, HttpVersion.Http11, OK, null, SetResponseLength, null },                                  //  7
            new object[] { false, HttpVersion.Http10, OK, RequestKeepAlive, NotSelfDefinedMsgLength, null },                //  8
            new object[] { false, HttpVersion.Http10, OK, null, NotSelfDefinedMsgLength, null },                            //  9
            new object[] { false, HttpVersion.Http11, OK, RequestKeepAlive, NotSelfDefinedMsgLength, null },                // 10
            new object[] { false, HttpVersion.Http11, OK, null, NotSelfDefinedMsgLength, null },                            // 11
            new object[] { false, HttpVersion.Http10, OK, RequestKeepAlive, SetResponseLength, null },                      // 12
            new object[] { true, HttpVersion.Http11, NoContent, RequestKeepAlive, NotSelfDefinedMsgLength, null},           // 13
            new object[] { false, HttpVersion.Http10, NoContent, null, NotSelfDefinedMsgLength, null}                       // 14
        };

        [Theory]
        [MemberData(nameof(GetKeepAliveCases))]
        public void KeepAlive(bool isKeepAliveResponseExpected, HttpVersion httpVersion, HttpResponseStatus responseStatus, string sendKeepAlive, int setSelfDefinedMessageLength, ICharSequence setResponseConnection)
        {
            var channel = new EmbeddedChannel(new HttpServerKeepAliveHandler());
            var request = new DefaultFullHttpRequest(httpVersion, HttpMethod.Get, "/v1/foo/bar");
            HttpUtil.SetKeepAlive(request, RequestKeepAlive.Equals(sendKeepAlive));
            var response = new DefaultFullHttpResponse(httpVersion, responseStatus);
            if (!CharUtil.IsNullOrEmpty(setResponseConnection))
            {
                response.Headers.Set(HttpHeaderNames.Connection, setResponseConnection);
            }
            SetupMessageLength(setSelfDefinedMessageLength, response);

            Assert.True(channel.WriteInbound(request));
            var requestForwarded = channel.ReadInbound<object>();
            Assert.Equal(request, requestForwarded);
            ReferenceCountUtil.Release(requestForwarded);
            channel.WriteAndFlushAsync(response).Wait(TimeSpan.FromSeconds(1));
            var writtenResponse = channel.ReadOutbound<IHttpResponse>();

            Assert.Equal(isKeepAliveResponseExpected, channel.Open);
            Assert.Equal(isKeepAliveResponseExpected, HttpUtil.IsKeepAlive(writtenResponse));
            ReferenceCountUtil.Release(writtenResponse);
            Assert.False(channel.FinishAndReleaseAll());
        }

        [Theory]
        [MemberData(nameof(GetKeepAliveCases))]
#pragma warning disable xUnit1026 // Theory methods should use all of their parameters
        public void ConnectionCloseHeaderHandledCorrectly(bool isKeepAliveResponseExpected, HttpVersion httpVersion, HttpResponseStatus responseStatus, string sendKeepAlive, int setSelfDefinedMessageLength, ICharSequence setResponseConnection)
#pragma warning restore xUnit1026 // Theory methods should use all of their parameters
        {
            var channel = new EmbeddedChannel(new HttpServerKeepAliveHandler());
            var response = new DefaultFullHttpResponse(httpVersion, responseStatus);
            response.Headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.Close);
            SetupMessageLength(setSelfDefinedMessageLength, response);

            channel.WriteAndFlushAsync(response).Wait(TimeSpan.FromSeconds(1));
            var writtenResponse = channel.ReadOutbound<IHttpResponse>();

            Assert.False(channel.Open);
            ReferenceCountUtil.Release(writtenResponse);
            Assert.False(channel.FinishAndReleaseAll());
        }

        [Theory]
        [MemberData(nameof(GetKeepAliveCases))]
        public void PipelineKeepAlive(bool isKeepAliveResponseExpected, HttpVersion httpVersion, HttpResponseStatus responseStatus, string sendKeepAlive, int setSelfDefinedMessageLength, ICharSequence setResponseConnection)
        {
            var channel = new EmbeddedChannel(new HttpServerKeepAliveHandler());
            var firstRequest = new DefaultFullHttpRequest(httpVersion, HttpMethod.Get, "/v1/foo/bar");
            HttpUtil.SetKeepAlive(firstRequest, true);
            var secondRequest = new DefaultFullHttpRequest(httpVersion, HttpMethod.Get, "/v1/foo/bar");
            HttpUtil.SetKeepAlive(secondRequest, RequestKeepAlive.Equals(sendKeepAlive));
            var finalRequest = new DefaultFullHttpRequest(httpVersion, HttpMethod.Get, "/v1/foo/bar");
            HttpUtil.SetKeepAlive(finalRequest, false);
            var response = new DefaultFullHttpResponse(httpVersion, responseStatus);
            var informationalResp = new DefaultFullHttpResponse(httpVersion, Processing);
            HttpUtil.SetKeepAlive(response, true);
            HttpUtil.SetContentLength(response, 0);
            HttpUtil.SetKeepAlive(informationalResp, true);

            Assert.True(channel.WriteInbound(firstRequest, secondRequest, finalRequest));

            var requestForwarded = channel.ReadInbound<object>();
            Assert.Equal(firstRequest, requestForwarded);
            ReferenceCountUtil.Release(requestForwarded);

            channel.WriteAndFlushAsync(response.Duplicate().Retain()).Wait(TimeSpan.FromSeconds(1));
            var firstResponse = channel.ReadOutbound<IHttpResponse>();
            Assert.True(channel.Open);
            Assert.True(HttpUtil.IsKeepAlive(firstResponse));
            ReferenceCountUtil.Release(firstResponse);

            requestForwarded = channel.ReadInbound<object>();
            Assert.Equal(secondRequest, requestForwarded);
            ReferenceCountUtil.Release(requestForwarded);

            channel.WriteAndFlushAsync(informationalResp).Wait(TimeSpan.FromSeconds(1));
            var writtenInfoResp = channel.ReadOutbound<IHttpResponse>();
            Assert.True(channel.Open);
            Assert.True(HttpUtil.IsKeepAlive(writtenInfoResp));
            ReferenceCountUtil.Release(writtenInfoResp);

            if (!CharUtil.IsNullOrEmpty(setResponseConnection))
            {
                response.Headers.Set(HttpHeaderNames.Connection, setResponseConnection);
            }
            else
            {
                response.Headers.Remove(HttpHeaderNames.Connection);
            }
            SetupMessageLength(setSelfDefinedMessageLength, response);
            channel.WriteAndFlushAsync(response.Duplicate().Retain()).Wait(TimeSpan.FromSeconds(1));
            var secondResponse = channel.ReadOutbound<IHttpResponse>();
            Assert.Equal(isKeepAliveResponseExpected, channel.Open);
            Assert.Equal(isKeepAliveResponseExpected, HttpUtil.IsKeepAlive(secondResponse));
            ReferenceCountUtil.Release(secondResponse);

            requestForwarded = channel.ReadInbound<object>();
            Assert.Equal(finalRequest, requestForwarded);
            ReferenceCountUtil.Release(requestForwarded);

            if (isKeepAliveResponseExpected)
            {
                channel.WriteAndFlushAsync(response).Wait(TimeSpan.FromSeconds(1));
                var finalResponse = channel.ReadOutbound<IHttpResponse>();
                Assert.False(channel.Open);
                Assert.False(HttpUtil.IsKeepAlive(finalResponse));
            }
            ReferenceCountUtil.Release(response);
            Assert.False(channel.FinishAndReleaseAll());
        }

        static void SetupMessageLength(int setSelfDefinedMessageLength, IHttpResponse response)
        {
            switch (setSelfDefinedMessageLength)
            {
                case NotSelfDefinedMsgLength:
                    if (HttpUtil.IsContentLengthSet(response))
                    {
                        response.Headers.Remove(HttpHeaderNames.ContentLength);
                    }
                    break;
                case SetResponseLength:
                    HttpUtil.SetContentLength(response, 0);
                    break;
                case SetChunked:
                    HttpUtil.SetTransferEncodingChunked(response, true);
                    break;
                case SetMultipart:
                    response.Headers.Set(HttpHeaderNames.ContentType, HttpHeaderValues.MultipartMixed);
                    break;
                default:
                    throw new ArgumentException($"Unknown selfDefinedMessageLength: {setSelfDefinedMessageLength}");
            }
        }
    }
}
