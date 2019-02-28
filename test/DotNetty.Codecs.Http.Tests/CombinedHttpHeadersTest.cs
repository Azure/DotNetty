// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using DotNetty.Common.Utilities;
    using Xunit;

    using static Common.Utilities.AsciiString;
    using static HttpHeadersTestUtils;

    public sealed class CombinedHttpHeadersTest
    {
        static readonly AsciiString HeaderName = new AsciiString("testHeader");

        [Fact]
        public void AddCharSequencesCsv()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Add(HeaderName, HeaderValue.Three.AsList());
            AssertCsvValues(headers, HeaderValue.Three);
        }

        [Fact]
        public void AddCharSequencesCsvWithExistingHeader()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Add(HeaderName, HeaderValue.Three.AsList());
            headers.Add(HeaderName, HeaderValue.Five.Subset(4));
            AssertCsvValues(headers, HeaderValue.Five);
        }

        [Fact]
        public void AddCombinedHeadersWhenEmpty()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            CombinedHttpHeaders otherHeaders = NewCombinedHttpHeaders();
            otherHeaders.Add(HeaderName, "a");
            otherHeaders.Add(HeaderName, "b");
            headers.Add(otherHeaders);
            Assert.Equal("a,b", headers.Get(HeaderName, null)?.ToString());
        }

        [Fact]
        public void AddCombinedHeadersWhenNotEmpty()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Add(HeaderName, "a");
            CombinedHttpHeaders otherHeaders = NewCombinedHttpHeaders();
            otherHeaders.Add(HeaderName, "b");
            otherHeaders.Add(HeaderName, "c");
            headers.Add(otherHeaders);
            Assert.Equal("a,b,c", headers.Get(HeaderName, null)?.ToString());
        }

        [Fact]
        public void SetCombinedHeadersWhenNotEmpty()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Add(HeaderName, "a");
            CombinedHttpHeaders otherHeaders = NewCombinedHttpHeaders();
            otherHeaders.Add(HeaderName, "b");
            otherHeaders.Add(HeaderName, "c");
            headers.Set(otherHeaders);
            Assert.Equal("b,c", headers.Get(HeaderName, null)?.ToString());
        }

        [Fact]
        public void AddUncombinedHeaders()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Add(HeaderName, "a");
            var otherHeaders = new DefaultHttpHeaders();
            otherHeaders.Add(HeaderName, "b");
            otherHeaders.Add(HeaderName, "c");
            headers.Add(otherHeaders);
            Assert.Equal("a,b,c", headers.Get(HeaderName, null)?.ToString());
        }

        [Fact]
        public void SetUncombinedHeaders()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Add(HeaderName, "a");
            var otherHeaders = new DefaultHttpHeaders();
            otherHeaders.Add(HeaderName, "b");
            otherHeaders.Add(HeaderName, "c");
            headers.Set(otherHeaders);
            Assert.Equal("b,c", headers.Get(HeaderName, null)?.ToString());
        }

        [Fact]
        public void AddCharSequencesCsvWithValueContainingComma()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Add(HeaderName, HeaderValue.SixQuoted.Subset(4));
            Assert.True(ContentEquals((StringCharSequence)HeaderValue.SixQuoted.SubsetAsCsvString(4), headers.Get(HeaderName, null)));
            Assert.Equal(HeaderValue.SixQuoted.Subset(4), headers.GetAll(HeaderName));
        }

        [Fact]
        public void AddCharSequencesCsvWithValueContainingCommas()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Add(HeaderName, HeaderValue.Eight.Subset(6));
            Assert.True(ContentEquals((StringCharSequence)HeaderValue.Eight.SubsetAsCsvString(6), headers.Get(HeaderName, null)));
            Assert.Equal(HeaderValue.Eight.Subset(6), headers.GetAll(HeaderName));
        }

        [Fact]
        public void AddCharSequencesCsvMultipleTimes()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            for (int i = 0; i < 5; ++i)
            {
                headers.Add(HeaderName, "value");
            }
            Assert.True(ContentEquals((StringCharSequence)"value,value,value,value,value", headers.Get(HeaderName, null)));
        }

        [Fact]
        public void AddCharSequenceCsv()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            AddValues(headers, HeaderValue.One, HeaderValue.Two, HeaderValue.Three);
            AssertCsvValues(headers, HeaderValue.Three);
        }

        [Fact]
        public void AddCharSequenceCsvSingleValue()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            AddValues(headers, HeaderValue.One);
            AssertCsvValue(headers, HeaderValue.One);
        }

        [Fact]
        public void AddIterableCsv()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Add(HeaderName, HeaderValue.Three.AsList());
            AssertCsvValues(headers, HeaderValue.Three);
        }

        [Fact]
        public void AddIterableCsvWithExistingHeader()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Add(HeaderName, HeaderValue.Three.AsList());
            headers.Add(HeaderName, HeaderValue.Five.Subset(4));
            AssertCsvValues(headers, HeaderValue.Five);
        }

        [Fact]
        public void AddIterableCsvSingleValue()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Add(HeaderName, HeaderValue.One.AsList());
            AssertCsvValue(headers, HeaderValue.One);
        }

        [Fact]
        public void AddIterableCsvEmpty()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Add(HeaderName, new List<ICharSequence>());
            Assert.Equal(0, headers.GetAll(HeaderName).Count);
        }

        [Fact]
        public void AddObjectCsv()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            AddObjectValues(headers, HeaderValue.One, HeaderValue.Two, HeaderValue.Three);
            AssertCsvValues(headers, HeaderValue.Three);
        }

        [Fact]
        public void AddObjectsCsv()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            List<ICharSequence> list = HeaderValue.Three.AsList();
            Assert.Equal(3, list.Count);
            headers.Add(HeaderName, list);
            AssertCsvValues(headers, HeaderValue.Three);
        }

        [Fact]
        public void AddObjectsIterableCsv()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Add(HeaderName, HeaderValue.Three.AsList());
            AssertCsvValues(headers, HeaderValue.Three);
        }

        [Fact]
        public void AddObjectsCsvWithExistingHeader()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Add(HeaderName, HeaderValue.Three.AsList());
            headers.Add(HeaderName, HeaderValue.Five.Subset(4));
            AssertCsvValues(headers, HeaderValue.Five);
        }

        [Fact]
        public void SetCharSequenceCsv()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Set(HeaderName, HeaderValue.Three.AsList());
            AssertCsvValues(headers, HeaderValue.Three);
        }

        [Fact]
        public void SetIterableCsv()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Set(HeaderName, HeaderValue.Three.AsList());
            AssertCsvValues(headers, HeaderValue.Three);
        }

        [Fact]
        public void SetObjectObjectsCsv()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Set(HeaderName, HeaderValue.Three.AsList());
            AssertCsvValues(headers, HeaderValue.Three);
        }

        [Fact]
        public void SetObjectIterableCsv()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Set(HeaderName, HeaderValue.Three.AsList());
            AssertCsvValues(headers, HeaderValue.Three);
        }

        static CombinedHttpHeaders NewCombinedHttpHeaders() => new CombinedHttpHeaders(true);

        static void AssertCsvValues(CombinedHttpHeaders headers, HeaderValue headerValue)
        {
            Assert.True(ContentEquals(headerValue.AsCsv(), headers.Get(HeaderName, null)));

            List<ICharSequence> expected = headerValue.AsList();
            IList<ICharSequence> values = headers.GetAll(HeaderName);

            Assert.Equal(expected.Count, values.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.True(expected[i].ContentEquals(values[i]));
            }
        }

        static void AssertCsvValue(CombinedHttpHeaders headers, HeaderValue headerValue)
        {
            Assert.True(ContentEquals((StringCharSequence)headerValue.ToString(), headers.Get(HeaderName, null)));
            Assert.True(ContentEquals((StringCharSequence)headerValue.ToString(), headers.GetAll(HeaderName)[0]));
        }

        static void AddValues(CombinedHttpHeaders headers, params HeaderValue[] headerValues)
        {
            foreach (HeaderValue v in headerValues)
            {
                headers.Add(HeaderName, (StringCharSequence)v.ToString());
            }
        }

        static void AddObjectValues(CombinedHttpHeaders headers, params HeaderValue[] headerValues)
        {
            foreach (HeaderValue v in headerValues)
            {
                headers.Add(HeaderName, v.ToString());
            }
        }

        [Fact]
        public void GetAll()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Set(HeaderName, new List<ICharSequence> { (StringCharSequence)"a", (StringCharSequence)"b", (StringCharSequence)"c" });
            var expected = new ICharSequence[] { (StringCharSequence)"a", (StringCharSequence)"b", (StringCharSequence)"c" };
            IList<ICharSequence> actual = headers.GetAll(HeaderName);
            Assert.True(expected.SequenceEqual(actual));

            headers.Set(HeaderName, new List<ICharSequence> { (StringCharSequence)"a,", (StringCharSequence)"b,", (StringCharSequence)"c," });
            expected = new ICharSequence[] { (StringCharSequence)"a,", (StringCharSequence)"b,", (StringCharSequence)"c," };
            actual = headers.GetAll(HeaderName);
            Assert.True(expected.SequenceEqual(actual));

            headers.Set(HeaderName, new List<ICharSequence> { (StringCharSequence)"a\"", (StringCharSequence)"b\"", (StringCharSequence)"c\"" });
            expected = new ICharSequence[] { (StringCharSequence)"a\"", (StringCharSequence)"b\"", (StringCharSequence)"c\"" };
            actual = headers.GetAll(HeaderName);
            Assert.True(expected.SequenceEqual(actual));

            headers.Set(HeaderName, new List<ICharSequence> { (StringCharSequence)"\"a\"", (StringCharSequence)"\"b\"", (StringCharSequence)"\"c\"" });
            expected = new ICharSequence[] { (StringCharSequence)"a", (StringCharSequence)"b", (StringCharSequence)"c" };
            actual = headers.GetAll(HeaderName);
            Assert.True(expected.SequenceEqual(actual));

            headers.Set(HeaderName, (StringCharSequence)"a,b,c");
            expected = new ICharSequence[] { (StringCharSequence)"a,b,c" };
            actual = headers.GetAll(HeaderName);
            Assert.True(expected.SequenceEqual(actual));

            headers.Set(HeaderName, (StringCharSequence)"\"a,b,c\"");
            actual = headers.GetAll(HeaderName);
            Assert.True(expected.SequenceEqual(actual));
        }

        [Fact]
        public void OwsTrimming()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Set(HeaderName, new List<ICharSequence> { (StringCharSequence)"\ta", (StringCharSequence)"   ", (StringCharSequence)"  b ", (StringCharSequence)"\t \t"});
            headers.Add(HeaderName, new List<ICharSequence> { (StringCharSequence)" c, d \t" });

            var expected = new List<ICharSequence> { (StringCharSequence)"a", (StringCharSequence)"", (StringCharSequence)"b", (StringCharSequence)"", (StringCharSequence)"c, d" };
            IList<ICharSequence> actual = headers.GetAll(HeaderName);
            Assert.True(expected.SequenceEqual(actual));
            Assert.Equal("a,,b,,\"c, d\"", headers.Get(HeaderName, null)?.ToString());

            Assert.True(headers.ContainsValue(HeaderName, (StringCharSequence)"a", true));
            Assert.True(headers.ContainsValue(HeaderName, (StringCharSequence)" a ", true));
            Assert.True(headers.ContainsValue(HeaderName, (StringCharSequence)"a", true));
            Assert.False(headers.ContainsValue(HeaderName, (StringCharSequence)"a,b", true));

            Assert.False(headers.ContainsValue(HeaderName, (StringCharSequence)" c, d ", true));
            Assert.False(headers.ContainsValue(HeaderName, (StringCharSequence)"c, d", true));
            Assert.True(headers.ContainsValue(HeaderName, (StringCharSequence)" c ", true));
            Assert.True(headers.ContainsValue(HeaderName, (StringCharSequence)"d", true));

            Assert.True(headers.ContainsValue(HeaderName, (StringCharSequence)"\t", true));
            Assert.True(headers.ContainsValue(HeaderName, (StringCharSequence)"", true));

            Assert.False(headers.ContainsValue(HeaderName, (StringCharSequence)"e", true));

            CombinedHttpHeaders copiedHeaders = NewCombinedHttpHeaders();
            copiedHeaders.Add(headers);
            Assert.Equal(new List<ICharSequence>{ (StringCharSequence)"a", (StringCharSequence)"", (StringCharSequence)"b", (StringCharSequence)"", (StringCharSequence)"c, d" }, copiedHeaders.GetAll(HeaderName));
        }

        [Fact]
        public void ValueIterator()
        {
            CombinedHttpHeaders headers = NewCombinedHttpHeaders();
            headers.Set(HeaderName, new List<ICharSequence> { (StringCharSequence)"\ta", (StringCharSequence)"   ", (StringCharSequence)"  b ", (StringCharSequence)"\t \t" });
            headers.Add(HeaderName, new List<ICharSequence> { (StringCharSequence)" c, d \t" });

            var list = new List<ICharSequence>(headers.ValueCharSequenceIterator(new AsciiString("foo")));
            Assert.Empty(list);
            AssertValueIterator(headers.ValueCharSequenceIterator(HeaderName));
        }

        static void AssertValueIterator(IEnumerable<ICharSequence> values)
        {
            var expected = new[] { "a", "", "b", "", "c, d" };
            int index = 0;
            foreach (ICharSequence value in values)
            {
                Assert.True(index < expected.Length, "Wrong number of values");
                Assert.Equal(expected[index], value.ToString());
                index++;
            }
            Assert.Equal(expected.Length, index);
        }
    }
}
