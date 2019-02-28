// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using DotNetty.Common.Utilities;
    using Xunit;

    public sealed class HttpUtilTest
    {
        [Fact]
        public void RemoveTransferEncodingIgnoreCase()
        {
            var message = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            message.Headers.Set(HttpHeaderNames.TransferEncoding, "Chunked");
            Assert.False(message.Headers.IsEmpty);
            HttpUtil.SetTransferEncodingChunked(message, false);
            Assert.True(message.Headers.IsEmpty);
        }

        // See https://github.com/netty/netty/issues/1690
        [Fact]
        public void GetOperations()
        {
            HttpHeaders headers = new DefaultHttpHeaders();
            headers.Add(new AsciiString("Foo"), new AsciiString("1"));
            headers.Add(new AsciiString("Foo"), new AsciiString("2"));

            Assert.True(headers.TryGet(new AsciiString("Foo"), out ICharSequence value));
            Assert.Equal("1", value.ToString());

            IList<ICharSequence> values = headers.GetAll(new AsciiString("Foo"));
            Assert.NotNull(values);
            Assert.Equal(2, values.Count);
            Assert.Equal("1", values[0].ToString());
            Assert.Equal("2", values[1].ToString());
        }

        [Fact]
        public void GetCharsetAsRawCharSequence()
        {
            const string QuotesCharsetContentType = "text/html; charset=\"utf8\"";
            const string SimpleContentType = "text/html";

            var message = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            message.Headers.Set(HttpHeaderNames.ContentType, QuotesCharsetContentType);
            Assert.Equal("\"utf8\"", HttpUtil.GetCharsetAsSequence(message).ToString());
            Assert.Equal("\"utf8\"", HttpUtil.GetCharsetAsSequence(new AsciiString(QuotesCharsetContentType)));

            message.Headers.Set(HttpHeaderNames.ContentType, "text/html");
            Assert.Null(HttpUtil.GetCharsetAsSequence(message));
            Assert.Null(HttpUtil.GetCharsetAsSequence(new AsciiString(SimpleContentType)));
        }

        [Fact]
        public void GetCharset()
        {
            const string NormalContentType = "text/html; charset=utf-8";
            const string UpperCaseNormalContentType = "TEXT/HTML; CHARSET=UTF-8";

            var message = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            message.Headers.Set(HttpHeaderNames.ContentType, NormalContentType);
            Assert.Equal(Encoding.UTF8, HttpUtil.GetCharset(message));
            Assert.Equal(Encoding.UTF8, HttpUtil.GetCharset(new AsciiString(NormalContentType)));

            message.Headers.Set(HttpHeaderNames.ContentType, UpperCaseNormalContentType);
            Assert.Equal(Encoding.UTF8, HttpUtil.GetCharset(message));
            Assert.Equal(Encoding.UTF8, HttpUtil.GetCharset(new AsciiString(UpperCaseNormalContentType)));
        }

        [Fact]
        public void GetCharsetDefaultValue()
        {
            const string SimpleContentType = "text/html";
            const string ContentTypeWithIncorrectCharset = "text/html; charset=UTFFF";

            var message = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            message.Headers.Set(HttpHeaderNames.ContentType, SimpleContentType);
            Assert.Equal(Encoding.UTF8, HttpUtil.GetCharset(message));
            Assert.Equal(Encoding.UTF8, HttpUtil.GetCharset(new AsciiString(SimpleContentType)));

            message.Headers.Set(HttpHeaderNames.ContentType, SimpleContentType);
            Assert.Equal(Encoding.UTF8, HttpUtil.GetCharset(message, Encoding.UTF8));
            Assert.Equal(Encoding.UTF8, HttpUtil.GetCharset(new AsciiString(SimpleContentType), Encoding.UTF8));

            message.Headers.Set(HttpHeaderNames.ContentType, ContentTypeWithIncorrectCharset);
            Assert.Equal(Encoding.UTF8, HttpUtil.GetCharset(message));
            Assert.Equal(Encoding.UTF8, HttpUtil.GetCharset(new AsciiString(ContentTypeWithIncorrectCharset)));

            message.Headers.Set(HttpHeaderNames.ContentType, ContentTypeWithIncorrectCharset);
            Assert.Equal(Encoding.UTF8, HttpUtil.GetCharset(message, Encoding.UTF8));
            Assert.Equal(Encoding.UTF8, HttpUtil.GetCharset(new AsciiString(ContentTypeWithIncorrectCharset), Encoding.UTF8));
        }

        [Fact]
        public void GetMimeType()
        {
            const string SimpleContentType = "text/html";
            const string NormalContentType = "text/html; charset=utf-8";

            var message = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            Assert.Null(HttpUtil.GetMimeType(message));
            message.Headers.Set(HttpHeaderNames.ContentType, "");
            Assert.Null(HttpUtil.GetMimeType(message));
            Assert.Null(HttpUtil.GetMimeType(new AsciiString("")));
            message.Headers.Set(HttpHeaderNames.ContentType, SimpleContentType);
            Assert.Equal("text/html", HttpUtil.GetMimeType(message));
            Assert.Equal("text/html", HttpUtil.GetMimeType(new AsciiString(SimpleContentType)));

            message.Headers.Set(HttpHeaderNames.ContentType, NormalContentType);
            Assert.Equal("text/html", HttpUtil.GetMimeType(message));
            Assert.Equal("text/html", HttpUtil.GetMimeType(new AsciiString(NormalContentType)));
        }

        [Fact]
        public void GetContentLengthThrowsNumberFormatException()
        {
            var message = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            message.Headers.Set(HttpHeaderNames.ContentLength, "bar");
            Assert.Throws<FormatException>(() => HttpUtil.GetContentLength(message));
        }

        [Fact]
        public void GetContentLengthIntDefaultValueThrowsNumberFormatException()
        {
            var message = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            message.Headers.Set(HttpHeaderNames.ContentLength, "bar");
            Assert.Throws<FormatException>(() => HttpUtil.GetContentLength(message, 1));
        }

        [Fact]
        public void GetContentLengthLongDefaultValueThrowsNumberFormatException()
        {
            var message = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            message.Headers.Set(HttpHeaderNames.ContentLength, "bar");
            Assert.Throws<FormatException>(() => HttpUtil.GetContentLength(message, 1L));
        }

        [Fact]
        public void DoubleChunkedHeader()
        {
            var message = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            message.Headers.Add(HttpHeaderNames.TransferEncoding, "chunked");
            HttpUtil.SetTransferEncodingChunked(message, true);

            IList<ICharSequence> list = message.Headers.GetAll(HttpHeaderNames.TransferEncoding);
            Assert.NotNull(list);
            
            var expected = new List<string> {"chunked"};
            Assert.True(expected.SequenceEqual(list.Select(x => x.ToString())));
        }

        static IEnumerable<string> AllPossibleCasesOfContinue()
        {
            var cases = new List<string>();
            string c = "continue";
            for (int i = 0; i < Math.Pow(2, c.Length); i++)
            {
                var sb = new StringBuilder(c.Length);
                int j = i;
                int k = 0;
                while (j > 0)
                {
                    if ((j & 1) == 1)
                    {
                        sb.Append(char.ToUpper(c[k++]));
                    }
                    else
                    {
                        sb.Append(c[k++]);
                    }
                    j >>= 1;
                }
                for (; k < c.Length; k++)
                {
                    sb.Append(c[k]);
                }

                cases.Add(sb.ToString());
            }

            return cases;
        }

        [Fact]
        public void Is100Continue()
        {
            // test all possible cases of 100-continue
            foreach (string continueCase in AllPossibleCasesOfContinue())
            {
                Run100ContinueTest(HttpVersion.Http11, "100-" + continueCase, true);
            }
            Run100ContinueTest(HttpVersion.Http11, null, false);
            Run100ContinueTest(HttpVersion.Http11, "chocolate=yummy", false);
            Run100ContinueTest(HttpVersion.Http10, "100-continue", false);

            var message = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            message.Headers.Set(HttpHeaderNames.Expect, "100-continue");
            Run100ContinueTest(message, false);
        }

        static void Run100ContinueTest(HttpVersion version, string expectations, bool expect)
        {
            var message = new DefaultFullHttpRequest(version, HttpMethod.Get, "/");
            if (expectations != null)
            {
                message.Headers.Set(HttpHeaderNames.Expect, expectations);
            }

            Run100ContinueTest(message, expect);
        }

        static void Run100ContinueTest(IHttpMessage message, bool expected)
        {
            Assert.Equal(expected, HttpUtil.Is100ContinueExpected(message));
            ReferenceCountUtil.Release(message);
        }

        [Fact]
        public void ContainsUnsupportedExpectation()
        {
            // test all possible cases of 100-continue
            foreach (string continueCase in AllPossibleCasesOfContinue())
            {
                RunUnsupportedExpectationTest(HttpVersion.Http11, "100-" + continueCase, false);
            }
            RunUnsupportedExpectationTest(HttpVersion.Http11, null, false);
            RunUnsupportedExpectationTest(HttpVersion.Http11, "chocolate=yummy", true);
            RunUnsupportedExpectationTest(HttpVersion.Http10, "100-continue", false);

            var message = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            message.Headers.Set(new AsciiString("Expect"), "100-continue");
            RunUnsupportedExpectationTest(message, false);
        }

        static void RunUnsupportedExpectationTest(HttpVersion version, string expectations, bool expect)
        {
            var message = new DefaultFullHttpRequest(version, HttpMethod.Get, "/");
            if (expectations != null)
            {
                message.Headers.Set(new AsciiString("Expect"), expectations);
            }
            RunUnsupportedExpectationTest(message, expect);
        }

        static void RunUnsupportedExpectationTest(IHttpMessage message, bool expected)
        {
            Assert.Equal(expected, HttpUtil.IsUnsupportedExpectation(message));
            ReferenceCountUtil.Release(message);
        }
    }
}
