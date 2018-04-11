// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System;
    using DotNetty.Common.Utilities;
    using Xunit;

    public sealed class HttpResponseStatusTest
    {
        [Fact]
        public void ParseLineStringJustCode()
        {
            Assert.Same(HttpResponseStatus.OK, HttpResponseStatus.ParseLine("200"));
        }

        [Fact]
        public void ParseLineStringCodeAndPhrase()
        {
            Assert.Same(HttpResponseStatus.OK, HttpResponseStatus.ParseLine("200 OK"));
        }

        [Fact]
        public void ParseLineStringCustomCode()
        {
            HttpResponseStatus customStatus = HttpResponseStatus.ParseLine("612");
            Assert.Equal(612, customStatus.Code);
        }

        [Fact]
        public void ParseLineStringCustomCodeAndPhrase()
        {
            HttpResponseStatus customStatus = HttpResponseStatus.ParseLine("612 FOO");
            Assert.Equal(612, customStatus.Code);
            Assert.Equal(new AsciiString("FOO"), customStatus.ReasonPhrase);
        }

        [Fact]
        public void ParseLineStringMalformedCode()
        {
            Assert.Throws<ArgumentException>(() => HttpResponseStatus.ParseLine("200a"));
        }

        [Fact]
        public void ParseLineStringMalformedCodeWithPhrase()
        {
            Assert.Throws<ArgumentException>(() => HttpResponseStatus.ParseLine("200a foo"));
        }

        [Fact]
        public void ParseLineAsciiStringJustCode()
        {
            Assert.Same(HttpResponseStatus.OK, HttpResponseStatus.ParseLine(new AsciiString("200")));
        }

        [Fact]
        public void ParseLineAsciiStringCodeAndPhrase()
        {
            Assert.Same(HttpResponseStatus.OK, HttpResponseStatus.ParseLine(new AsciiString("200 OK")));
        }

        [Fact]
        public void ParseLineAsciiStringCustomCode()
        {
            HttpResponseStatus customStatus = HttpResponseStatus.ParseLine(new AsciiString("612"));
            Assert.Equal(612, customStatus.Code);
        }

        [Fact]
        public void ParseLineAsciiStringCustomCodeAndPhrase()
        {
            HttpResponseStatus customStatus = HttpResponseStatus.ParseLine(new AsciiString("612 FOO"));
            Assert.Equal(612, customStatus.Code);
            Assert.Equal("FOO", customStatus.ReasonPhrase);
        }

        [Fact]
        public void ParseLineAsciiStringMalformedCode()
        {
            Assert.Throws<ArgumentException>(() => HttpResponseStatus.ParseLine(new AsciiString("200a")));
        }

        [Fact]
        public void ParseLineAsciiStringMalformedCodeWithPhrase()
        {
            Assert.Throws<ArgumentException>(() => HttpResponseStatus.ParseLine(new AsciiString("200a foo")));
        }
    }
}
