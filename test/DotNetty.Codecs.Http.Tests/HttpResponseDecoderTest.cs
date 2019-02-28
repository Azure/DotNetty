// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class HttpResponseDecoderTest
    {
        // The size of headers should be calculated correctly even if a single header is split into multiple fragments.
        // see <a href="https://github.com/netty/netty/issues/3445">#3445</a>
        [Fact]
        public void MaxHeaderSize1()
        {
            const int MaxHeaderSize = 8192;

            var ch = new EmbeddedChannel(new HttpResponseDecoder(4096, MaxHeaderSize, 8192));
            var bytes = new byte[MaxHeaderSize / 2 - 2];
            bytes.Fill((byte)'a');

            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n")));

            // Write two 4096-byte headers (= 8192 bytes)
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("A:")));
            ch.WriteInbound(Unpooled.CopiedBuffer(bytes));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("\r\n")));
            Assert.Null(ch.ReadInbound<IByteBuffer>());
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("B:")));
            ch.WriteInbound(Unpooled.CopiedBuffer(bytes));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("\r\n")));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("\r\n")));

            var res = ch.ReadInbound<IHttpResponse>();
            Assert.Null(res.Result.Cause);
            Assert.True(res.Result.IsSuccess);

            Assert.Null(ch.ReadInbound<IHttpResponse>());
            Assert.True(ch.Finish());

            var last = ch.ReadInbound<ILastHttpContent>();
            Assert.NotNull(last);
        }

        // Complementary test case of {@link #testMaxHeaderSize1()} When it actually exceeds the maximum, it should fail.
        [Fact]
        public void MaxHeaderSize2()
        {
            const int MaxHeaderSize = 8192;

            var ch = new EmbeddedChannel(new HttpResponseDecoder(4096, MaxHeaderSize, 8192));
            var bytes = new byte[MaxHeaderSize / 2 - 2];
            bytes.Fill((byte)'a');

            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n")));

            // Write a 4096-byte header and a 4097-byte header to test an off-by-one case (= 8193 bytes)
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("A:")));
            ch.WriteInbound(Unpooled.CopiedBuffer(bytes));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("\r\n")));
            Assert.Null(ch.ReadInbound<IByteBuffer>());
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("B: "))); // Note an extra space.
            ch.WriteInbound(Unpooled.CopiedBuffer(bytes));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("\r\n")));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("\r\n")));

            var res = ch.ReadInbound<IHttpResponse>();
            Assert.True(res.Result.Cause is TooLongFrameException);

            Assert.False(ch.Finish());

            var last = ch.ReadInbound<IByteBufferHolder>();
            Assert.Null(last);
        }

        [Fact]
        public void ResponseChunked()
        {
            var ch = new EmbeddedChannel(new HttpResponseDecoder());
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n")));

            var res = ch.ReadInbound<IHttpResponse>();
            Assert.Same(HttpVersion.Http11, res.ProtocolVersion);
            Assert.Equal(HttpResponseStatus.OK, res.Status);

            var data = new byte[64];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)i;
            }

            for (int i = 0; i < 10; i++)
            {
                Assert.False(ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes($"{Convert.ToString(data.Length, 16)}\r\n"))));
                Assert.True(ch.WriteInbound(Unpooled.WrappedBuffer(data)));
                var content = ch.ReadInbound<IHttpContent>();
                Assert.Equal(data.Length, content.Content.ReadableBytes);

                var decodedData = new byte[data.Length];
                content.Content.ReadBytes(decodedData);
                Assert.True(data.SequenceEqual(decodedData));
                content.Release();

                Assert.False(ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("\r\n"))));
            }

            // Write the last chunk.
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("0\r\n\r\n")));

            // Ensure the last chunk was decoded.
            var lastContent = ch.ReadInbound<ILastHttpContent>();
            Assert.False(lastContent.Content.IsReadable());
            lastContent.Release();

            ch.Finish();

            var last = ch.ReadInbound<IByteBufferHolder>();
            Assert.Null(last);
        }

        [Fact]
        public void ResponseChunkedExceedMaxChunkSize()
        {
            var ch = new EmbeddedChannel(new HttpResponseDecoder(4096, 8192, 32));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n")));

            var res = ch.ReadInbound<IHttpResponse>();
            Assert.Same(HttpVersion.Http11, res.ProtocolVersion);
            Assert.Equal(HttpResponseStatus.OK, res.Status);

            var data = new byte[64];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)i;
            }

            for (int i = 0; i < 10; i++)
            {
                Assert.False(ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes($"{Convert.ToString(data.Length, 16)}\r\n"))));
                Assert.True(ch.WriteInbound(Unpooled.WrappedBuffer(data)));

                var decodedData = new byte[data.Length];
                var content = ch.ReadInbound<IHttpContent>();
                Assert.Equal(32, content.Content.ReadableBytes);
                content.Content.ReadBytes(decodedData, 0, 32);
                content.Release();

                content = ch.ReadInbound<IHttpContent>();
                Assert.NotNull(content);
                Assert.Equal(32, content.Content.ReadableBytes);

                content.Content.ReadBytes(decodedData, 32, 32);

                Assert.True(decodedData.SequenceEqual(data));
                content.Release();

                Assert.False(ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("\r\n"))));
            }

            // Write the last chunk.
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("0\r\n\r\n")));

            // Ensure the last chunk was decoded.
            var lastContent = ch.ReadInbound<ILastHttpContent>();
            Assert.False(lastContent.Content.IsReadable());
            lastContent.Release();

            ch.Finish();

            var last = ch.ReadInbound<IByteBufferHolder>();
            Assert.Null(last);
        }

        [Fact]
        public void ClosureWithoutContentLength1()
        {
            var ch = new EmbeddedChannel(new HttpResponseDecoder());
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n\r\n")));

            // Read the response headers.
            var res = ch.ReadInbound<IHttpResponse>();
            Assert.Same(HttpVersion.Http11, res.ProtocolVersion);
            Assert.Equal(HttpResponseStatus.OK, res.Status);

            res = ch.ReadInbound<IHttpResponse>();
            Assert.Null(res);

            // Close the connection without sending anything.
            Assert.True(ch.Finish());

            // The decoder should still produce the last content.
            var content = ch.ReadInbound<ILastHttpContent>();
            Assert.NotNull(content);
            Assert.False(content.Content.IsReadable());
            content.Release();

            // But nothing more.
            var last = ch.ReadInbound<IByteBufferHolder>();
            Assert.Null(last);
        }

        [Fact]
        public void ClosureWithoutContentLength2()
        {
            var ch = new EmbeddedChannel(new HttpResponseDecoder());

            // Write the partial response.
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n\r\n12345678")));

            // Read the response headers.
            var res = ch.ReadInbound<IHttpResponse>();
            Assert.Same(HttpVersion.Http11, res.ProtocolVersion);
            Assert.Equal(HttpResponseStatus.OK, res.Status);

            // Read the partial content.
            var content = ch.ReadInbound<IHttpContent>();
            Assert.Equal("12345678", content.Content.ToString(Encoding.ASCII));
            Assert.Null(content as ILastHttpContent);
            content.Release();

            res = ch.ReadInbound<IHttpResponse>();
            Assert.Null(res);

            // Close the connection.
            Assert.True(ch.Finish());

            // The decoder should still produce the last content.
            var lastContent = ch.ReadInbound<ILastHttpContent>();
            Assert.NotNull(lastContent);
            Assert.False(lastContent.Content.IsReadable());
            lastContent.Release();

            // But nothing more.
            var last = ch.ReadInbound<IByteBufferHolder>();
            Assert.Null(last);
        }

        [Fact]
        public void PrematureClosureWithChunkedEncoding1()
        {
            var ch = new EmbeddedChannel(new HttpResponseDecoder());
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n")));

            // Read the response headers.
            var res = ch.ReadInbound<IHttpResponse>();
            Assert.Same(HttpVersion.Http11, res.ProtocolVersion);
            Assert.Equal(HttpResponseStatus.OK, res.Status);
            Assert.Equal("chunked", res.Headers.Get(HttpHeaderNames.TransferEncoding, null).ToString());
            res = ch.ReadInbound<IHttpResponse>();
            Assert.Null(res);

            // Close the connection without sending anything.
            ch.Finish();

            // The decoder should not generate the last chunk because it's closed prematurely.
            var last = ch.ReadInbound<IByteBufferHolder>();
            Assert.Null(last);
        }

        [Fact]
        public void PrematureClosureWithChunkedEncoding2()
        {
            var ch = new EmbeddedChannel(new HttpResponseDecoder());

            // Write the partial response.
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n8\r\n12345678")));

            // Read the response headers.
            var res = ch.ReadInbound<IHttpResponse>();
            Assert.Same(HttpVersion.Http11, res.ProtocolVersion);
            Assert.Equal(HttpResponseStatus.OK, res.Status);
            Assert.Equal("chunked", res.Headers.Get(HttpHeaderNames.TransferEncoding, null).ToString());

            // Read the partial content.
            var content = ch.ReadInbound<IHttpContent>();
            Assert.Equal("12345678", content.Content.ToString(Encoding.ASCII));
            Assert.Null(content as ILastHttpContent);
            content.Release();

            content = ch.ReadInbound<IHttpContent>();
            Assert.Null(content);

            // Close the connection.
            ch.Finish();

            // The decoder should not generate the last chunk because it's closed prematurely.
            var last = ch.ReadInbound<IByteBufferHolder>();
            Assert.Null(last);
        }

        [Fact]
        public void LastResponseWithEmptyHeaderAndEmptyContent()
        {
            var ch = new EmbeddedChannel(new HttpResponseDecoder());
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n\r\n")));

            var res = ch.ReadInbound<IHttpResponse>();
            Assert.Same(HttpVersion.Http11, res.ProtocolVersion);
            Assert.Equal(HttpResponseStatus.OK, res.Status);

            var content = ch.ReadInbound<IHttpContent>();
            Assert.Null(content);

            Assert.True(ch.Finish());

            var lastContent = ch.ReadInbound<ILastHttpContent>();
            Assert.False(lastContent.Content.IsReadable());
            lastContent.Release();

            var last = ch.ReadInbound<IByteBufferHolder>();
            Assert.Null(last);
        }

        [Fact]
        public void LastResponseWithoutContentLengthHeader()
        {
            var ch = new EmbeddedChannel(new HttpResponseDecoder());
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n\r\n")));

            var res = ch.ReadInbound<IHttpResponse>();
            Assert.Same(HttpVersion.Http11, res.ProtocolVersion);
            Assert.Equal(HttpResponseStatus.OK, res.Status);
            var content = ch.ReadInbound<IHttpContent>();
            Assert.Null(content);

            ch.WriteInbound(Unpooled.WrappedBuffer(new byte[1024]));
            content = ch.ReadInbound<IHttpContent>();
            Assert.Equal(1024, content.Content.ReadableBytes);
            content.Release();

            Assert.True(ch.Finish());

            var lastContent = ch.ReadInbound<ILastHttpContent>();
            Assert.False(lastContent.Content.IsReadable());
            lastContent.Release();

            var last = ch.ReadInbound<IByteBufferHolder>();
            Assert.Null(last);
        }

        [Fact]
        public void LastResponseWithHeaderRemoveTrailingSpaces()
        {
            var ch = new EmbeddedChannel(new HttpResponseDecoder());
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nX-Header: h2=h2v2; Expires=Wed, 09-Jun-2021 10:18:14 GMT       \r\n\r\n")));

            var res = ch.ReadInbound<IHttpResponse>();
            Assert.Same(HttpVersion.Http11, res.ProtocolVersion);
            Assert.Equal(HttpResponseStatus.OK, res.Status);
            Assert.Equal("h2=h2v2; Expires=Wed, 09-Jun-2021 10:18:14 GMT", res.Headers.Get((AsciiString)"X-Header", null).ToString());
            var content = ch.ReadInbound<IHttpContent>();
            Assert.Null(content);

            ch.WriteInbound(Unpooled.WrappedBuffer(new byte[1024]));
            content = ch.ReadInbound<IHttpContent>();
            Assert.Equal(1024, content.Content.ReadableBytes);
            content.Release();

            Assert.True(ch.Finish());

            var lastContent = ch.ReadInbound<ILastHttpContent>();
            Assert.False(lastContent.Content.IsReadable());
            lastContent.Release();

            var last = ch.ReadInbound<IByteBufferHolder>();
            Assert.Null(last);
        }

        [Fact]
        public void ResetContentResponseWithTransferEncoding()
        {
            var ch = new EmbeddedChannel(new HttpResponseDecoder());
            Assert.True(ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(
                "HTTP/1.1 205 Reset Content\r\n" +
                "Transfer-Encoding: chunked\r\n" +
                "\r\n" +
                "0\r\n" +
                "\r\n"))));

            var res = ch.ReadInbound<IHttpResponse>();
            Assert.Same(HttpVersion.Http11, res.ProtocolVersion);
            Assert.Equal(HttpResponseStatus.ResetContent, res.Status);

            var lastContent = ch.ReadInbound<ILastHttpContent>();
            Assert.False(lastContent.Content.IsReadable());
            lastContent.Release();

            Assert.False(ch.Finish());
        }

        [Fact]
        public void LastResponseWithTrailingHeader()
        {
            var ch = new EmbeddedChannel(new HttpResponseDecoder());
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\n" +
                "Transfer-Encoding: chunked\r\n" +
                "\r\n" +
                "0\r\n" +
                "Set-Cookie: t1=t1v1\r\n" +
                "Set-Cookie: t2=t2v2; Expires=Wed, 09-Jun-2021 10:18:14 GMT\r\n" +
                "\r\n")));

            var res = ch.ReadInbound<IHttpResponse>();
            Assert.Same(HttpVersion.Http11, res.ProtocolVersion);
            Assert.Equal(HttpResponseStatus.OK, res.Status);

            var lastContent = ch.ReadInbound<ILastHttpContent>();
            Assert.False(lastContent.Content.IsReadable());
            HttpHeaders headers = lastContent.TrailingHeaders;
            Assert.Equal(1, headers.Names().Count);
            IList<ICharSequence> values = headers.GetAll((AsciiString)"Set-Cookie");
            Assert.Equal(2, values.Count);
            Assert.True(values.Contains((AsciiString)"t1=t1v1"));
            Assert.True(values.Contains((AsciiString)"t2=t2v2; Expires=Wed, 09-Jun-2021 10:18:14 GMT"));
            lastContent.Release();

            Assert.False(ch.Finish());
            var last = ch.ReadInbound<IByteBufferHolder>();
            Assert.Null(last);
        }

        [Fact]
        public void LastResponseWithTrailingHeaderFragmented()
        {
            byte[] data = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n" +
                "Transfer-Encoding: chunked\r\n" +
                "\r\n" +
                "0\r\n" +
                "Set-Cookie: t1=t1v1\r\n" +
                "Set-Cookie: t2=t2v2; Expires=Wed, 09-Jun-2021 10:18:14 GMT\r\n" +
                "\r\n");

            for (int i = 1; i < data.Length; i++)
            {
                LastResponseWithTrailingHeaderFragmented0(data, i);
            }
        }

        static void LastResponseWithTrailingHeaderFragmented0(byte[] content, int fragmentSize)
        {
            var ch = new EmbeddedChannel(new HttpResponseDecoder());
            const int HeaderLength = 47;

            // split up the header
            for (int a = 0; a < HeaderLength;)
            {
                int amount = fragmentSize;
                if (a + amount > HeaderLength)
                {
                    amount = HeaderLength - a;
                }

                // if header is done it should produce a HttpRequest
                bool headerDone = a + amount == HeaderLength;
                Assert.Equal(headerDone, ch.WriteInbound(Unpooled.WrappedBuffer(content, a, amount)));
                a += amount;
            }

            ch.WriteInbound(Unpooled.WrappedBuffer(content, HeaderLength, content.Length - HeaderLength));
            var res = ch.ReadInbound<IHttpResponse>();
            Assert.Same(HttpVersion.Http11, res.ProtocolVersion);
            Assert.Equal(HttpResponseStatus.OK, res.Status);

            var lastContent = ch.ReadInbound<ILastHttpContent>();
            Assert.False(lastContent.Content.IsReadable());

            HttpHeaders headers = lastContent.TrailingHeaders;
            Assert.Equal(1, headers.Names().Count);
            IList<ICharSequence> values = headers.GetAll((AsciiString)"Set-Cookie");
            Assert.Equal(2, values.Count);
            Assert.True(values.Contains((AsciiString)"t1=t1v1"));
            Assert.True(values.Contains((AsciiString)"t2=t2v2; Expires=Wed, 09-Jun-2021 10:18:14 GMT"));
            lastContent.Release();

            Assert.False(ch.Finish());
            var last = ch.ReadInbound<IByteBufferHolder>();
            Assert.Null(last);
        }

        [Fact]
        public void ResponseWithContentLength()
        {
            var ch = new EmbeddedChannel(new HttpResponseDecoder());
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\n" +
                "Content-Length: 10\r\n" +
                "\r\n")));

            var data = new byte[10];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)i;
            }
            ch.WriteInbound(Unpooled.WrappedBuffer(data, 0, data.Length / 2));
            ch.WriteInbound(Unpooled.WrappedBuffer(data, 5, data.Length / 2));

            var res = ch.ReadInbound<IHttpResponse>();
            Assert.Same(HttpVersion.Http11, res.ProtocolVersion);
            Assert.Equal(HttpResponseStatus.OK, res.Status);

            var firstContent = ch.ReadInbound<IHttpContent>();
            Assert.Equal(5, firstContent.Content.ReadableBytes);
            Assert.Equal(Unpooled.WrappedBuffer(data, 0, 5), firstContent.Content);
            firstContent.Release();

            var lastContent = ch.ReadInbound<ILastHttpContent>();
            Assert.Equal(5, lastContent.Content.ReadableBytes);
            Assert.Equal(Unpooled.WrappedBuffer(data, 5, 5), lastContent.Content);
            lastContent.Release();

            Assert.False(ch.Finish());
            var last = ch.ReadInbound<IByteBufferHolder>();
            Assert.Null(last);
        }

        [Fact]
        public void ResponseWithContentLengthFragmented()
        {
            byte[] data = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n" +
                "Content-Length: 10\r\n" +
                "\r\n");

            for (int i = 1; i < data.Length; i++)
            {
                ResponseWithContentLengthFragmented0(data, i);
            }
        }

        static void ResponseWithContentLengthFragmented0(byte[] header, int fragmentSize)
        {
            var ch = new EmbeddedChannel(new HttpResponseDecoder());
            // split up the header
            for (int a = 0; a < header.Length;)
            {
                int amount = fragmentSize;
                if (a + amount > header.Length)
                {
                    amount = header.Length - a;
                }

                ch.WriteInbound(Unpooled.WrappedBuffer(header, a, amount));
                a += amount;
            }
            var data = new byte[10];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)i;
            }
            ch.WriteInbound(Unpooled.WrappedBuffer(data, 0, data.Length / 2));
            ch.WriteInbound(Unpooled.WrappedBuffer(data, 5, data.Length / 2));

            var res = ch.ReadInbound<IHttpResponse>();
            Assert.Same(HttpVersion.Http11, res.ProtocolVersion);
            Assert.Equal(HttpResponseStatus.OK, res.Status);

            var firstContent = ch.ReadInbound<IHttpContent>();
            Assert.Equal(5, firstContent.Content.ReadableBytes);
            Assert.Equal(Unpooled.WrappedBuffer(data, 0, 5), firstContent.Content);
            firstContent.Release();

            var lastContent = ch.ReadInbound<ILastHttpContent>();
            Assert.Equal(5, lastContent.Content.ReadableBytes);
            Assert.Equal(Unpooled.WrappedBuffer(data, 5, 5), lastContent.Content);
            lastContent.Release();

            Assert.False(ch.Finish());
            var last = ch.ReadInbound<IByteBufferHolder>();
            Assert.Null(last);
        }

        [Fact]
        public void WebSocketResponse()
        {
            byte[] data = Encoding.ASCII.GetBytes("HTTP/1.1 101 WebSocket Protocol Handshake\r\n" +
                "Upgrade: WebSocket\r\n" +
                "Connection: Upgrade\r\n" +
                "Sec-WebSocket-Origin: http://localhost:8080\r\n" +
                "Sec-WebSocket-Location: ws://localhost/some/path\r\n" +
                "\r\n" +
                "1234567812345678");
            var ch = new EmbeddedChannel(new HttpResponseDecoder());
            ch.WriteInbound(Unpooled.WrappedBuffer(data));

            var res = ch.ReadInbound<IHttpResponse>();
            Assert.Same(HttpVersion.Http11, res.ProtocolVersion);
            Assert.Equal(HttpResponseStatus.SwitchingProtocols, res.Status);
            var content = ch.ReadInbound<IHttpContent>();
            Assert.Equal(16, content.Content.ReadableBytes);
            content.Release();

            Assert.False(ch.Finish());
            var last = ch.ReadInbound<IByteBufferHolder>();
            Assert.Null(last);
        }

        // See https://github.com/netty/netty/issues/2173
        [Fact]
        public void WebSocketResponseWithDataFollowing()
        {
            byte[] data = Encoding.ASCII.GetBytes("HTTP/1.1 101 WebSocket Protocol Handshake\r\n" +
                "Upgrade: WebSocket\r\n" +
                "Connection: Upgrade\r\n" +
                "Sec-WebSocket-Origin: http://localhost:8080\r\n" +
                "Sec-WebSocket-Location: ws://localhost/some/path\r\n" +
                "\r\n" +
                "1234567812345678");
            byte[] otherData = { 1, 2, 3, 4 };

            var ch = new EmbeddedChannel(new HttpResponseDecoder());
            IByteBuffer compositeBuffer = Unpooled.WrappedBuffer(data, otherData);
            ch.WriteInbound(compositeBuffer);

            var res = ch.ReadInbound<IHttpResponse>();
            Assert.Same(HttpVersion.Http11, res.ProtocolVersion);
            Assert.Equal(HttpResponseStatus.SwitchingProtocols, res.Status);
            var content = ch.ReadInbound<IHttpContent>();
            Assert.Equal(16, content.Content.ReadableBytes);
            content.Release();

            Assert.True(ch.Finish());

            IByteBuffer expected = Unpooled.WrappedBuffer(otherData);
            var buffer = ch.ReadInbound<IByteBuffer>();
            try
            {
                Assert.Equal(expected, buffer);
            }
            finally
            {
                expected.Release();
                buffer?.Release();
            }
        }

        [Fact]
        public void GarbageHeaders()
        {
            // A response without headers - from https://github.com/netty/netty/issues/2103
            byte[] data = Encoding.ASCII.GetBytes("<html>\r\n" +
                "<head><title>400 Bad Request</title></head>\r\n" +
                "<body bgcolor=\"white\">\r\n" +
                "<center><h1>400 Bad Request</h1></center>\r\n" +
                "<hr><center>nginx/1.1.19</center>\r\n" +
                "</body>\r\n" +
                "</html>\r\n");
            var ch = new EmbeddedChannel(new HttpResponseDecoder());

            ch.WriteInbound(Unpooled.WrappedBuffer(data));
            // Garbage input should generate the 999 Unknown response.
            var res = ch.ReadInbound<IHttpResponse>();
            Assert.Same(HttpVersion.Http10, res.ProtocolVersion);
            Assert.Equal(999, res.Status.Code);
            Assert.True(res.Result.IsFailure);
            Assert.True(res.Result.IsFinished);

            var next = ch.ReadInbound<IByteBufferHolder>();
            Assert.Null(next);

            // More garbage should not generate anything (i.e. the decoder discards anything beyond this point.)
            ch.WriteInbound(Unpooled.WrappedBuffer(data));
            next = ch.ReadInbound<IByteBufferHolder>();
            Assert.Null(next);

            // Closing the connection should not generate anything since the protocol has been violated.
            ch.Finish();
            next = ch.ReadInbound<IByteBufferHolder>();
            Assert.Null(next);
        }

        // Tests if the decoder produces one and only {@link LastHttpContent} when an invalid chunk is received and
        // the connection is closed.
        [Fact]
        public void GarbageChunk()
        {
            var ch = new EmbeddedChannel(new HttpResponseDecoder());
            const string ResponseWithIllegalChunk = "HTTP/1.1 200 OK\r\n" +
                "Transfer-Encoding: chunked\r\n\r\n" +
                "NOT_A_CHUNK_LENGTH\r\n";

            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(ResponseWithIllegalChunk)));
            var res = ch.ReadInbound<IHttpResponse>();
            Assert.NotNull(res);

            // Ensure that the decoder generates the last chunk with correct decoder result.
            var invalidChunk = ch.ReadInbound<ILastHttpContent>();
            Assert.True(invalidChunk.Result.IsFailure);
            invalidChunk.Release();

            // And no more messages should be produced by the decoder.
            var next = ch.ReadInbound<IByteBufferHolder>();
            Assert.Null(next);

            // .. even after the connection is closed.
            Assert.False(ch.Finish());
        }

        [Fact]
        public void ConnectionClosedBeforeHeadersReceived()
        {
            var ch = new EmbeddedChannel(new HttpResponseDecoder());
            const string ResponseInitialLine = "HTTP/1.1 200 OK\r\n";
            Assert.False(ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(ResponseInitialLine))));
            Assert.True(ch.Finish());
            var message = ch.ReadInbound<IHttpMessage>();
            Assert.True(message.Result.IsFailure);
            Assert.IsType<PrematureChannelClosureException>(message.Result.Cause);

            var last = ch.ReadInbound<IByteBufferHolder>();
            Assert.Null(last);
        }
    }
}
