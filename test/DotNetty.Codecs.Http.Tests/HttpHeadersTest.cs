// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Common.Utilities;
    using Xunit;

    public sealed class HttpHeadersTest
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

        // Test for https://github.com/netty/netty/issues/1690
        [Fact]
        public void GetOperations()
        {
            HttpHeaders headers = new DefaultHttpHeaders();
            headers.Add(HttpHeadersTestUtils.Of("Foo"), HttpHeadersTestUtils.Of("1"));
            headers.Add(HttpHeadersTestUtils.Of("Foo"), HttpHeadersTestUtils.Of("2"));

            Assert.Equal("1", headers.Get(HttpHeadersTestUtils.Of("Foo"), null));

            IList<ICharSequence> values = headers.GetAll(HttpHeadersTestUtils.Of("Foo"));
            Assert.Equal(2, values.Count);
            Assert.Equal("1", values[0].ToString());
            Assert.Equal("2", values[1].ToString());
        }

        [Fact]
        public void EqualsIgnoreCase()
        {
            Assert.True(AsciiString.ContentEqualsIgnoreCase(null, null));
            Assert.False(AsciiString.ContentEqualsIgnoreCase(null, (StringCharSequence)"foo"));
            Assert.False(AsciiString.ContentEqualsIgnoreCase((StringCharSequence)"bar", null));
            Assert.True(AsciiString.ContentEqualsIgnoreCase((StringCharSequence)"FoO", (StringCharSequence)"fOo"));
        }

        [Fact]
        public void AddSelf()
        {
            HttpHeaders headers = new DefaultHttpHeaders(false);
            Assert.Throws<ArgumentException>(() => headers.Add(headers));
        }

        [Fact]
        public void SetSelfIsNoOp()
        {
            HttpHeaders headers = new DefaultHttpHeaders(false);
            headers.Add((AsciiString)"name", (StringCharSequence)"value");
            headers.Set(headers);
            Assert.Equal(1, headers.Size);
        }
    }
}
