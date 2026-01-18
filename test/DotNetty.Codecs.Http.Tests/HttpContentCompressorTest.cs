// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Compression;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class HttpContentCompressorTest
    {
        // GZIPHeader
        public static IEnumerable<object[]> GetGZipHeader()
        {
            string gzipHeaderHex = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                gzipHeaderHex = "1f8b080000000000000b";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                gzipHeaderHex = "1f8b0800000000000003";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                gzipHeaderHex = "1f8b0800000000000007";
            }
            else
            {
                gzipHeaderHex = "1f8b08000000000000ff";
            }

            yield return new object[] { gzipHeaderHex };
        }
        
        [Fact]
        public void GetTargetContentEncoding()
        {
            var compressor = new HttpContentCompressor();

            string[] tests =
            {
                // Accept-Encoding -> Content-Encoding
                "", null,
                "*", "gzip",
                "*;q=0.0", null,
                "gzip", "gzip",
                "compress, gzip;q=0.5", "gzip",
                "gzip; q=0.5, identity", "gzip",
                "gzip ; q=0.1", "gzip",
                "gzip; q=0, deflate", "deflate",
                " deflate ; q=0 , *;q=0.5", "gzip"
            };
            for (int i = 0; i < tests.Length; i += 2)
            {
                var acceptEncoding = (AsciiString)tests[i];
                string contentEncoding = tests[i + 1];
                ZlibWrapper? targetWrapper = compressor.DetermineWrapper(acceptEncoding);
                string targetEncoding = null;
                if (targetWrapper != null)
                {
                    switch (targetWrapper)
                    {
                        case ZlibWrapper.Gzip:
                            targetEncoding = "gzip";
                            break;
                        case ZlibWrapper.Zlib:
                            targetEncoding = "deflate";
                            break;
                        default:
                            Assert.True(false, $"Invalid type {targetWrapper}");
                            break;
                    }
                }
                Assert.Equal(contentEncoding, targetEncoding);
            }
        }

        [Theory]
        [MemberData(nameof(GetGZipHeader))]
        public void SplitContent(string gzipHeaderHex)
        {
            var ch = new EmbeddedChannel(new HttpContentCompressor());
            ch.WriteInbound(NewRequest());

            ch.WriteOutbound(new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK));
            ch.WriteOutbound(new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("Hell"))));
            ch.WriteOutbound(new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("o, w"))));
            ch.WriteOutbound(new DefaultLastHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("orld"))));

            AssertEncodedResponse(ch);

            var chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal(gzipHeaderHex + "f248cdc901000000ffff", ByteBufferUtil.HexDump(chunk.Content));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal("cad7512807000000ffff", ByteBufferUtil.HexDump(chunk.Content));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal("ca2fca4901000000ffff", ByteBufferUtil.HexDump(chunk.Content));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal("0300c2a99ae70c000000", ByteBufferUtil.HexDump(chunk.Content));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.False(chunk.Content.IsReadable());
            Assert.Equal(EmptyLastHttpContent.Default, chunk);
            chunk.Release();

            var last = ch.ReadOutbound<object>();
            Assert.Null(last);
        }

        [Theory]
        [MemberData(nameof(GetGZipHeader))]
        public void ChunkedContent(string gzipHeaderHex)
        {
            var ch = new EmbeddedChannel(new HttpContentCompressor());
            ch.WriteInbound(NewRequest());

            var res = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            res.Headers.Set(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);
            ch.WriteOutbound(res);

            AssertEncodedResponse(ch);

            ch.WriteOutbound(new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("Hell"))));
            ch.WriteOutbound(new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("o, w"))));
            ch.WriteOutbound(new DefaultLastHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("orld"))));

            var chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal(gzipHeaderHex + "f248cdc901000000ffff", ByteBufferUtil.HexDump(chunk.Content));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal("cad7512807000000ffff", ByteBufferUtil.HexDump(chunk.Content));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal("ca2fca4901000000ffff", ByteBufferUtil.HexDump(chunk.Content));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal("0300c2a99ae70c000000", ByteBufferUtil.HexDump(chunk.Content));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.False(chunk.Content.IsReadable());
            Assert.Equal(EmptyLastHttpContent.Default, chunk);
            chunk.Release();

            var last = ch.ReadOutbound<object>();
            Assert.Null(last);
        }

        [Theory]
        [MemberData(nameof(GetGZipHeader))]
        public void ChunkedContentWithTrailingHeader(string gzipHeaderHex)
        {
            var ch = new EmbeddedChannel(new HttpContentCompressor());
            ch.WriteInbound(NewRequest());

            var res = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            res.Headers.Set(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);
            ch.WriteOutbound(res);

            AssertEncodedResponse(ch);

            ch.WriteOutbound(new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("Hell"))));
            ch.WriteOutbound(new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("o, w"))));
            var content = new DefaultLastHttpContent(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("orld")));
            content.TrailingHeaders.Set((AsciiString)"X-Test", (AsciiString)"Netty");
            ch.WriteOutbound(content);

            var chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal(gzipHeaderHex + "f248cdc901000000ffff", ByteBufferUtil.HexDump(chunk.Content));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal("cad7512807000000ffff", ByteBufferUtil.HexDump(chunk.Content));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal("ca2fca4901000000ffff", ByteBufferUtil.HexDump(chunk.Content));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal("0300c2a99ae70c000000", ByteBufferUtil.HexDump(chunk.Content));
            chunk.Release();

            var lastChunk = ch.ReadOutbound<ILastHttpContent>();
            Assert.NotNull(lastChunk);
            Assert.Equal("Netty", lastChunk.TrailingHeaders.Get((AsciiString)"X-Test", null).ToString());
            lastChunk.Release();

            var last = ch.ReadOutbound<object>();
            Assert.Null(last);
        }

        [Theory]
        [MemberData(nameof(GetGZipHeader))]
        public void FullContentWithContentLength(string gzipHeaderHex)
        {
            var ch = new EmbeddedChannel(new HttpContentCompressor());
            ch.WriteInbound(NewRequest());

            var fullRes = new DefaultFullHttpResponse(
                HttpVersion.Http11,
                HttpResponseStatus.OK,
                Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("Hello, World")));
            fullRes.Headers.Set(HttpHeaderNames.ContentLength, fullRes.Content.ReadableBytes);
            ch.WriteOutbound(fullRes);

            var res = ch.ReadOutbound<IHttpResponse>();
            Assert.NotNull(res);
            Assert.False(res is IHttpContent, $"{res.GetType()}");

            Assert.False(res.Headers.TryGet(HttpHeaderNames.TransferEncoding, out _));
            Assert.Equal("gzip", res.Headers.Get(HttpHeaderNames.ContentEncoding, null).ToString());

            long contentLengthHeaderValue = HttpUtil.GetContentLength(res);
            long observedLength = 0;

            var c = ch.ReadOutbound<IHttpContent>();
            observedLength += c.Content.ReadableBytes;
            Assert.Equal(gzipHeaderHex + "f248cdc9c9d75108cf2fca4901000000ffff", ByteBufferUtil.HexDump(c.Content));
            c.Release();

            c = ch.ReadOutbound<IHttpContent>();
            observedLength += c.Content.ReadableBytes;
            Assert.Equal("0300c6865b260c000000", ByteBufferUtil.HexDump(c.Content));
            c.Release();

            var last = ch.ReadOutbound<ILastHttpContent>();
            Assert.Equal(0, last.Content.ReadableBytes);
            last.Release();

            var next = ch.ReadOutbound<object>();
            Assert.Null(next);
            Assert.Equal(contentLengthHeaderValue, observedLength);
        }

        [Theory]
        [MemberData(nameof(GetGZipHeader))]
        public void FullContent(string gzipHeaderHex)
        {
            var ch = new EmbeddedChannel(new HttpContentCompressor());
            ch.WriteInbound(NewRequest());

            var res = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK, 
                Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("Hello, World")));
            ch.WriteOutbound(res);

            AssertEncodedResponse(ch);

            var chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal(gzipHeaderHex + "f248cdc9c9d75108cf2fca4901000000ffff", ByteBufferUtil.HexDump(chunk.Content));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal("0300c6865b260c000000", ByteBufferUtil.HexDump(chunk.Content));
            chunk.Release();

            var lastChunk = ch.ReadOutbound<ILastHttpContent>();
            Assert.NotNull(lastChunk);
            Assert.Equal(0, lastChunk.Content.ReadableBytes);
            lastChunk.Release();

            var last = ch.ReadOutbound<object>();
            Assert.Null(last);
        }

        [Theory]
        [MemberData(nameof(GetGZipHeader))]
        public void EmptySplitContent(string gzipHeaderHex)
        {
            var ch = new EmbeddedChannel(new HttpContentCompressor());
            ch.WriteInbound(NewRequest());

            ch.WriteOutbound(new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK));
            AssertEncodedResponse(ch);

            ch.WriteOutbound(EmptyLastHttpContent.Default);
            var chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal(gzipHeaderHex + "03000000000000000000", ByteBufferUtil.HexDump(chunk.Content));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.False(chunk.Content.IsReadable());
            Assert.IsAssignableFrom<ILastHttpContent>(chunk);

            var last = ch.ReadOutbound<object>();
            Assert.Null(last);
        }

        [Fact]
        public void EmptyFullContent()
        {
            var ch = new EmbeddedChannel(new HttpContentCompressor());
            ch.WriteInbound(NewRequest());

            IFullHttpResponse res = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK, Unpooled.Empty);
            ch.WriteOutbound(res);

            res = ch.ReadOutbound<IFullHttpResponse>();
            Assert.NotNull(res);

            Assert.False(res.Headers.TryGet(HttpHeaderNames.TransferEncoding, out _));

            // Content encoding shouldn't be modified.
            Assert.False(res.Headers.TryGet(HttpHeaderNames.ContentEncoding, out _));
            Assert.Equal(0, res.Content.ReadableBytes);
            Assert.Equal("", res.Content.ToString(Encoding.ASCII));
            res.Release();

            var last = ch.ReadOutbound<object>();
            Assert.Null(last);
        }

        [Fact]
        public void EmptyFullContentWithTrailer()
        {
            var ch = new EmbeddedChannel(new HttpContentCompressor());
            ch.WriteInbound(NewRequest());

            IFullHttpResponse res = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK, Unpooled.Empty);
            res.TrailingHeaders.Set((AsciiString)"X-Test", (AsciiString)"Netty");
            ch.WriteOutbound(res);

            res = ch.ReadOutbound<IFullHttpResponse>();
            Assert.False(res.Headers.TryGet(HttpHeaderNames.TransferEncoding, out _));

            // Content encoding shouldn't be modified.
            Assert.False(res.Headers.TryGet(HttpHeaderNames.ContentEncoding, out _));
            Assert.Equal(0, res.Content.ReadableBytes);
            Assert.Equal("", res.Content.ToString(Encoding.ASCII));
            Assert.Equal("Netty", res.TrailingHeaders.Get((AsciiString)"X-Test", null));

            var last = ch.ReadOutbound<object>();
            Assert.Null(last);
        }

        [Fact]
        public void Status100Continue()
        {
            IFullHttpRequest request = NewRequest();
            HttpUtil.Set100ContinueExpected(request, true);

            var ch = new EmbeddedChannel(new HttpContentCompressor());
            ch.WriteInbound(request);

            var continueResponse = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.Continue, Unpooled.Empty);
            ch.WriteOutbound(continueResponse);

            IFullHttpResponse res = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK, Unpooled.Empty);
            res.TrailingHeaders.Set((AsciiString)"X-Test", (AsciiString)"Netty");
            ch.WriteOutbound(res);

            res = ch.ReadOutbound<IFullHttpResponse>();
            Assert.NotNull(res);
            Assert.Same(continueResponse, res);
            res.Release();

            res = ch.ReadOutbound<IFullHttpResponse>();
            Assert.NotNull(res);
            Assert.False(res.Headers.TryGet(HttpHeaderNames.TransferEncoding, out _));

            // Content encoding shouldn't be modified.
            Assert.False(res.Headers.TryGet(HttpHeaderNames.ContentEncoding, out _));
            Assert.Equal(0, res.Content.ReadableBytes);
            Assert.Equal("", res.Content.ToString(Encoding.ASCII));
            Assert.Equal("Netty", res.TrailingHeaders.Get((AsciiString)"X-Test", null));

            var last = ch.ReadOutbound<object>();
            Assert.Null(last);
        }

        [Fact]
        public void TooManyResponses()
        {
            var ch = new EmbeddedChannel(new HttpContentCompressor());
            ch.WriteInbound(NewRequest());

            ch.WriteOutbound(new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK, Unpooled.Empty));

            try
            {
                ch.WriteOutbound(new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK, Unpooled.Empty));
                Assert.True(false, "Should not get here, expecting exception thrown");
            }
            catch (AggregateException e)
            {
                Assert.Single(e.InnerExceptions);
                Assert.IsType<EncoderException>(e.InnerExceptions[0]);
                Exception exception = e.InnerExceptions[0];
                Assert.IsType<InvalidOperationException>(exception.InnerException);
            }

            Assert.True(ch.Finish());

            for (;;)
            {
                var message = ch.ReadOutbound<object>();
                if (message == null)
                {
                    break;
                }
                ReferenceCountUtil.Release(message);
            }
            for (;;)
            {
                var message = ch.ReadInbound<object>();
                if (message == null)
                {
                    break;
                }
                ReferenceCountUtil.Release(message);
            }
        }

        [Fact]
        public void Identity()
        {
            var ch = new EmbeddedChannel(new HttpContentCompressor());
            Assert.True(ch.WriteInbound(NewRequest()));

            var res = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK, 
                Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("Hello, World")));
            int len = res.Content.ReadableBytes;
            res.Headers.Set(HttpHeaderNames.ContentLength, len);
            res.Headers.Set(HttpHeaderNames.ContentEncoding, HttpHeaderValues.Identity);
            Assert.True(ch.WriteOutbound(res));

            var response = ch.ReadOutbound<IFullHttpResponse>();
            Assert.Equal(len.ToString(), response.Headers.Get(HttpHeaderNames.ContentLength, null).ToString());
            Assert.Equal(HttpHeaderValues.Identity.ToString(), response.Headers.Get(HttpHeaderNames.ContentEncoding, null).ToString());
            Assert.Equal("Hello, World", response.Content.ToString(Encoding.ASCII));
            response.Release();

            Assert.True(ch.FinishAndReleaseAll());
        }

        static IFullHttpRequest NewRequest()
        {
            var req = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/");
            req.Headers.Set(HttpHeaderNames.AcceptEncoding, "gzip");
            return req;
        }

        static void AssertEncodedResponse(EmbeddedChannel ch)
        {
            var res = ch.ReadOutbound<IHttpResponse>();
            Assert.NotNull(res);

            var content = res as IHttpContent;
            Assert.Null(content);

            Assert.Equal("chunked", res.Headers.Get(HttpHeaderNames.TransferEncoding, null));
            Assert.False(res.Headers.TryGet(HttpHeaderNames.ContentLength, out _));
            Assert.Equal("gzip", res.Headers.Get(HttpHeaderNames.ContentEncoding, null));
        }
    }
}
