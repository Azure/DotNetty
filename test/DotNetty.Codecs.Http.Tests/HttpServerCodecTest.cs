// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class HttpServerCodecTest
    {
        // Testcase for https://github.com/netty/netty/issues/433
        [Fact]
        public void UnfinishedChunkedHttpRequestIsLastFlag()
        {
            const int MaxChunkSize = 2000;
            var httpServerCodec = new HttpServerCodec(1000, 1000, MaxChunkSize);
            var ch = new EmbeddedChannel(httpServerCodec);

            int totalContentLength = MaxChunkSize * 5;
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(
                    "PUT /test HTTP/1.1\r\n" +
                    "Content-Length: " + totalContentLength + "\r\n" +
                    "\r\n")));

            int offeredContentLength = (int)(MaxChunkSize * 2.5);
            ch.WriteInbound(PrepareDataChunk(offeredContentLength));
            ch.Finish();

            var httpMessage = ch.ReadInbound<IHttpMessage>();
            Assert.NotNull(httpMessage);

            bool empty = true;
            int totalBytesPolled = 0;
            for (;;)
            {
                var httpChunk = ch.ReadInbound<IHttpContent>();
                if (httpChunk == null)
                {
                    break;
                }
                empty = false;
                totalBytesPolled += httpChunk.Content.ReadableBytes;
                Assert.False(httpChunk is ILastHttpContent);
                httpChunk.Release();
            }

            Assert.False(empty);
            Assert.Equal(offeredContentLength, totalBytesPolled);
        }

        [Fact]
        public void Code100Continue()
        {
            var ch = new EmbeddedChannel(new HttpServerCodec(), new HttpObjectAggregator(1024));

            // Send the request headers.
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(
                    "PUT /upload-large HTTP/1.1\r\n" +
                    "Expect: 100-continue\r\n" +
                    "Content-Length: 1\r\n\r\n")));

            // Ensure the aggregator generates nothing.
            var next = ch.ReadInbound<object>();
            Assert.Null(next);

            // Ensure the aggregator writes a 100 Continue response.
            var continueResponse = ch.ReadOutbound<IByteBuffer>();
            Assert.Equal("HTTP/1.1 100 Continue\r\n\r\n", continueResponse.ToString(Encoding.UTF8));
            continueResponse.Release();

            // But nothing more.
            next = ch.ReadInbound<object>();
            Assert.Null(next);

            // Send the content of the request.
            ch.WriteInbound(Unpooled.WrappedBuffer(new byte[] { 42 }));

            // Ensure the aggregator generates a full request.
            var req = ch.ReadInbound<IFullHttpRequest>();
            Assert.Equal("1", req.Headers.Get(HttpHeaderNames.ContentLength, null).ToString());
            Assert.Equal(1, req.Content.ReadableBytes);
            Assert.Equal((byte)42, req.Content.ReadByte());
            req.Release();

            // But nothing more.
            next = ch.ReadInbound<object>();
            Assert.Null(next);

            // Send the actual response.
            var res = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.Created);
            res.Content.WriteBytes(Encoding.UTF8.GetBytes("OK"));
            res.Headers.SetInt(HttpHeaderNames.ContentLength, 2);
            ch.WriteOutbound(res);

            // Ensure the encoder handles the response after handling 100 Continue.
            var encodedRes = ch.ReadOutbound<IByteBuffer>();
            Assert.Equal("HTTP/1.1 201 Created\r\n" + HttpHeaderNames.ContentLength + ": 2\r\n\r\nOK", encodedRes.ToString(Encoding.UTF8));
            encodedRes.Release();

            ch.Finish();
        }

        [Fact]
        public void ChunkedHeadResponse()
        {
            var ch = new EmbeddedChannel(new HttpServerCodec());

            // Send the request headers.
            Assert.True(ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(
                "HEAD / HTTP/1.1\r\n\r\n"))));

            var request = ch.ReadInbound<IHttpRequest>();
            Assert.Equal(HttpMethod.Head, request.Method);
            var content = ch.ReadInbound<ILastHttpContent>();
            Assert.False(content.Content.IsReadable());
            content.Release();

            var response = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            HttpUtil.SetTransferEncodingChunked(response, true);
            Assert.True(ch.WriteOutbound(response));
            Assert.True(ch.WriteOutbound(EmptyLastHttpContent.Default));
            Assert.True(ch.Finish());

            var buf = ch.ReadOutbound<IByteBuffer>();
            Assert.Equal("HTTP/1.1 200 OK\r\ntransfer-encoding: chunked\r\n\r\n", buf.ToString(Encoding.ASCII));
            buf.Release();

            buf = ch.ReadOutbound<IByteBuffer>();
            Assert.False(buf.IsReadable());
            buf.Release();

            Assert.False(ch.FinishAndReleaseAll());
        }

        [Fact]
        public void ChunkedHeadFullHttpResponse()
        {
            var ch = new EmbeddedChannel(new HttpServerCodec());

            // Send the request headers.
            Assert.True(ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(
                    "HEAD / HTTP/1.1\r\n\r\n"))));

            var request = ch.ReadInbound<IHttpRequest>();
            Assert.Equal(HttpMethod.Head, request.Method);
            var content = ch.ReadInbound<ILastHttpContent>();
            Assert.False(content.Content.IsReadable());
            content.Release();

            var response = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            HttpUtil.SetTransferEncodingChunked(response, true);
            Assert.True(ch.WriteOutbound(response));
            Assert.True(ch.Finish());

            var buf = ch.ReadOutbound<IByteBuffer>();
            Assert.Equal("HTTP/1.1 200 OK\r\ntransfer-encoding: chunked\r\n\r\n", buf.ToString(Encoding.ASCII));
            buf.Release();

            Assert.False(ch.FinishAndReleaseAll());
        }

        static IByteBuffer PrepareDataChunk(int size)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < size; ++i)
            {
                sb.Append('a');
            }

            return Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(sb.ToString()));
        }
    }
}
