// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Common.Utilities;
    using Xunit;

    static class HttpHeadersTestUtils
    {
        static readonly IReadOnlyDictionary<int, HeaderValue> ValueMap = new Dictionary<int, HeaderValue>
        {
            { 0, HeaderValue.Unknown },
            { 1, HeaderValue.One },
            { 2, HeaderValue.Two },
            { 3, HeaderValue.Three },
            { 4, HeaderValue.Four },
            { 5, HeaderValue.Five },
            { 6, HeaderValue.SixQuoted },
            { 7, HeaderValue.SevenQuoted },
            { 8, HeaderValue.Eight }
        };

        public class HeaderValue
        {
            public static readonly HeaderValue Unknown = new HeaderValue("Unknown", 0);
            public static readonly HeaderValue One = new HeaderValue("One", 1);
            public static readonly HeaderValue Two = new HeaderValue("Two", 2);
            public static readonly HeaderValue Three = new HeaderValue("Three", 3);
            public static readonly HeaderValue Four = new HeaderValue("Four", 4);
            public static readonly HeaderValue Five = new HeaderValue("Five", 5);
            public static readonly HeaderValue SixQuoted = new HeaderValue("Six,", 6);
            public static readonly HeaderValue SevenQuoted = new HeaderValue("Seven; , GMT", 7);
            public static readonly HeaderValue Eight = new HeaderValue("Eight", 8);

            readonly int nr;
            readonly string value;
            List<ICharSequence> array;

            HeaderValue(string value, int nr)
            {
                this.nr = nr;
                this.value = value;
            }

            public override string ToString() => this.value;

            public List<ICharSequence> Subset(int from)
            {
                Assert.True(from > 0);
                --from;
                int size = this.nr - from;
                int end = from + size;
                var list = new List<ICharSequence>(size);
                List<ICharSequence> fullList = this.AsList();
                for (int i = from; i < end; ++i)
                {
                    list.Add(fullList[i]);
                }

                return list;
            }

            public string SubsetAsCsvString(int from)
            {
                List<ICharSequence> subset = this.Subset(from);
                return this.AsCsv(subset);
            }

            public List<ICharSequence> AsList()
            {
                if (this.array == null)
                {
                    var list = new List<ICharSequence>(this.nr);
                    for (int i = 1; i <= this.nr; i++)
                    {
                        list.Add(new StringCharSequence(Of(i).ToString()));
                    }

                    this.array = list;
                }

                return this.array;
            }

            public string AsCsv(IList<ICharSequence> arr)
            {
                if (arr == null || arr.Count == 0)
                {
                    return "";
                }

                var sb = new StringBuilder(arr.Count * 10);
                int end = arr.Count - 1;
                for (int i = 0; i < end; ++i)
                {
                    Quoted(sb, arr[i]).Append(StringUtil.Comma);
                }

                Quoted(sb, arr[end]);
                return sb.ToString();
            }

            public ICharSequence AsCsv() => (StringCharSequence)this.AsCsv(this.AsList());

            public static HeaderValue Of(int nr) => ValueMap.TryGetValue(nr, out HeaderValue v) ? v : Unknown;
        }

        public static AsciiString Of(string s) => new AsciiString(s);

        static StringBuilder Quoted(StringBuilder sb, ICharSequence value)
        {
            if (Contains(value, StringUtil.Comma) && !Contains(value, StringUtil.DoubleQuote))
            {
                return sb.Append(StringUtil.DoubleQuote)
                    .Append(value)
                    .Append(StringUtil.DoubleQuote);
            }

            return sb.Append(value);
        }

        static bool Contains(IEnumerable<char> value, char c)
        {
            foreach (char t in value)
            {
                if (t == c)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
