// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System;
    using System.Text;
    using System.Threading;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class HttpContentEncoderTest
    {
        sealed class TestEncoder : HttpContentEncoder
        {
            protected override Result BeginEncode(IHttpResponse headers, ICharSequence acceptEncoding) =>
                new Result(new StringCharSequence("test"), new EmbeddedChannel(new EmbeddedMessageEncoder()));
        }

        sealed class EmbeddedMessageEncoder : MessageToByteEncoder<IByteBuffer>
        {
            protected override void Encode(IChannelHandlerContext context, IByteBuffer message, IByteBuffer output)
            {
                output.WriteBytes(Encoding.ASCII.GetBytes(Convert.ToString(message.ReadableBytes)));
                message.SkipBytes(message.ReadableBytes);
            }
        }

        [Fact]
        public void SplitContent()
        {
            var ch = new EmbeddedChannel(new TestEncoder());
            ch.WriteInbound(new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/"));

            ch.WriteOutbound(new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK));
            ch.WriteOutbound(new DefaultHttpContent(Unpooled.WrappedBuffer(new byte[3])));
            ch.WriteOutbound(new DefaultHttpContent(Unpooled.WrappedBuffer(new byte[2])));
            ch.WriteOutbound(new DefaultLastHttpContent(Unpooled.WrappedBuffer(new byte[1])));

            AssertEncodedResponse(ch);

            var chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal("3", chunk.Content.ToString(Encoding.ASCII));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal("2", chunk.Content.ToString(Encoding.ASCII));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal("1", chunk.Content.ToString(Encoding.ASCII));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.False(chunk.Content.IsReadable());
            Assert.IsAssignableFrom<ILastHttpContent>(chunk);
            chunk.Release();

            var next = ch.ReadOutbound<object>();
            Assert.Null(next);
        }

        [Fact]
        public void ChunkedContent()
        {
            var ch = new EmbeddedChannel(new TestEncoder());
            ch.WriteInbound(new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/"));

            var res = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            res.Headers.Set(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);
            ch.WriteOutbound(res);

            AssertEncodedResponse(ch);

            ch.WriteOutbound(new DefaultHttpContent(Unpooled.WrappedBuffer(new byte[3])));
            ch.WriteOutbound(new DefaultHttpContent(Unpooled.WrappedBuffer(new byte[2])));
            ch.WriteOutbound(new DefaultLastHttpContent(Unpooled.WrappedBuffer(new byte[1])));

            var chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal("3", chunk.Content.ToString(Encoding.ASCII));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal("2", chunk.Content.ToString(Encoding.ASCII));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal("1", chunk.Content.ToString(Encoding.ASCII));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.False(chunk.Content.IsReadable());
            Assert.IsAssignableFrom<ILastHttpContent>(chunk);
            chunk.Release();

            var next = ch.ReadOutbound<object>();
            Assert.Null(next);
        }

        [Fact]
        public void ChunkedContentWithTrailingHeader()
        {
            var ch = new EmbeddedChannel(new TestEncoder());
            ch.WriteInbound(new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/"));

            var res = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            res.Headers.Set(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);
            ch.WriteOutbound(res);

            AssertEncodedResponse(ch);

            ch.WriteOutbound(new DefaultHttpContent(Unpooled.WrappedBuffer(new byte[3])));
            ch.WriteOutbound(new DefaultHttpContent(Unpooled.WrappedBuffer(new byte[2])));
            var content = new DefaultLastHttpContent(Unpooled.WrappedBuffer(new byte[1]));
            content.TrailingHeaders.Set((AsciiString)"X-Test", (AsciiString)"Netty");
            ch.WriteOutbound(content);

            var chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal("3", chunk.Content.ToString(Encoding.ASCII));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal("2", chunk.Content.ToString(Encoding.ASCII));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.Equal("1", chunk.Content.ToString(Encoding.ASCII));
            chunk.Release();

            var last = ch.ReadOutbound<ILastHttpContent>();
            Assert.NotNull(last);
            Assert.False(last.Content.IsReadable());
            Assert.Equal("Netty", last.TrailingHeaders.Get((AsciiString)"X-Test", null).ToString());
            last.Release();

            var next = ch.ReadOutbound<object>();
            Assert.Null(next);
        }

        [Fact]
        public void FullContentWithContentLength()
        {
            var ch = new EmbeddedChannel(new TestEncoder());
            ch.WriteInbound(new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/"));

            var fullRes = new DefaultFullHttpResponse(
                HttpVersion.Http11, HttpResponseStatus.OK, Unpooled.WrappedBuffer(new byte[42]));
            fullRes.Headers.Set(HttpHeaderNames.ContentLength, 42);
            ch.WriteOutbound(fullRes);

            var res = ch.ReadOutbound<IHttpResponse>();
            Assert.NotNull(res);
            Assert.False(res is IHttpContent, $"{res.GetType()}");
            Assert.False(res.Headers.TryGet(HttpHeaderNames.TransferEncoding, out _));
            Assert.Equal("2", res.Headers.Get(HttpHeaderNames.ContentLength, null).ToString());
            Assert.Equal("test", res.Headers.Get(HttpHeaderNames.ContentEncoding, null).ToString());

            var c = ch.ReadOutbound<IHttpContent>();
            Assert.Equal(2, c.Content.ReadableBytes);
            Assert.Equal("42", c.Content.ToString(Encoding.ASCII));
            c.Release();

            var last = ch.ReadOutbound<ILastHttpContent>();
            Assert.Equal(0, last.Content.ReadableBytes);
            last.Release();

            var next = ch.ReadOutbound<object>();
            Assert.Null(next);
        }

        [Fact]
        public void FullContent()
        {
            var ch = new EmbeddedChannel(new TestEncoder());
            ch.WriteInbound(new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/"));

            var res = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK,
                Unpooled.WrappedBuffer(new byte[42]));
            ch.WriteOutbound(res);

            AssertEncodedResponse(ch);
            var c = ch.ReadOutbound<IHttpContent>();
            Assert.NotNull(c);
            Assert.Equal(2, c.Content.ReadableBytes);
            Assert.Equal("42", c.Content.ToString(Encoding.ASCII));
            c.Release();

            var last = ch.ReadOutbound<ILastHttpContent>();
            Assert.NotNull(last);
            Assert.Equal(0, last.Content.ReadableBytes);
            last.Release();

            var next = ch.ReadOutbound<object>();
            Assert.Null(next);
        }

        // If the length of the content is unknown, {@link HttpContentEncoder} should not skip encoding the content
        // even if the actual length is turned out to be 0.
        [Fact]
        public void EmptySplitContent()
        {
            var ch = new EmbeddedChannel(new TestEncoder());
            ch.WriteInbound(new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/"));

            ch.WriteOutbound(new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK));

            AssertEncodedResponse(ch);

            ch.WriteOutbound(EmptyLastHttpContent.Default);

            var chunk = ch.ReadOutbound<IHttpContent>();
            Assert.NotNull(chunk);
            Assert.Equal("0", chunk.Content.ToString(Encoding.ASCII));
            chunk.Release();

            var last = ch.ReadOutbound<ILastHttpContent>();
            Assert.False(last.Content.IsReadable());
            last.Release();

            var next = ch.ReadOutbound<object>();
            Assert.Null(next);
        }

        // If the length of the content is 0 for sure, {@link HttpContentEncoder} should skip encoding.
        [Fact]
        public void EmptyFullContent()
        {
            var ch = new EmbeddedChannel(new TestEncoder());
            ch.WriteInbound(new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/"));

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

            var next = ch.ReadOutbound<object>();
            Assert.Null(next);
        }

        [Fact]
        public void EmptyFullContentWithTrailer()
        {
            var ch = new EmbeddedChannel(new TestEncoder());
            ch.WriteInbound(new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/"));

            IFullHttpResponse res = new DefaultFullHttpResponse(
                HttpVersion.Http11, HttpResponseStatus.OK, Unpooled.Empty);
            res.TrailingHeaders.Set((AsciiString)"X-Test", (StringCharSequence)"Netty");
            ch.WriteOutbound(res);

            res = ch.ReadOutbound<IFullHttpResponse>();
            Assert.NotNull(res);
            Assert.False(res.Headers.TryGet(HttpHeaderNames.TransferEncoding, out _));

            // Content encoding shouldn't be modified.
            Assert.False(res.Headers.TryGet(HttpHeaderNames.ContentEncoding, out _));
            Assert.Equal(0, res.Content.ReadableBytes);
            Assert.Equal("", res.Content.ToString(Encoding.ASCII));
            Assert.Equal("Netty", res.TrailingHeaders.Get((AsciiString)"X-Test", null));

            var next = ch.ReadOutbound<object>();
            Assert.Null(next);
        }

        [Fact]
        public void EmptyHeadResponse()
        {
            var ch = new EmbeddedChannel(new TestEncoder());
            var req = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Head, "/");
            ch.WriteInbound(req);

            var res = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            res.Headers.Set(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);
            ch.WriteOutbound(res);
            ch.WriteOutbound(EmptyLastHttpContent.Default);

            AssertEmptyResponse(ch);
        }

        [Fact]
        public void Http304Response()
        {
            var ch = new EmbeddedChannel(new TestEncoder());
            var req = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Head, "/");
            req.Headers.Set(HttpHeaderNames.AcceptEncoding, HttpHeaderValues.Gzip);
            ch.WriteInbound(req);

            var res = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.NotModified);
            res.Headers.Set(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);
            ch.WriteOutbound(res);
            ch.WriteOutbound(EmptyLastHttpContent.Default);

            AssertEmptyResponse(ch);
        }

        [Fact]
        public void Connect200Response()
        {
            var ch = new EmbeddedChannel(new TestEncoder());
            var req = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Connect, "google.com:80");
            ch.WriteInbound(req);

            var res = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            res.Headers.Set(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);
            ch.WriteOutbound(res);
            ch.WriteOutbound(EmptyLastHttpContent.Default);

            AssertEmptyResponse(ch);
        }

        [Fact]
        public void ConnectFailureResponse()
        {
            const string Content = "Not allowed by configuration";

            var ch = new EmbeddedChannel(new TestEncoder());
            var req = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Connect, "google.com:80");
            ch.WriteInbound(req);

            var res = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.MethodNotAllowed);
            res.Headers.Set(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);
            ch.WriteOutbound(res);
            ch.WriteOutbound(new DefaultHttpContent(Unpooled.WrappedBuffer(Encoding.ASCII.GetBytes(Content))));
            ch.WriteOutbound(EmptyLastHttpContent.Default);

            AssertEncodedResponse(ch);

            var chunk = ch.ReadOutbound<IHttpContent>();
            Assert.NotNull(chunk);
            Assert.Equal("28", chunk.Content.ToString(Encoding.ASCII));
            chunk.Release();

            chunk = ch.ReadOutbound<IHttpContent>();
            Assert.True(chunk.Content.IsReadable());
            Assert.Equal("0", chunk.Content.ToString(Encoding.ASCII));
            chunk.Release();

            var last = ch.ReadOutbound<ILastHttpContent>();
            Assert.NotNull(last);
            last.Release();

            var next = ch.ReadOutbound<object>();
            Assert.Null(next);
        }

        [Fact]
        public void Http10()
        {
            var ch = new EmbeddedChannel(new TestEncoder());
            var req = new DefaultFullHttpRequest(HttpVersion.Http10, HttpMethod.Get, "/");
            Assert.True(ch.WriteInbound(req));

            var res = new DefaultHttpResponse(HttpVersion.Http10, HttpResponseStatus.OK);
            res.Headers.Set(HttpHeaderNames.ContentLength, HttpHeaderValues.Zero);
            Assert.True(ch.WriteOutbound(res));
            Assert.True(ch.WriteOutbound(EmptyLastHttpContent.Default));
            Assert.True(ch.Finish());

            var request = ch.ReadInbound<IFullHttpRequest>();
            Assert.True(request.Release());
            var next = ch.ReadInbound<object>();
            Assert.Null(next);

            var response = ch.ReadOutbound<IHttpResponse>();
            Assert.Same(res, response);

            var content = ch.ReadOutbound<ILastHttpContent>();
            Assert.Same(content, EmptyLastHttpContent.Default);
            content.Release();

            next = ch.ReadOutbound<object>();
            Assert.Null(next);
        }

        [Fact]
        public void CleanupThrows()
        {
            var encoder = new CleanupEncoder();
            var inboundHandler = new InboundHandler();
            var channel = new EmbeddedChannel(encoder, inboundHandler);

            Assert.True(channel.WriteInbound(new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/")));
            Assert.True(channel.WriteOutbound(new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK)));
            var content = new DefaultHttpContent(Unpooled.Buffer().WriteZero(10));
            Assert.True(channel.WriteOutbound(content));
            Assert.Equal(1, content.ReferenceCount);

            Assert.Throws<EncoderException>(() => channel.FinishAndReleaseAll());
            Assert.Equal(1, inboundHandler.ChannelInactiveCalled);
            Assert.Equal(0, content.ReferenceCount);
        }

        sealed class CleanupEncoder : HttpContentEncoder
        {
            protected override Result BeginEncode(IHttpResponse headers, ICharSequence acceptEncoding) =>
                new Result(new StringCharSequence("myencoding"), new EmbeddedChannel(new Handler()));

            sealed class Handler : ChannelHandlerAdapter
            {
                public override void ChannelInactive(IChannelHandlerContext context)
                {
                    context.FireExceptionCaught(new EncoderException("CleanupThrows"));
                    context.FireChannelInactive();
                }
            }
        }

        sealed class InboundHandler : ChannelHandlerAdapter
        {
            public int ChannelInactiveCalled;

            public override void ChannelInactive(IChannelHandlerContext context)
            {
                Interlocked.CompareExchange(ref this.ChannelInactiveCalled, 1, 0);
                base.ChannelInactive(context);
            }
        }

        static void AssertEmptyResponse(EmbeddedChannel ch)
        {
            var res = ch.ReadOutbound<IHttpResponse>();
            Assert.NotNull(res);
            Assert.False(res is IHttpContent);
            Assert.Equal("chunked", res.Headers.Get(HttpHeaderNames.TransferEncoding, null).ToString());
            Assert.False(res.Headers.TryGet(HttpHeaderNames.ContentLength, out _));

            var chunk = ch.ReadOutbound<ILastHttpContent>();
            Assert.NotNull(chunk);
            chunk.Release();

            var next = ch.ReadOutbound<object>();
            Assert.Null(next);
        }

        static void AssertEncodedResponse(EmbeddedChannel ch)
        {
            var res = ch.ReadOutbound<IHttpResponse>();
            Assert.NotNull(res);

            Assert.False(res is IHttpContent, $"{res.GetType()}");
            Assert.Equal("chunked", res.Headers.Get(HttpHeaderNames.TransferEncoding, null).ToString());
            Assert.False(res.Headers.TryGet(HttpHeaderNames.ContentLength, out _));
            Assert.Equal("test", res.Headers.Get(HttpHeaderNames.ContentEncoding, null).ToString());
        }
    }
}
