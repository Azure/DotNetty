// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System.Collections.Generic;
    using DotNetty.Common.Utilities;
    using Xunit;

    public sealed class DefaultHttpHeadersTest
    {
        static readonly AsciiString HeaderName = new AsciiString("testHeader");

        [Fact]
        public void KeysShouldBeCaseInsensitive()
        {
            var headers = new DefaultHttpHeaders();
            headers.Add(HttpHeadersTestUtils.Of("Name"), HttpHeadersTestUtils.Of("value1"));
            headers.Add(HttpHeadersTestUtils.Of("name"), HttpHeadersTestUtils.Of("value2"));
            headers.Add(HttpHeadersTestUtils.Of("NAME"), HttpHeadersTestUtils.Of("value3"));
            Assert.Equal(3, headers.Size);

            var values = new List<ICharSequence>
            {
                HttpHeadersTestUtils.Of("value1"),
                HttpHeadersTestUtils.Of("value2"),
                HttpHeadersTestUtils.Of("value3")
            };

            Assert.Equal(values, headers.GetAll(HttpHeadersTestUtils.Of("NAME")));
            Assert.Equal(values, headers.GetAll(HttpHeadersTestUtils.Of("name")));
            Assert.Equal(values, headers.GetAll(HttpHeadersTestUtils.Of("Name")));
            Assert.Equal(values, headers.GetAll(HttpHeadersTestUtils.Of("nAmE")));
        }

        [Fact]
        public void KeysShouldBeCaseInsensitiveInHeadersEquals()
        {
            var headers1 = new DefaultHttpHeaders();
            headers1.Add(HttpHeadersTestUtils.Of("name1"), new[] { "value1", "value2", "value3" });
            headers1.Add(HttpHeadersTestUtils.Of("nAmE2"), HttpHeadersTestUtils.Of("value4"));

            var headers2 = new DefaultHttpHeaders();
            headers2.Add(HttpHeadersTestUtils.Of("naMe1"), new[] { "value1", "value2", "value3" });
            headers2.Add(HttpHeadersTestUtils.Of("NAME2"), HttpHeadersTestUtils.Of("value4"));

            Assert.True(Equals(headers1, headers2));
            Assert.True(Equals(headers2, headers1));
            Assert.Equal(headers1.GetHashCode(), headers2.GetHashCode());
        }

        [Fact]
        public void StringKeyRetrievedAsAsciiString()
        {
            var headers = new DefaultHttpHeaders(false);

            // Test adding String key and retrieving it using a AsciiString key
            const string Connection = "keep-alive";
            headers.Add(HttpHeadersTestUtils.Of("Connection"), Connection);

            // Passes
            headers.TryGetAsString(HttpHeaderNames.Connection, out string value);
            Assert.NotNull(value);
            Assert.Equal(Connection, value);

            // Passes
            ICharSequence value2 = headers.Get(HttpHeaderNames.Connection, null);
            Assert.NotNull(value2);
            Assert.Equal(Connection, value2);
        }

        [Fact]
        public void AsciiStringKeyRetrievedAsString()
        {
            var headers = new DefaultHttpHeaders(false);

            // Test adding AsciiString key and retrieving it using a String key
            const string CacheControl = "no-cache";
            headers.Add(HttpHeaderNames.CacheControl, CacheControl);

            headers.TryGetAsString(HttpHeaderNames.CacheControl, out string value);
            Assert.NotNull(value);
            Assert.Equal(CacheControl, value);

            ICharSequence value2 = headers.Get(HttpHeaderNames.CacheControl, null);
            Assert.NotNull(value2);
            Assert.Equal(CacheControl, value2);
        }

        [Fact]
        public void GetOperations()
        {
            var headers = new DefaultHttpHeaders();
            headers.Add(HttpHeadersTestUtils.Of("Foo"), HttpHeadersTestUtils.Of("1"));
            headers.Add(HttpHeadersTestUtils.Of("Foo"), HttpHeadersTestUtils.Of("2"));

            Assert.Equal("1", headers.Get(HttpHeadersTestUtils.Of("Foo"), null));

            IList<ICharSequence> values = headers.GetAll(HttpHeadersTestUtils.Of("Foo"));
            Assert.Equal(2, values.Count);
            Assert.Equal("1", values[0]);
            Assert.Equal("2", values[1]);
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
        public void AddCharSequences()
        {
            var headers = new DefaultHttpHeaders();
            headers.Add(HeaderName, HttpHeadersTestUtils.HeaderValue.Three.AsList());
            AssertDefaultValues(headers, HttpHeadersTestUtils.HeaderValue.Three);
        }

        [Fact]
        public void AddObjects()
        {
            var headers = new DefaultHttpHeaders();
            headers.Add(HeaderName, HttpHeadersTestUtils.HeaderValue.Three.AsList());
            AssertDefaultValues(headers, HttpHeadersTestUtils.HeaderValue.Three);
        }

        [Fact]
        public void SetCharSequences()
        {
            var headers = new DefaultHttpHeaders();
            headers.Set(HeaderName, HttpHeadersTestUtils.HeaderValue.Three.AsList());
            AssertDefaultValues(headers, HttpHeadersTestUtils.HeaderValue.Three);
        }

        static void AssertDefaultValues(HttpHeaders headers, HttpHeadersTestUtils.HeaderValue headerValue)
        {
            Assert.True(AsciiString.ContentEquals(headerValue.AsList()[0], (StringCharSequence)headers.Get(HeaderName, null)));
            List<ICharSequence> expected = headerValue.AsList();
            IList<ICharSequence> actual = headers.GetAll(HeaderName);
            Assert.Equal(expected.Count, actual.Count);

            for (int i =0; i < expected.Count; i++)
            {
                Assert.True(AsciiString.ContentEquals(expected[i], (StringCharSequence)actual[i]));
            }
        }
    }
}
