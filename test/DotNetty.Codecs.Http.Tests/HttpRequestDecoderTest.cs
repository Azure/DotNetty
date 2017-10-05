// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class HttpRequestDecoderTest
    {
        const int ContentLength = 8;

        static readonly byte[] ContentCrlfDelimiters = CreateContent("\r\n");
        static readonly byte[] ContentLfDelimiters = CreateContent("\n");
        static readonly byte[] ContentMixedDelimiters = CreateContent("\r\n", "\n");

        static byte[] CreateContent(params string[] lineDelimiters)
        {
            string lineDelimiter;
            string lineDelimiter2;
            if (lineDelimiters.Length == 2)
            {
                lineDelimiter = lineDelimiters[0];
                lineDelimiter2 = lineDelimiters[1];
            }
            else
            {
                lineDelimiter = lineDelimiters[0];
                lineDelimiter2 = lineDelimiters[0];
            }

            string content = "GET /some/path?foo=bar&wibble=eek HTTP/1.1" + "\r\n" +
                    "Upgrade: WebSocket" + lineDelimiter2 +
                    "Connection: Upgrade" + lineDelimiter +
                    "Host: localhost" + lineDelimiter2 +
                    "Origin: http://localhost:8080" + lineDelimiter +
                    "Sec-WebSocket-Key1: 10  28 8V7 8 48     0" + lineDelimiter2 +
                    "Sec-WebSocket-Key2: 8 Xt754O3Q3QW 0   _60" + lineDelimiter +
                    "Content-Length: " + ContentLength + lineDelimiter2 +
                    "\r\n" +
                    "12345678";

            return Encoding.ASCII.GetBytes(content);
        }

        [Fact]
        public void DecodeWholeRequestAtOnceCrlfDelimiters() => DecodeWholeRequestAtOnce(ContentCrlfDelimiters);

        [Fact]
        public void DecodeWholeRequestAtOnceLfDelimiters() => DecodeWholeRequestAtOnce(ContentLfDelimiters);

        [Fact]
        public void DecodeWholeRequestAtOnceMixedDelimiters() => DecodeWholeRequestAtOnce(ContentMixedDelimiters);

        static void DecodeWholeRequestAtOnce(byte[] content)
        {
            var channel = new EmbeddedChannel(new HttpRequestDecoder());
            Assert.True(channel.WriteInbound(Unpooled.WrappedBuffer(content)));
            var req = channel.ReadInbound<IHttpRequest>();
            Assert.NotNull(req);
            CheckHeaders(req.Headers);

            var c = channel.ReadInbound<ILastHttpContent>();
            Assert.Equal(ContentLength, c.Content.ReadableBytes);
            Assert.Equal(
                Unpooled.WrappedBuffer(content, content.Length - ContentLength, ContentLength), 
                c.Content.ReadSlice(ContentLength));
            c.Release();

            Assert.False(channel.Finish());
            Assert.Null(channel.ReadInbound<IHttpObject>());
        }

        static void CheckHeaders(HttpHeaders headers)
        {
            Assert.Equal(7, headers.Names().Count);
            CheckHeader(headers, "Upgrade", "WebSocket");
            CheckHeader(headers, "Connection", "Upgrade");
            CheckHeader(headers, "Host", "localhost");
            CheckHeader(headers, "Origin", "http://localhost:8080");
            CheckHeader(headers, "Sec-WebSocket-Key1", "10  28 8V7 8 48     0");
            CheckHeader(headers, "Sec-WebSocket-Key2", "8 Xt754O3Q3QW 0   _60");
            CheckHeader(headers, "Content-Length", $"{ContentLength}");
        }

        static void CheckHeader(HttpHeaders headers, string name, string value)
        {
            var headerName = (AsciiString)name;
            var headerValue = (StringCharSequence)value;

            IList<ICharSequence> header1 = headers.GetAll(headerName);
            Assert.Equal(1, header1.Count);
            Assert.Equal(headerValue, header1[0]);
        }

        [Fact]
        public void DecodeWholeRequestInMultipleStepsCrlfDelimiters() => DecodeWholeRequestInMultipleSteps(ContentCrlfDelimiters);

        [Fact]
        public void DecodeWholeRequestInMultipleStepsLFDelimiters() => DecodeWholeRequestInMultipleSteps(ContentLfDelimiters);

        [Fact]
        public void DecodeWholeRequestInMultipleStepsMixedDelimiters() => DecodeWholeRequestInMultipleSteps(ContentMixedDelimiters);

        static void DecodeWholeRequestInMultipleSteps(byte[] content)
        {
            for (int i = 1; i < content.Length; i++)
            {
                DecodeWholeRequestInMultipleSteps(content, i);
            }
        }

        static void DecodeWholeRequestInMultipleSteps(byte[] content, int fragmentSize)
        {
            var channel = new EmbeddedChannel(new HttpRequestDecoder());
            int headerLength = content.Length - ContentLength;

            // split up the header
            for (int a = 0; a < headerLength;)
            {
                int amount = fragmentSize;
                if (a + amount > headerLength)
                {
                    amount = headerLength - a;
                }

                // if header is done it should produce a HttpRequest
                channel.WriteInbound(Unpooled.WrappedBuffer(content, a, amount));
                a += amount;
            }

            for (int i = ContentLength; i > 0; i--)
            {
                // Should produce HttpContent
                channel.WriteInbound(Unpooled.WrappedBuffer(content, content.Length - i, 1));
            }

            var req = channel.ReadInbound<IHttpRequest>();
            Assert.NotNull(req);
            CheckHeaders(req.Headers);

            for (int i = ContentLength; i > 1; i--)
            {
                var c = channel.ReadInbound<IHttpContent>();
                Assert.Equal(1, c.Content.ReadableBytes);
                Assert.Equal(content[content.Length - i], c.Content.ReadByte());
                c.Release();
            }

            var last = channel.ReadInbound<ILastHttpContent>();
            Assert.Equal(1, last.Content.ReadableBytes);
            Assert.Equal(content[content.Length - 1], last.Content.ReadByte());
            last.Release();

            Assert.False(channel.Finish());
            Assert.Null(channel.ReadInbound<IHttpObject>());
        }

        [Fact]
        public void MultiLineHeader()
        {
            var channel = new EmbeddedChannel(new HttpRequestDecoder());
            const string Crlf = "\r\n";
            const string Request = "GET /some/path HTTP/1.1" + Crlf +
                "Host: localhost" + Crlf +
                "MyTestHeader: part1" + Crlf +
                "              newLinePart2" + Crlf +
                "MyTestHeader2: part21" + Crlf +
                "\t            newLinePart22"
                + Crlf + Crlf;
            Assert.True(channel.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(Request))));
            var req = channel.ReadInbound<IHttpRequest>();
            Assert.Equal("part1 newLinePart2", req.Headers.Get(new AsciiString("MyTestHeader"), null).ToString());
            Assert.Equal("part21 newLinePart22", req.Headers.Get(new AsciiString("MyTestHeader2"), null).ToString());

            var c = channel.ReadInbound<ILastHttpContent>();
            c.Release();

            Assert.False(channel.Finish());
            var last = channel.ReadInbound<object>();
            Assert.Null(last);
        }

        [Fact]
        public void EmptyHeaderValue()
        {
            var channel = new EmbeddedChannel(new HttpRequestDecoder());
            const string Crlf = "\r\n";
            const string Request = "GET /some/path HTTP/1.1" + Crlf +
                "Host: localhost" + Crlf +
                "EmptyHeader:" + Crlf + Crlf;
            byte[] data = Encoding.ASCII.GetBytes(Request);

            channel.WriteInbound(Unpooled.WrappedBuffer(data));
            var req = channel.ReadInbound<IHttpRequest>();
            Assert.Equal("", req.Headers.Get((AsciiString)"EmptyHeader", null).ToString());
        }

        [Fact]
        public void Http100Continue()
        {
            var decoder = new HttpRequestDecoder();
            var channel = new EmbeddedChannel(decoder);
            const string Oversized = "PUT /file HTTP/1.1\r\n" +
                "Expect: 100-continue\r\n" +
                "Content-Length: 1048576000\r\n\r\n";
            byte[] data = Encoding.ASCII.GetBytes(Oversized);
            channel.WriteInbound(Unpooled.CopiedBuffer(data));
            var req = channel.ReadInbound<IHttpRequest>();
            Assert.NotNull(req);

            // At this point, we assume that we sent '413 Entity Too Large' to the peer without closing the connection
            // so that the client can try again.
            decoder.Reset();

            const string Query = "GET /max-file-size HTTP/1.1\r\n\r\n";
            data = Encoding.ASCII.GetBytes(Query);
            channel.WriteInbound(Unpooled.CopiedBuffer(data));

            req = channel.ReadInbound<IHttpRequest>();
            Assert.NotNull(req);

            var last = channel.ReadInbound<ILastHttpContent>();
            Assert.NotNull(last);
            Assert.IsType<EmptyLastHttpContent>(last);

            Assert.False(channel.Finish());
        }

        [Fact]
        public void Http100ContinueWithBadClient()
        {
            var decoder = new HttpRequestDecoder();
            var channel = new EmbeddedChannel(decoder);
            const string Oversized =
                "PUT /file HTTP/1.1\r\n" +
                "Expect: 100-continue\r\n" +
                "Content-Length: 1048576000\r\n\r\n" +
                "WAY_TOO_LARGE_DATA_BEGINS";
            byte[] data = Encoding.ASCII.GetBytes(Oversized);
            channel.WriteInbound(Unpooled.CopiedBuffer(data));
            var req = channel.ReadInbound<IHttpRequest>();
            Assert.NotNull(req);

            var prematureData = channel.ReadInbound<IHttpContent>();
            prematureData.Release();

            req = channel.ReadInbound<IHttpRequest>();
            Assert.Null(req);

            // At this point, we assume that we sent '413 Entity Too Large' to the peer without closing the connection
            // so that the client can try again.
            decoder.Reset();

            const string Query = "GET /max-file-size HTTP/1.1\r\n\r\n";
            data = Encoding.ASCII.GetBytes(Query);
            channel.WriteInbound(Unpooled.CopiedBuffer(data));

            req = channel.ReadInbound<IHttpRequest>();
            Assert.NotNull(req);

            var last = channel.ReadInbound<ILastHttpContent>();
            Assert.NotNull(last);
            Assert.IsType<EmptyLastHttpContent>(last);

            Assert.False(channel.Finish());
        }

        [Fact]
        public void MessagesSplitBetweenMultipleBuffers()
        {
            var channel = new EmbeddedChannel(new HttpRequestDecoder());
            const string Crlf = "\r\n";
            const string Str1 = "GET /some/path HTTP/1.1" + Crlf +
                "Host: localhost1" + Crlf + Crlf +
                "GET /some/other/path HTTP/1.0" + Crlf +
                "Hos";
            const string Str2 = "t: localhost2" + Crlf +
                "content-length: 0" + Crlf + Crlf;

            byte[] data = Encoding.ASCII.GetBytes(Str1);
            channel.WriteInbound(Unpooled.CopiedBuffer(data));

            var req = channel.ReadInbound<IHttpRequest>();
            Assert.Equal(HttpVersion.Http11, req.ProtocolVersion);
            Assert.Equal("/some/path", req.Uri);
            Assert.Equal(1, req.Headers.Size);
            Assert.True(AsciiString.ContentEqualsIgnoreCase((StringCharSequence)"localhost1", req.Headers.Get(HttpHeaderNames.Host, null)));
            var cnt = channel.ReadInbound<ILastHttpContent>();
            cnt.Release();

            data = Encoding.ASCII.GetBytes(Str2);
            channel.WriteInbound(Unpooled.CopiedBuffer(data));
            req = channel.ReadInbound<IHttpRequest>();
            Assert.Equal(HttpVersion.Http10, req.ProtocolVersion);
            Assert.Equal("/some/other/path", req.Uri);
            Assert.Equal(2, req.Headers.Size);
            Assert.True(AsciiString.ContentEqualsIgnoreCase((StringCharSequence)"localhost2", req.Headers.Get(HttpHeaderNames.Host, null)));
            Assert.True(AsciiString.ContentEqualsIgnoreCase((StringCharSequence)"0", req.Headers.Get(HttpHeaderNames.ContentLength, null)));
            cnt = channel.ReadInbound<ILastHttpContent>();
            cnt.Release();

            Assert.False(channel.FinishAndReleaseAll());
        }

        [Fact]
        public void TooLargeInitialLine()
        {
            var channel = new EmbeddedChannel(new HttpRequestDecoder(10, 1024, 1024));
            const string RequestStr = "GET /some/path HTTP/1.1\r\n" +
                "Host: localhost1\r\n\r\n";

            Assert.True(channel.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(RequestStr))));
            var request = channel.ReadInbound<IHttpRequest>();
            Assert.True(request.Result.IsFailure);
            Assert.IsType<TooLongFrameException>(request.Result.Cause);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void TooLargeHeaders()
        {
            var channel = new EmbeddedChannel(new HttpRequestDecoder(1024, 10, 1024));
            const string RequestStr = "GET /some/path HTTP/1.1\r\n" +
                "Host: localhost1\r\n\r\n";

            Assert.True(channel.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(RequestStr))));
            var request = channel.ReadInbound<IHttpRequest>();
            Assert.True(request.Result.IsFailure);
            Assert.IsType<TooLongFrameException>(request.Result.Cause);
            Assert.False(channel.Finish());
        }
    }
}
