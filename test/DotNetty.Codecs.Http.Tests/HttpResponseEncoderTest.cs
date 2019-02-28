// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class HttpResponseEncoderTest
    {
        const long IntegerOverflow = (long)int.MaxValue + 1;
        static readonly IFileRegion FileRegion = new DummyLongFileRegion();

        [Fact]
        public void LargeFileRegionChunked()
        {
            var channel = new EmbeddedChannel(new HttpResponseEncoder());
            IHttpResponse response = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            response.Headers.Set(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);

            Assert.True(channel.WriteOutbound(response));

            var buffer = channel.ReadOutbound<IByteBuffer>();
            Assert.Equal(
                "HTTP/1.1 200 OK\r\n" + HttpHeaderNames.TransferEncoding + ": " +
                HttpHeaderValues.Chunked + "\r\n\r\n",
                buffer.ToString(Encoding.ASCII));
            buffer.Release();

            Assert.True(channel.WriteOutbound(FileRegion));
            buffer = channel.ReadOutbound<IByteBuffer>();
            Assert.Equal("80000000\r\n", buffer.ToString(Encoding.ASCII));
            buffer.Release();

            var region = channel.ReadOutbound<IFileRegion>();
            Assert.Same(FileRegion, region);
            region.Release();
            buffer = channel.ReadOutbound<IByteBuffer>();
            Assert.Equal("\r\n", buffer.ToString(Encoding.ASCII));
            buffer.Release();

            Assert.True(channel.WriteOutbound(EmptyLastHttpContent.Default));
            buffer = channel.ReadOutbound<IByteBuffer>();
            Assert.Equal("0\r\n\r\n", buffer.ToString(Encoding.ASCII));
            buffer.Release();

            Assert.False(channel.Finish());
        }

        class DummyLongFileRegion : IFileRegion
        {
            public int ReferenceCount => 1;

            public IReferenceCounted Retain() => this;

            public IReferenceCounted Retain(int increment) => this;

            public IReferenceCounted Touch() => this;

            public IReferenceCounted Touch(object hint) => this;

            public bool Release() => false;

            public bool Release(int decrement) => false;

            public long Position => 0;

            public long Transferred => 0;

            public long Count => IntegerOverflow;

            public long TransferTo(Stream target, long position)
            {
                throw new NotSupportedException();
            }
        }

        [Fact]
        public void EmptyBufferBypass()
        {
            var channel = new EmbeddedChannel(new HttpResponseEncoder());

            // Test writing an empty buffer works when the encoder is at ST_INIT.
            channel.WriteOutbound(Unpooled.Empty);
            var buffer = channel.ReadOutbound<IByteBuffer>();
            Assert.Same(buffer, Unpooled.Empty);

            // Leave the ST_INIT state.
            IHttpResponse response = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            Assert.True(channel.WriteOutbound(response));
            buffer = channel.ReadOutbound<IByteBuffer>();
            Assert.Equal("HTTP/1.1 200 OK\r\n\r\n", buffer.ToString(Encoding.ASCII));
            buffer.Release();

            // Test writing an empty buffer works when the encoder is not at ST_INIT.
            channel.WriteOutbound(Unpooled.Empty);
            buffer = channel.ReadOutbound<IByteBuffer>();
            Assert.Same(buffer, Unpooled.Empty);

            Assert.False(channel.Finish());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void EmptyContent(bool chunked)
        {
            const string Content = "netty rocks";
            IByteBuffer contentBuffer = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(Content));
            int length = contentBuffer.ReadableBytes;

            var channel = new EmbeddedChannel(new HttpResponseEncoder());
            IHttpResponse response = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            if (!chunked)
            {
                HttpUtil.SetContentLength(response, length);
            }
            Assert.True(channel.WriteOutbound(response));
            Assert.True(channel.WriteOutbound(new DefaultHttpContent(Unpooled.Empty)));
            Assert.True(channel.WriteOutbound(new DefaultLastHttpContent(contentBuffer)));

            var buffer = channel.ReadOutbound<IByteBuffer>();
            if (!chunked)
            {
                Assert.Equal(
                    "HTTP/1.1 200 OK\r\ncontent-length: " + length + "\r\n\r\n",
                    buffer.ToString(Encoding.ASCII));
            }
            else
            {
                Assert.Equal("HTTP/1.1 200 OK\r\n\r\n", buffer.ToString(Encoding.ASCII));
            }
            buffer.Release();

            // Test writing an empty buffer works when the encoder is not at ST_INIT.
            buffer = channel.ReadOutbound<IByteBuffer>();
            Assert.Equal(0, buffer.ReadableBytes);
            buffer.Release();

            buffer = channel.ReadOutbound<IByteBuffer>();
            Assert.Equal(length, buffer.ReadableBytes);
            buffer.Release();

            Assert.False(channel.Finish());
        }
    }
}
