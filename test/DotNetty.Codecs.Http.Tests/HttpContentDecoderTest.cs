// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Compression;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class HttpContentDecoderTest
    {
        const string HelloWorld = "hello, world";
        static readonly byte[] GzHelloWorld = {
            31, (256 -117), 8, 8, 12, 3, (256 -74), 84, 0, 3, 50, 0, (256 -53), 72, (256 -51), (256 -55), (256 -55),
            (256 -41), 81, 40, (256 -49), 47, (256 -54), 73, 1, 0, 58, 114, (256 -85), (256 -1), 12, 0, 0, 0
        };

        [Fact]
        public void BinaryDecompression()
        {
            // baseline test: zlib library and test helpers work correctly.
            byte[] helloWorld = GzDecompress(GzHelloWorld);
            byte[] expected = Encoding.ASCII.GetBytes(HelloWorld);

            Assert.True(expected.SequenceEqual(helloWorld));

            const string FullCycleTest = "full cycle test";
            byte[] compressed = GzCompress(Encoding.ASCII.GetBytes(FullCycleTest));
            byte[] decompressed = GzDecompress(compressed);

            string result = Encoding.ASCII.GetString(decompressed);
            Assert.Equal(FullCycleTest, result);
        }

        [Fact]
        public void RequestDecompression()
        {
            // baseline test: request decoder, content decompressor && request aggregator work as expected
            var decoder = new HttpRequestDecoder();
            var decompressor = new HttpContentDecompressor();
            var aggregator = new HttpObjectAggregator(1024);
            var channel = new EmbeddedChannel(decoder, decompressor, aggregator);

            string headers = "POST / HTTP/1.1\r\n" +
                         "Content-Length: " + GzHelloWorld.Length + "\r\n" +
                         "Content-Encoding: gzip\r\n" +
                         "\r\n";
            IByteBuffer buf = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(headers), GzHelloWorld);
            Assert.True(channel.WriteInbound(buf));

            var req = channel.ReadInbound<IFullHttpRequest>();
            Assert.NotNull(req);
            Assert.True(req.Headers.TryGetInt(HttpHeaderNames.ContentLength, out int length));
            Assert.Equal(HelloWorld.Length, length);
            Assert.Equal(HelloWorld, req.Content.ToString(Encoding.ASCII));
            req.Release();

            AssertHasInboundMessages(channel, false);
            AssertHasOutboundMessages(channel, false);
            Assert.False(channel.Finish()); // assert that no messages are left in channel
        }

        [Fact]
        public void ResponseDecompression()
        {
            // baseline test: response decoder, content decompressor && request aggregator work as expected
            var decoder = new HttpResponseDecoder();
            var decompressor = new HttpContentDecompressor();
            var aggregator = new HttpObjectAggregator(1024);
            var channel = new EmbeddedChannel(decoder, decompressor, aggregator);

            string headers = "HTTP/1.1 200 OK\r\n" +
                             "Content-Length: " + GzHelloWorld.Length + "\r\n" +
                             "Content-Encoding: gzip\r\n" +
                             "\r\n";
            IByteBuffer buf = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(headers), GzHelloWorld);
            Assert.True(channel.WriteInbound(buf));

            var resp = channel.ReadInbound<IFullHttpResponse>();
            Assert.NotNull(resp);
            Assert.True(resp.Headers.TryGetInt(HttpHeaderNames.ContentLength, out int length));
            Assert.Equal(HelloWorld.Length, length);
            Assert.Equal(HelloWorld, resp.Content.ToString(Encoding.ASCII));
            resp.Release();

            AssertHasInboundMessages(channel, false);
            AssertHasOutboundMessages(channel, false);
            Assert.False(channel.Finish()); // assert that no messages are left in channel
        }

        [Fact]
        public void ExpectContinueResponse1()
        {
            // request with header "Expect: 100-continue" must be replied with one "100 Continue" response
            // case 1: no ContentDecoder in chain at all (baseline test)
            var decoder = new HttpRequestDecoder();
            var aggregator = new HttpObjectAggregator(1024);
            var channel = new EmbeddedChannel(decoder, aggregator);
            string req = "POST / HTTP/1.1\r\n" +
                         "Content-Length: " + GzHelloWorld.Length + "\r\n" +
                         "Expect: 100-continue\r\n" +
                         "\r\n";
            // note: the following writeInbound() returns false as there is no message is inbound buffer
            // until HttpObjectAggregator caches composes a complete message.
            // however, http response "100 continue" must be sent as soon as headers are received
            Assert.False(channel.WriteInbound(Unpooled.WrappedBuffer(Encoding.ASCII.GetBytes(req))));

            var resp = channel.ReadOutbound<IFullHttpResponse>();
            Assert.NotNull(resp);
            Assert.Equal(100, resp.Status.Code);
            Assert.True(channel.WriteInbound(Unpooled.WrappedBuffer(GzHelloWorld)));
            resp.Release();

            AssertHasInboundMessages(channel, true);
            AssertHasOutboundMessages(channel, false);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void ExpectContinueResponse2()
        {
            // request with header "Expect: 100-continue" must be replied with one "100 Continue" response
            // case 2: contentDecoder is in chain, but the content is not encoded, should be no-op
            var decoder = new HttpRequestDecoder();
            var decompressor = new HttpContentDecompressor();
            var aggregator = new HttpObjectAggregator(1024);
            var channel = new EmbeddedChannel(decoder, decompressor, aggregator);
            string req = "POST / HTTP/1.1\r\n" +
                         "Content-Length: " + GzHelloWorld.Length + "\r\n" +
                         "Expect: 100-continue\r\n" +
                         "\r\n";
            Assert.False(channel.WriteInbound(Unpooled.WrappedBuffer(Encoding.ASCII.GetBytes(req))));

            var resp = channel.ReadOutbound<IFullHttpResponse>();
            Assert.NotNull(resp);
            Assert.Equal(100, resp.Status.Code);
            resp.Release();
            Assert.True(channel.WriteInbound(Unpooled.WrappedBuffer(GzHelloWorld)));

            AssertHasInboundMessages(channel, true);
            AssertHasOutboundMessages(channel, false);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void ExpectContinueResponse3()
        {
            // request with header "Expect: 100-continue" must be replied with one "100 Continue" response
            // case 3: ContentDecoder is in chain and content is encoded
            var decoder = new HttpRequestDecoder();
            var decompressor = new HttpContentDecompressor();
            var aggregator = new HttpObjectAggregator(1024);
            var channel = new EmbeddedChannel(decoder, decompressor, aggregator);
            string req = "POST / HTTP/1.1\r\n" +
                         "Content-Length: " + GzHelloWorld.Length + "\r\n" +
                         "Expect: 100-continue\r\n" +
                         "Content-Encoding: gzip\r\n" +
                         "\r\n";
            Assert.False(channel.WriteInbound(Unpooled.WrappedBuffer(Encoding.ASCII.GetBytes(req))));

            var resp = channel.ReadOutbound<IFullHttpResponse>();
            Assert.Equal(100, resp.Status.Code);
            resp.Release();
            Assert.True(channel.WriteInbound(Unpooled.WrappedBuffer(GzHelloWorld)));

            AssertHasInboundMessages(channel, true);
            AssertHasOutboundMessages(channel, false);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void ExpectContinueResponse4()
        {
            // request with header "Expect: 100-continue" must be replied with one "100 Continue" response
            // case 4: ObjectAggregator is up in chain
            var decoder = new HttpRequestDecoder();
            var aggregator = new HttpObjectAggregator(1024);
            var decompressor = new HttpContentDecompressor();
            var channel = new EmbeddedChannel(decoder, aggregator, decompressor);
            string req = "POST / HTTP/1.1\r\n" +
                         "Content-Length: " + GzHelloWorld.Length + "\r\n" +
                         "Expect: 100-continue\r\n" +
                         "Content-Encoding: gzip\r\n" +
                         "\r\n";
            Assert.False(channel.WriteInbound(Unpooled.WrappedBuffer(Encoding.ASCII.GetBytes(req))));

            var resp = channel.ReadOutbound<IFullHttpResponse>();
            Assert.NotNull(resp);
            Assert.Equal(100, resp.Status.Code);
            resp.Release();
            Assert.True(channel.WriteInbound(Unpooled.WrappedBuffer(GzHelloWorld)));

            AssertHasInboundMessages(channel, true);
            AssertHasOutboundMessages(channel, false);
            Assert.False(channel.Finish());
        }

        sealed class TestHandler : ChannelHandlerAdapter
        {
            IFullHttpRequest request;

            public IFullHttpRequest Request => this.request;

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                if (message is IFullHttpRequest value)
                {
                    if (Interlocked.CompareExchange(ref this.request, value, null) != null)
                    {
                        value.Release();
                    }
                }
                else
                {
                    ReferenceCountUtil.Release(message);
                }
            }
        }

        [Fact]
        public void ExpectContinueResetHttpObjectDecoder()
        {
            // request with header "Expect: 100-continue" must be replied with one "100 Continue" response
            // case 5: Test that HttpObjectDecoder correctly resets its internal state after a failed expectation.
            var decoder = new HttpRequestDecoder();
            const int MaxBytes = 10;
            var aggregator = new HttpObjectAggregator(MaxBytes);

            var testHandler = new TestHandler();
            var channel = new EmbeddedChannel(decoder, aggregator, testHandler);
            string req1 = "POST /1 HTTP/1.1\r\n" +
                "Content-Length: " + (MaxBytes + 1) + "\r\n" +
                "Expect: 100-continue\r\n" +
                "\r\n";
            Assert.False(channel.WriteInbound(Unpooled.WrappedBuffer(Encoding.ASCII.GetBytes(req1))));

            var resp = channel.ReadOutbound<IFullHttpResponse>();
            Assert.Equal(HttpStatusClass.ClientError, resp.Status.CodeClass);
            resp.Release();

            string req2 = "POST /2 HTTP/1.1\r\n" +
                "Content-Length: " + MaxBytes + "\r\n" +
                "Expect: 100-continue\r\n" +
                "\r\n";
            Assert.False(channel.WriteInbound(Unpooled.WrappedBuffer(Encoding.ASCII.GetBytes(req2))));

            resp = channel.ReadOutbound<IFullHttpResponse>();
            Assert.Equal(100, resp.Status.Code);
            resp.Release();

            var content = new byte[MaxBytes];
            Assert.False(channel.WriteInbound(Unpooled.WrappedBuffer(content)));

            IFullHttpRequest req = testHandler.Request;
            Assert.NotNull(req);
            Assert.Equal("/2", req.Uri);
            Assert.Equal(10, req.Content.ReadableBytes);
            req.Release();

            AssertHasInboundMessages(channel, false);
            AssertHasOutboundMessages(channel, false);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void RequestContentLength1()
        {
            // case 1: test that ContentDecompressor either sets the correct Content-Length header
            // or removes it completely (handlers down the chain must rely on LastHttpContent object)

            // force content to be in more than one chunk (5 bytes/chunk)
            var decoder = new HttpRequestDecoder(4096, 4096, 5);
            var decompressor = new HttpContentDecompressor();
            var channel = new EmbeddedChannel(decoder, decompressor);
            string headers = "POST / HTTP/1.1\r\n" +
                "Content-Length: " + GzHelloWorld.Length + "\r\n" +
                "Content-Encoding: gzip\r\n" +
                "\r\n";
            IByteBuffer buf = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(headers), GzHelloWorld);
            Assert.True(channel.WriteInbound(buf));

            Queue<object> req = channel.InboundMessages;
            Assert.True(req.Count >= 1);
            object o = req.Peek();
            Assert.IsAssignableFrom<IHttpRequest>(o);
            var request = (IHttpRequest)o;
            if (request.Headers.TryGet(HttpHeaderNames.ContentLength, out ICharSequence v))
            {
                Assert.Equal(HelloWorld.Length, long.Parse(v.ToString()));
            }

            AssertHasInboundMessages(channel, true);
            AssertHasOutboundMessages(channel, false);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void RequestContentLength2()
        {
            // case 2: if HttpObjectAggregator is down the chain, then correct Content-Length header must be set

            // force content to be in more than one chunk (5 bytes/chunk)
            var decoder = new HttpRequestDecoder(4096, 4096, 5);
            var decompressor = new HttpContentDecompressor();
            var aggregator = new HttpObjectAggregator(1024);
            var channel = new EmbeddedChannel(decoder, decompressor, aggregator);
            string headers = "POST / HTTP/1.1\r\n" +
                "Content-Length: " + GzHelloWorld.Length + "\r\n" +
                "Content-Encoding: gzip\r\n" +
                "\r\n";
            IByteBuffer buf = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(headers), GzHelloWorld);
            Assert.True(channel.WriteInbound(buf));

            var req = channel.ReadInbound<IFullHttpRequest>();
            Assert.NotNull(req);
            Assert.True(req.Headers.TryGet(HttpHeaderNames.ContentLength, out ICharSequence value));
            Assert.Equal(HelloWorld.Length, long.Parse(value.ToString()));
            req.Release();

            AssertHasInboundMessages(channel, false);
            AssertHasOutboundMessages(channel, false);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void ResponseContentLength1()
        {
            // case 1: test that ContentDecompressor either sets the correct Content-Length header
            // or removes it completely (handlers down the chain must rely on LastHttpContent object)

            // force content to be in more than one chunk (5 bytes/chunk)
            var decoder = new HttpResponseDecoder(4096, 4096, 5);
            var decompressor = new HttpContentDecompressor();
            var channel = new EmbeddedChannel(decoder, decompressor);
            string headers = "HTTP/1.1 200 OK\r\n" +
                "Content-Length: " + GzHelloWorld.Length + "\r\n" +
                "Content-Encoding: gzip\r\n" +
                "\r\n";
            IByteBuffer buf = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(headers), GzHelloWorld);
            Assert.True(channel.WriteInbound(buf));

            Queue<object> resp = channel.InboundMessages;
            Assert.True(resp.Count >= 1);
            object o = resp.Peek();
            Assert.IsAssignableFrom<IHttpResponse>(o);
            var r = (IHttpResponse)o;

            Assert.False(r.Headers.Contains(HttpHeaderNames.ContentLength));
            Assert.True(r.Headers.TryGet(HttpHeaderNames.TransferEncoding, out ICharSequence transferEncoding));
            Assert.NotNull(transferEncoding);
            Assert.Equal(HttpHeaderValues.Chunked, transferEncoding);

            AssertHasInboundMessages(channel, true);
            AssertHasOutboundMessages(channel, false);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void ResponseContentLength2()
        {
            // case 2: if HttpObjectAggregator is down the chain, then correct Content - Length header must be set

            // force content to be in more than one chunk (5 bytes/chunk)
            var decoder = new HttpResponseDecoder(4096, 4096, 5);
            var decompressor = new HttpContentDecompressor();
            var aggregator = new HttpObjectAggregator(1024);
            var channel = new EmbeddedChannel(decoder, decompressor, aggregator);
            string headers = "HTTP/1.1 200 OK\r\n" +
                             "Content-Length: " + GzHelloWorld.Length + "\r\n" +
                             "Content-Encoding: gzip\r\n" +
                             "\r\n";
            IByteBuffer buf = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(headers), GzHelloWorld);
            Assert.True(channel.WriteInbound(buf));

            var res = channel.ReadInbound<IFullHttpResponse>();
            Assert.NotNull(res);
            Assert.True(res.Headers.TryGet(HttpHeaderNames.ContentLength, out ICharSequence value));
            Assert.Equal(HelloWorld.Length, long.Parse(value.ToString()));
            res.Release();

            AssertHasInboundMessages(channel, false);
            AssertHasOutboundMessages(channel, false);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void FullHttpRequest()
        {
            // test that ContentDecoder can be used after the ObjectAggregator
            var decoder = new HttpRequestDecoder(4096, 4096, 5);
            var aggregator = new HttpObjectAggregator(1024);
            var decompressor = new HttpContentDecompressor();
            var channel = new EmbeddedChannel(decoder, aggregator, decompressor);
            string headers = "POST / HTTP/1.1\r\n" +
                             "Content-Length: " + GzHelloWorld.Length + "\r\n" +
                             "Content-Encoding: gzip\r\n" +
                             "\r\n";
            Assert.True(channel.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(headers), GzHelloWorld)));

            Queue<object> req = channel.InboundMessages;
            Assert.True(req.Count > 1);
            int contentLength = 0;
            contentLength = CalculateContentLength(req, contentLength);
            byte[] receivedContent = ReadContent(req, contentLength, true);
            Assert.Equal(HelloWorld, Encoding.ASCII.GetString(receivedContent));

            AssertHasInboundMessages(channel, true);
            AssertHasOutboundMessages(channel, false);
            Assert.False(channel.Finish());
        }

        [Fact]
        public void FullHttpResponse()
        {
            // test that ContentDecoder can be used after the ObjectAggregator
            var decoder = new HttpResponseDecoder(4096, 4096, 5);
            var aggregator = new HttpObjectAggregator(1024);
            var decompressor = new HttpContentDecompressor();
            var channel = new EmbeddedChannel(decoder, aggregator, decompressor);
            string headers = "HTTP/1.1 200 OK\r\n" +
                             "Content-Length: " + GzHelloWorld.Length + "\r\n" +
                             "Content-Encoding: gzip\r\n" +
                             "\r\n";
            Assert.True(channel.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(headers), GzHelloWorld)));

            Queue<object> resp = channel.InboundMessages;
            Assert.True(resp.Count > 1);
            int contentLength = 0;
            contentLength = CalculateContentLength(resp, contentLength);
            byte[] receivedContent = ReadContent(resp, contentLength, true);
            Assert.Equal(HelloWorld, Encoding.ASCII.GetString(receivedContent));

            AssertHasInboundMessages(channel, true);
            AssertHasOutboundMessages(channel, false);
            Assert.False(channel.Finish());
        }

        // See https://github.com/netty/netty/issues/5892
        [Fact]
        public void FullHttpResponseEOF()
        {
            // test that ContentDecoder can be used after the ObjectAggregator
            var decoder = new HttpResponseDecoder(4096, 4096, 5);
            var decompressor = new HttpContentDecompressor();
            var channel = new EmbeddedChannel(decoder, decompressor);
            string headers = "HTTP/1.1 200 OK\r\n" +
                    "Content-Encoding: gzip\r\n" +
                    "\r\n";
            Assert.True(channel.WriteInbound(Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(headers), GzHelloWorld)));
            // This should terminate it.
            Assert.True(channel.Finish());

            Queue<object> resp = channel.InboundMessages;
            Assert.True(resp.Count > 1);
            int contentLength = 0;
            contentLength = CalculateContentLength(resp, contentLength);
            byte[] receivedContent = ReadContent(resp, contentLength, false);
            Assert.Equal(HelloWorld, Encoding.ASCII.GetString(receivedContent));

            AssertHasInboundMessages(channel, true);
            AssertHasOutboundMessages(channel, false);
            Assert.False(channel.Finish());
        }

        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
        static byte[] ReadContent(IEnumerable<object> req, int contentLength, bool hasTransferEncoding)
        {
            var receivedContent = new byte[contentLength];
            int readCount = 0;
            foreach (object o in req)
            {
                if (o is IHttpContent content)
                {
                    int readableBytes = content.Content.ReadableBytes;
                    content.Content.ReadBytes(receivedContent, readCount, readableBytes);
                    readCount += readableBytes;
                }

                if (o is IHttpMessage message)
                {
                    Assert.Equal(hasTransferEncoding, message.Headers.Contains(HttpHeaderNames.TransferEncoding));
                }
            }

            return receivedContent;
        }

        [Fact]
        public void CleanupThrows()
        {
            var decoder = new CleanupDecoder();
            var inboundHandler = new InboundHandler();
            var channel = new EmbeddedChannel(decoder, inboundHandler);

            Assert.True(channel.WriteInbound(new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/")));
            var content = new DefaultHttpContent(Unpooled.Buffer().WriteZero(10));
            Assert.True(channel.WriteInbound(content));
            Assert.Equal(1, content.ReferenceCount);

            Assert.Throws<DecoderException>(() => channel.FinishAndReleaseAll());
            Assert.Equal(1, inboundHandler.ChannelInactiveCalled);
            Assert.Equal(0, content.ReferenceCount);
        }

        sealed class CleanupDecoder : HttpContentDecoder
        {
            protected override EmbeddedChannel NewContentDecoder(ICharSequence contentEncoding) => new EmbeddedChannel(new Handler());

            sealed class Handler : ChannelHandlerAdapter
            {
                public override void ChannelInactive(IChannelHandlerContext context)
                {
                    context.FireExceptionCaught(new DecoderException("CleanupThrows"));
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

        static int CalculateContentLength(IEnumerable<object> req, int contentLength)
        {
            foreach (object o in req)
            {
                if (o is IHttpContent content)
                {
                    Assert.True(content.ReferenceCount > 0);
                    contentLength += content.Content.ReadableBytes;
                }
            }

            return contentLength;
        }

        static byte[] GzCompress(byte[] input)
        {
            ZlibEncoder encoder = ZlibCodecFactory.NewZlibEncoder(ZlibWrapper.Gzip);
            var channel = new EmbeddedChannel(encoder);
            Assert.True(channel.WriteOutbound(Unpooled.WrappedBuffer(input)));
            Assert.True(channel.Finish());  // close the channel to indicate end-of-data

            int outputSize = 0;
            IByteBuffer o;
            var outbound = new List<IByteBuffer>();
            while ((o = channel.ReadOutbound<IByteBuffer>()) != null)
            {
                outbound.Add(o);
                outputSize += o.ReadableBytes;
            }

            var output = new byte[outputSize];
            int readCount = 0;
            foreach (IByteBuffer b in outbound)
            {
                int readableBytes = b.ReadableBytes;
                b.ReadBytes(output, readCount, readableBytes);
                b.Release();
                readCount += readableBytes;
            }
            Assert.True(channel.InboundMessages.Count == 0 && channel.OutboundMessages.Count == 0);

            return output;
        }

        static byte[] GzDecompress(byte[] input)
        {
            ZlibDecoder decoder = ZlibCodecFactory.NewZlibDecoder(ZlibWrapper.Gzip);
            var channel = new EmbeddedChannel(decoder);
            Assert.True(channel.WriteInbound(Unpooled.WrappedBuffer(input)));
            Assert.True(channel.Finish()); // close the channel to indicate end-of-data

            int outputSize = 0;
            IByteBuffer o;
            var inbound = new List<IByteBuffer>();
            while ((o = channel.ReadInbound<IByteBuffer>()) != null)
            {
                inbound.Add(o);
                outputSize += o.ReadableBytes;
            }

            var output = new byte[outputSize];
            int readCount = 0;
            foreach (IByteBuffer b in inbound)
            {
                int readableBytes = b.ReadableBytes;
                b.ReadBytes(output, readCount, readableBytes);
                b.Release();
                readCount += readableBytes;
            }
            Assert.True(channel.InboundMessages.Count == 0 && channel.OutboundMessages.Count == 0);

            return output;
        }

        static void AssertHasInboundMessages(EmbeddedChannel channel, bool hasMessages)
        {
            object o;
            if (hasMessages)
            {
                while (true)
                {
                    o = channel.ReadInbound<object>();
                    Assert.NotNull(o);
                    ReferenceCountUtil.Release(o);
                    if (o is ILastHttpContent)
                    {
                        break;
                    }
                }
            }
            else
            {
                o = channel.ReadInbound<object>();
                Assert.Null(o);
            }
        }

        static void AssertHasOutboundMessages(EmbeddedChannel channel, bool hasMessages)
        {
            object o;
            if (hasMessages)
            {
                while (true)
                {
                    o = channel.ReadOutbound<object>();
                    Assert.NotNull(o);
                    ReferenceCountUtil.Release(o);
                    if (o is ILastHttpContent)
                    {
                        break;
                    }
                }
            }
            else
            {
                o = channel.ReadOutbound<object>();
                Assert.Null(o);
            }
        }
    }
}
