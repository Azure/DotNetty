// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class HttpInvalidMessageTest
    {
        readonly Random rnd = new Random();

        [Fact]
        public void RequestWithBadInitialLine()
        {
            var ch = new EmbeddedChannel(new HttpRequestDecoder());
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("GET / HTTP/1.0 with extra\r\n")));
            var req = ch.ReadInbound<IHttpRequest>();
            DecoderResult dr = req.Result;
            Assert.NotNull(dr);
            Assert.False(dr.IsSuccess);
            Assert.True(dr.IsFailure);
            this.EnsureInboundTrafficDiscarded(ch);
        }

        [Fact]
        public void RequestWithBadHeader()
        {
            var ch = new EmbeddedChannel(new HttpRequestDecoder());
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("GET /maybe-something HTTP/1.0\r\n")));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("Good_Name: Good Value\r\n")));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("Bad=Name: Bad Value\r\n")));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("\r\n")));
            var req = ch.ReadInbound<IHttpRequest>();
            DecoderResult dr = req.Result;
            Assert.NotNull(dr);
            Assert.False(dr.IsSuccess);
            Assert.True(dr.IsFailure);
            Assert.Equal("Good Value", req.Headers.Get((AsciiString)"Good_Name", null).ToString());
            Assert.Equal("/maybe-something", req.Uri);
            this.EnsureInboundTrafficDiscarded(ch);
        }

        [Fact]
        public void ResponseWithBadInitialLine()
        {
            var ch = new EmbeddedChannel(new HttpResponseDecoder());
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("HTTP/1.0 BAD_CODE Bad Server\r\n")));
            var res = ch.ReadInbound<IHttpResponse>();
            DecoderResult dr = res.Result;
            Assert.NotNull(dr);
            Assert.False(dr.IsSuccess);
            Assert.True(dr.IsFailure);
            this.EnsureInboundTrafficDiscarded(ch);
        }

        [Fact]
        public void ResponseWithBadHeader()
        {
            var ch = new EmbeddedChannel(new HttpResponseDecoder());
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("HTTP/1.0 200 Maybe OK\r\n")));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("Good_Name: Good Value\r\n")));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("Bad=Name: Bad Value\r\n")));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("\r\n")));
            var res = ch.ReadInbound<IHttpResponse>();
            DecoderResult dr = res.Result;
            Assert.NotNull(dr);
            Assert.False(dr.IsSuccess);
            Assert.True(dr.IsFailure);
            Assert.Equal("Maybe OK", res.Status.ReasonPhrase);
            Assert.Equal("Good Value", res.Headers.Get((AsciiString)"Good_Name", null).ToString());
            this.EnsureInboundTrafficDiscarded(ch);
        }

        [Fact]
        public void BadChunk()
        {
            var ch = new EmbeddedChannel(new HttpRequestDecoder());
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("GET / HTTP/1.0\r\n")));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("Transfer-Encoding: chunked\r\n\r\n")));
            ch.WriteInbound(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("BAD_LENGTH\r\n")));
            var req = ch.ReadInbound<IHttpRequest>();
            DecoderResult dr = req.Result;
            Assert.NotNull(dr);
            Assert.True(dr.IsSuccess);
            var chunk = ch.ReadInbound<ILastHttpContent>();
            dr = chunk.Result;
            Assert.False(dr.IsSuccess);
            Assert.True(dr.IsFailure);
            this.EnsureInboundTrafficDiscarded(ch);
        }

        void EnsureInboundTrafficDiscarded(EmbeddedChannel ch)
        {
            // Generate a lot of random traffic to ensure that it's discarded silently.
            var data = new byte[1048576];
            this.rnd.NextBytes(data);

            IByteBuffer buf = Unpooled.WrappedBuffer(data);
            for (int i = 0; i < 4096; i++)
            {
                buf.SetIndex(0, data.Length);
                ch.WriteInbound(buf.Retain());
                ch.CheckException();
                Assert.Null(ch.ReadInbound<object>());
            }
            buf.Release();
        }
    }
}
