// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class HttpObjectAggregatorTest
    {
        [Fact]
        public void Aggregate()
        {
            var aggregator = new HttpObjectAggregator(1024 * 1024);
            var ch = new EmbeddedChannel(aggregator);

            var message = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Get, "http://localhost");
            message.Headers.Set((AsciiString)"X-Test", true);
            IHttpContent chunk1 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test")));
            IHttpContent chunk2 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test2")));
            IHttpContent chunk3 = new DefaultLastHttpContent(Unpooled.Empty);

            Assert.False(ch.WriteInbound(message));
            Assert.False(ch.WriteInbound(chunk1));
            Assert.False(ch.WriteInbound(chunk2));

            // this should trigger a channelRead event so return true
            Assert.True(ch.WriteInbound(chunk3));
            Assert.True(ch.Finish());
            var aggregatedMessage = ch.ReadInbound<IFullHttpRequest>();
            Assert.NotNull(aggregatedMessage);

            Assert.Equal(chunk1.Content.ReadableBytes + chunk2.Content.ReadableBytes, HttpUtil.GetContentLength(aggregatedMessage));
            Assert.Equal(bool.TrueString, aggregatedMessage.Headers.Get((AsciiString)"X-Test", null)?.ToString());
            CheckContentBuffer(aggregatedMessage);
            var last = ch.ReadInbound<object>();
            Assert.Null(last);
        }

        [Fact]
        public void AggregateWithTrailer()
        {
            var aggregator = new HttpObjectAggregator(1024 * 1024);
            var ch = new EmbeddedChannel(aggregator);
            var message = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Get, "http://localhost");
            message.Headers.Set((AsciiString)"X-Test", true);
            HttpUtil.SetTransferEncodingChunked(message, true);
            var chunk1 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test")));
            var chunk2 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test2")));
            var trailer = new DefaultLastHttpContent();
            trailer.TrailingHeaders.Set((AsciiString)"X-Trailer", true);

            Assert.False(ch.WriteInbound(message));
            Assert.False(ch.WriteInbound(chunk1));
            Assert.False(ch.WriteInbound(chunk2));

            // this should trigger a channelRead event so return true
            Assert.True(ch.WriteInbound(trailer));
            Assert.True(ch.Finish());
            var aggregatedMessage = ch.ReadInbound<IFullHttpRequest>();
            Assert.NotNull(aggregatedMessage);

            Assert.Equal(chunk1.Content.ReadableBytes + chunk2.Content.ReadableBytes, HttpUtil.GetContentLength(aggregatedMessage));
            Assert.Equal(bool.TrueString, aggregatedMessage.Headers.Get((AsciiString)"X-Test", null)?.ToString());
            Assert.Equal(bool.TrueString, aggregatedMessage.TrailingHeaders.Get((AsciiString)"X-Trailer", null)?.ToString());
            CheckContentBuffer(aggregatedMessage);
            var last = ch.ReadInbound<object>();
            Assert.Null(last);
        }

        [Fact]
        public void OversizedRequest()
        {
            var aggregator = new HttpObjectAggregator(4);
            var ch = new EmbeddedChannel(aggregator);
            var message = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Put, "http://localhost");
            var chunk1 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test")));
            var chunk2 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test2")));
            EmptyLastHttpContent chunk3 = EmptyLastHttpContent.Default;

            Assert.False(ch.WriteInbound(message));
            Assert.False(ch.WriteInbound(chunk1));
            Assert.False(ch.WriteInbound(chunk2));

            var response = ch.ReadOutbound<IFullHttpResponse>();
            Assert.Equal(HttpResponseStatus.RequestEntityTooLarge, response.Status);
            Assert.Equal("0", response.Headers.Get(HttpHeaderNames.ContentLength, null));
            Assert.False(ch.Open);

            try
            {
                Assert.False(ch.WriteInbound(chunk3));
                Assert.True(false, "Shoud not get here, expecting exception thrown.");
            }
            catch (Exception e)
            {
                Assert.True(e is ClosedChannelException);
            }

            Assert.False(ch.Finish());
        }

        [Fact]
        public void OversizedRequestWithoutKeepAlive()
        {
            // send a HTTP/1.0 request with no keep-alive header
            var message = new DefaultHttpRequest(HttpVersion.Http10, HttpMethod.Put, "http://localhost");
            HttpUtil.SetContentLength(message, 5);
            CheckOversizedRequest(message);
        }

        [Fact]
        public void OversizedRequestWithContentLength()
        {
            var message = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Put, "http://localhost");
            HttpUtil.SetContentLength(message, 5);
            CheckOversizedRequest(message);
        }

        [Fact]
        public void OversizedResponse()
        {
            var aggregator = new HttpObjectAggregator(4);
            var ch = new EmbeddedChannel(aggregator);
            var message = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            var chunk1 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test")));
            var chunk2 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test2")));

            Assert.False(ch.WriteInbound(message));
            Assert.False(ch.WriteInbound(chunk1));
            Assert.Throws<TooLongFrameException>(() => ch.WriteInbound(chunk2));

            Assert.False(ch.Open);
            Assert.False(ch.Finish());
        }

        [Fact]
        public void InvalidConstructorUsage()
        {
            var error = Assert.Throws<ArgumentException>(() => new HttpObjectAggregator(-1));
            Assert.Equal("maxContentLength", error.ParamName);
        }

        [Fact]
        public void InvalidMaxCumulationBufferComponents()
        {
            var aggregator = new HttpObjectAggregator(int.MaxValue);
            Assert.Throws<ArgumentException>(() => aggregator.MaxCumulationBufferComponents = 1);
        }

        [Fact]
        public void SetMaxCumulationBufferComponentsAfterInit()
        {
            var aggregator = new HttpObjectAggregator(int.MaxValue);
            var ch = new EmbeddedChannel(aggregator);
            Assert.Throws<InvalidOperationException>(() => aggregator.MaxCumulationBufferComponents = 10);
            Assert.False(ch.Finish());
        }

        [Fact]
        public void AggregateTransferEncodingChunked()
        {
            var aggregator = new HttpObjectAggregator(1024 * 1024);
            var ch = new EmbeddedChannel(aggregator);

            var message = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Put, "http://localhost");
            message.Headers.Set((AsciiString)"X-Test", true);
            message.Headers.Set((AsciiString)"Transfer-Encoding", (AsciiString)"Chunked");
            var chunk1 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test")));
            var chunk2 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test2")));
            EmptyLastHttpContent chunk3 = EmptyLastHttpContent.Default;
            Assert.False(ch.WriteInbound(message));
            Assert.False(ch.WriteInbound(chunk1));
            Assert.False(ch.WriteInbound(chunk2));

            // this should trigger a channelRead event so return true
            Assert.True(ch.WriteInbound(chunk3));
            Assert.True(ch.Finish());
            var aggregatedMessage = ch.ReadInbound<IFullHttpRequest>();
            Assert.NotNull(aggregatedMessage);

            Assert.Equal(chunk1.Content.ReadableBytes + chunk2.Content.ReadableBytes, HttpUtil.GetContentLength(aggregatedMessage));
            Assert.Equal(bool.TrueString, aggregatedMessage.Headers.Get((AsciiString)"X-Test", null));
            CheckContentBuffer(aggregatedMessage);
            var last = ch.ReadInbound<object>();
            Assert.Null(last);
        }

        [Fact]
        public void BadRequest()
        {
            var ch = new EmbeddedChannel(new HttpRequestDecoder(), new HttpObjectAggregator(1024 * 1024));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("GET / HTTP/1.0 with extra\r\n")));
            var req = ch.ReadInbound<IFullHttpRequest>();
            Assert.NotNull(req);
            Assert.True(req.Result.IsFailure);
            var last = ch.ReadInbound<object>();
            Assert.Null(last);
            ch.Finish();
        }

        [Fact]
        public void BadResponse()
        {
            var ch = new EmbeddedChannel(new HttpResponseDecoder(), new HttpObjectAggregator(1024 * 1024));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("HTTP/1.0 BAD_CODE Bad Server\r\n")));
            var resp = ch.ReadInbound<IFullHttpResponse>();
            Assert.NotNull(resp);
            Assert.True(resp.Result.IsFailure);
            var last = ch.ReadInbound<object>();
            Assert.Null(last);
            ch.Finish();
        }

        [Fact]
        public void OversizedRequestWith100Continue()
        {
            var ch = new EmbeddedChannel(new HttpObjectAggregator(8));

            // Send an oversized request with 100 continue.
            var message = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Put, "http://localhost");
            HttpUtil.Set100ContinueExpected(message, true);
            HttpUtil.SetContentLength(message, 16);

            var chunk1 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("some")));
            var chunk2 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test")));
            EmptyLastHttpContent chunk3 = EmptyLastHttpContent.Default;

            // Send a request with 100-continue + large Content-Length header value.
            Assert.False(ch.WriteInbound(message));

            // The aggregator should respond with '413.'
            var response = ch.ReadOutbound<IFullHttpResponse>();
            Assert.Equal(HttpResponseStatus.RequestEntityTooLarge, response.Status);
            Assert.Equal((AsciiString)"0", response.Headers.Get(HttpHeaderNames.ContentLength, null));

            // An ill-behaving client could continue to send data without a respect, and such data should be discarded.
            Assert.False(ch.WriteInbound(chunk1));

            // The aggregator should not close the connection because keep-alive is on.
            Assert.True(ch.Open);

            // Now send a valid request.
            var message2 = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Put, "http://localhost");

            Assert.False(ch.WriteInbound(message2));
            Assert.False(ch.WriteInbound(chunk2));
            Assert.True(ch.WriteInbound(chunk3));

            var fullMsg = ch.ReadInbound<IFullHttpRequest>();
            Assert.NotNull(fullMsg);

            Assert.Equal(chunk2.Content.ReadableBytes + chunk3.Content.ReadableBytes, HttpUtil.GetContentLength(fullMsg));
            Assert.Equal(HttpUtil.GetContentLength(fullMsg), fullMsg.Content.ReadableBytes);

            fullMsg.Release();
            Assert.False(ch.Finish());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void UnsupportedExpectHeaderExpectation(bool close)
        {
            int maxContentLength = 4;
            var aggregator = new HttpObjectAggregator(maxContentLength, close);
            var ch = new EmbeddedChannel(new HttpRequestDecoder(), aggregator);

            Assert.False(ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(
                "GET / HTTP/1.1\r\n" +
                "Expect: chocolate=yummy\r\n" +
                "Content-Length: 100\r\n\r\n"))));
            var next = ch.ReadInbound<object>();
            Assert.Null(next);

            var response = ch.ReadOutbound<IFullHttpResponse>();
            Assert.Equal(HttpResponseStatus.ExpectationFailed, response.Status);
            Assert.Equal((AsciiString)"0", response.Headers.Get(HttpHeaderNames.ContentLength, null));
            response.Release();

            if (close)
            {
                Assert.False(ch.Open);
            }
            else
            {
                // keep-alive is on by default in HTTP/1.1, so the connection should be still alive
                Assert.True(ch.Open);

                // the decoder should be reset by the aggregator at this point and be able to decode the next request
                Assert.True(ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n\r\n"))));

                var request = ch.ReadInbound<IFullHttpRequest>();
                Assert.NotNull(request);
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal("/", request.Uri);
                Assert.Equal(0, request.Content.ReadableBytes);
                request.Release();
            }

            Assert.False(ch.Finish());
        }

        [Fact]
        public void OversizedRequestWith100ContinueAndDecoder()
        {
            var ch = new EmbeddedChannel(new HttpRequestDecoder(), new HttpObjectAggregator(4));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(
                    "PUT /upload HTTP/1.1\r\n" +
                            "Expect: 100-continue\r\n" +
                            "Content-Length: 100\r\n\r\n")));

            var next = ch.ReadInbound<object>();
            Assert.Null(next);

            var response = ch.ReadOutbound<IFullHttpResponse>();
            Assert.Equal(HttpResponseStatus.RequestEntityTooLarge, response.Status);
            Assert.Equal((AsciiString)"0", response.Headers.Get(HttpHeaderNames.ContentLength, null));

            // Keep-alive is on by default in HTTP/1.1, so the connection should be still alive.
            Assert.True(ch.Open);

            // The decoder should be reset by the aggregator at this point and be able to decode the next request.
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("GET /max-upload-size HTTP/1.1\r\n\r\n")));

            var request = ch.ReadInbound<IFullHttpRequest>();
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/max-upload-size", request.Uri);
            Assert.Equal(0, request.Content.ReadableBytes);
            request.Release();

            Assert.False(ch.Finish());
        }

        [Fact]
        public void OversizedRequestWith100ContinueAndDecoderCloseConnection()
        {
            var ch = new EmbeddedChannel(new HttpRequestDecoder(), new HttpObjectAggregator(4, true));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(
                    "PUT /upload HTTP/1.1\r\n" +
                            "Expect: 100-continue\r\n" +
                            "Content-Length: 100\r\n\r\n")));

            var next = ch.ReadInbound<object>();
            Assert.Null(next);

            var response = ch.ReadOutbound<IFullHttpResponse>();
            Assert.Equal(HttpResponseStatus.RequestEntityTooLarge, response.Status);
            Assert.Equal((AsciiString)"0", response.Headers.Get(HttpHeaderNames.ContentLength, null));

            // We are forcing the connection closed if an expectation is exceeded.
            Assert.False(ch.Open);
            Assert.False(ch.Finish());
        }

        [Fact]
        public void RequestAfterOversized100ContinueAndDecoder()
        {
            var ch = new EmbeddedChannel(new HttpRequestDecoder(), new HttpObjectAggregator(15));

            // Write first request with Expect: 100-continue.
            var message = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Put, "http://localhost");
            HttpUtil.Set100ContinueExpected(message, true);
            HttpUtil.SetContentLength(message, 16);

            var chunk1 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("some")));
            var chunk2 = new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("test")));
            EmptyLastHttpContent chunk3 = EmptyLastHttpContent.Default;

            // Send a request with 100-continue + large Content-Length header value.
            Assert.False(ch.WriteInbound(message));

            // The aggregator should respond with '413'.
            var response = ch.ReadOutbound<IFullHttpResponse>();
            Assert.Equal(HttpResponseStatus.RequestEntityTooLarge, response.Status);
            Assert.Equal((AsciiString)"0", response.Headers.Get(HttpHeaderNames.ContentLength, null));

            // An ill-behaving client could continue to send data without a respect, and such data should be discarded.
            Assert.False(ch.WriteInbound(chunk1));

            // The aggregator should not close the connection because keep-alive is on.
            Assert.True(ch.Open);

            // Now send a valid request.
            var message2 = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Put, "http://localhost");

            Assert.False(ch.WriteInbound(message2));
            Assert.False(ch.WriteInbound(chunk2));
            Assert.True(ch.WriteInbound(chunk3));

            var fullMsg = ch.ReadInbound<IFullHttpRequest>();
            Assert.NotNull(fullMsg);

            Assert.Equal(chunk2.Content.ReadableBytes + chunk3.Content.ReadableBytes,HttpUtil.GetContentLength(fullMsg));
            Assert.Equal(HttpUtil.GetContentLength(fullMsg), fullMsg.Content.ReadableBytes);

            fullMsg.Release();
            Assert.False(ch.Finish());
        }

        [Fact]
        public void ReplaceAggregatedRequest()
        {
            var ch = new EmbeddedChannel(new HttpObjectAggregator(1024 * 1024));

            var boom = new Exception("boom");
            var req = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Get, "http://localhost");
            req.Result = DecoderResult.Failure(boom);

            Assert.True(ch.WriteInbound(req) && ch.Finish());

            var aggregatedReq = ch.ReadInbound<IFullHttpRequest>();
            var replacedReq = (IFullHttpRequest)aggregatedReq.Replace(Unpooled.Empty);

            Assert.Equal(replacedReq.Result, aggregatedReq.Result);
            aggregatedReq.Release();
            replacedReq.Release();
        }

        [Fact]
        public void ReplaceAggregatedResponse()
        {
            var ch = new EmbeddedChannel(new HttpObjectAggregator(1024 * 1024));

            var boom = new Exception("boom");
            var rep = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            rep.Result = DecoderResult.Failure(boom);

            Assert.True(ch.WriteInbound(rep) && ch.Finish());

            var aggregatedRep = ch.ReadInbound<IFullHttpResponse>();
            var replacedRep = (IFullHttpResponse)aggregatedRep.Replace(Unpooled.Empty);

            Assert.Equal(replacedRep.Result, aggregatedRep.Result);
            aggregatedRep.Release();
            replacedRep.Release();
        }

        static void CheckOversizedRequest(IHttpRequest message)
        {
            var ch = new EmbeddedChannel(new HttpObjectAggregator(4));

            Assert.False(ch.WriteInbound(message));
            var response = ch.ReadOutbound<IHttpResponse>();
            Assert.Equal(HttpResponseStatus.RequestEntityTooLarge, response.Status);
            Assert.Equal("0", response.Headers.Get(HttpHeaderNames.ContentLength, null));

            if (ServerShouldCloseConnection(message, response))
            {
                Assert.False(ch.Open);
                Assert.False(ch.Finish());
            }
            else
            {
                Assert.True(ch.Open);
            }
        }

        static bool ServerShouldCloseConnection(IHttpRequest message, IHttpResponse response)
        {
            // If the response wasn't keep-alive, the server should close the connection.
            if (!HttpUtil.IsKeepAlive(response))
            {
                return true;
            }
            // The connection should only be kept open if Expect: 100-continue is set,
            // or if keep-alive is on.
            if (HttpUtil.Is100ContinueExpected(message))
            {
                return false;
            }
            if (HttpUtil.IsKeepAlive(message))
            {
                return false;
            }

            return true;
        }

        static void CheckContentBuffer(IFullHttpRequest aggregatedMessage)
        {
            var buffer = (CompositeByteBuffer)aggregatedMessage.Content;
            Assert.Equal(2, buffer.NumComponents);
            IList<IByteBuffer> buffers = buffer.Decompose(0, buffer.Capacity);
            Assert.Equal(2, buffers.Count);
            foreach (IByteBuffer buf in buffers)
            {
                // This should be false as we decompose the buffer before to not have deep hierarchy
                Assert.False(buf is CompositeByteBuffer);
            }
            aggregatedMessage.Release();
        }
    }
}
