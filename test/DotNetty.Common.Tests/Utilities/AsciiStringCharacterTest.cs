// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Tests.Utilities
{
    using System;
    using System.Linq;
    using System.Text;
    using DotNetty.Common.Utilities;
    using Xunit;

    public sealed class AsciiStringCharacterTest
    {
        static readonly Random Rand = new Random();

        [Fact]
        public void GetBytes()
        {
            var b = new StringBuilder();
            for (int i = 0; i < 1 << 16; ++i)
            {
                b.Append("eéaà");
            }
            string bString = b.ToString();
            var encodingList = new[]
            {
                Encoding.ASCII,
                Encoding.BigEndianUnicode, 
                Encoding.UTF32, 
                Encoding.UTF7, 
                Encoding.UTF8,
                Encoding.Unicode
            };

            foreach (Encoding encoding in encodingList)
            {
                byte[] expected = encoding.GetBytes(bString);
                var value = new AsciiString(bString, encoding);
                byte[] actual = value.ToByteArray();
                Assert.True(expected.SequenceEqual(actual));
            }
        }

        [Fact]
        public void ComparisonWithString()
        {
            const string Value = "shouldn't fail";
            var ascii = new AsciiString(Value.ToCharArray());
            Assert.Equal(Value, ascii.ToString());
        }

        [Fact]
        public void SubSequence()
        {
            char[] initChars = { 't', 'h', 'i', 's', ' ', 'i', 's', ' ', 'a', ' ', 't', 'e', 's', 't' };
            byte[] init = initChars.Select(c => (byte)c).ToArray();
            var ascii = new AsciiString(init);
            const int Start = 2;
            int end = init.Length;
            AsciiString sub1 = ascii.SubSequence(Start, end, false);
            AsciiString sub2 = ascii.SubSequence(Start, end, true);
            Assert.Equal(sub1.GetHashCode(), sub2.GetHashCode());
            Assert.Equal(sub1, sub2);
            for (int i = Start; i < end; ++i)
            {
                Assert.Equal(init[i], sub1.ByteAt(i - Start));
            }
        }

        [Fact]
        public void Contains()
        {
            string[] falseLhs = { "a", "aa", "aaa" };
            string[] falseRhs = { "b", "ba", "baa" };
            foreach (string lhs in falseLhs)
            {
                foreach (string rhs in falseRhs)
                {
                    AssertContains(lhs, rhs, false, false);
                }
            }

            AssertContains("", "", true, true);
            AssertContains("AsfdsF", "", true, true);
            AssertContains("", "b", false, false);
            AssertContains("a", "a", true, true);
            AssertContains("a", "b", false, false);
            AssertContains("a", "A", false, true);
            string b = "xyz";
            string a = b;
            AssertContains(a, b, true, true);

            a = "a" + b;
            AssertContains(a, b, true, true);

            a = b + "a";
            AssertContains(a, b, true, true);

            a = "a" + b + "a";
            AssertContains(a, b, true, true);

            b = "xYz";
            a = "xyz";
            AssertContains(a, b, false, true);

            b = "xYz";
            a = "xyzxxxXyZ" + b + "aaa";
            AssertContains(a, b, true, true);

            b = "foOo";
            a = "fooofoO";
            AssertContains(a, b, false, true);

            b = "Content-Equals: 10000";
            a = "content-equals: 1000";
            AssertContains(a, b, false, false);
            a += "0";
            AssertContains(a, b, false, true);
        }

        static void AssertContains(string a, string b, bool caseSensitiveEquals, bool caseInsenstaiveEquals)
        {
            var asciiA = new AsciiString(a);
            var asciiB = new AsciiString(b);
            Assert.Equal(caseSensitiveEquals, AsciiString.Contains(asciiA, asciiB));
            Assert.Equal(caseInsenstaiveEquals, AsciiString.ContainsIgnoreCase(asciiA, asciiB));
        }

        [Fact]
        public void CaseSensitivity()
        {
            int i = 0;
            for (; i < 32; i++)
            {
                DoCaseSensitivity(i);
            }
            int min = i;
            const int Max = 4000;
            int len = Rand.Next((Max - min) + 1) + min;
            DoCaseSensitivity(len);
        }

        static void DoCaseSensitivity(int len)
        {
            // Build an upper case and lower case string
            const int UpperA = 'A';
            const int UpperZ = 'Z';
            const int UpperToLower = (int)'a' - UpperA;

            var lowerCaseBytes = new byte[len];
            var upperCaseBuilder = new StringBuilderCharSequence(len);
            for (int i = 0; i < len; ++i)
            {
                char upper = (char)(Rand.Next((UpperZ - UpperA) + 1) + UpperA);
                upperCaseBuilder.Append(upper);
                lowerCaseBytes[i] = (byte)(upper + UpperToLower);
            }
            var upperCaseString = (StringCharSequence)upperCaseBuilder.ToString();
            var lowerCaseString = (StringCharSequence)new string(lowerCaseBytes.Select(x => (char)x).ToArray());
            var lowerCaseAscii = new AsciiString(lowerCaseBytes, false);
            var upperCaseAscii = new AsciiString(upperCaseString);

            // Test upper case hash codes are equal
            int upperCaseExpected = upperCaseAscii.GetHashCode();
            Assert.Equal(upperCaseExpected, AsciiString.GetHashCode(upperCaseBuilder));
            Assert.Equal(upperCaseExpected, AsciiString.GetHashCode(upperCaseString));
            Assert.Equal(upperCaseExpected, upperCaseAscii.GetHashCode());

            // Test lower case hash codes are equal
            int lowerCaseExpected = lowerCaseAscii.GetHashCode();
            Assert.Equal(lowerCaseExpected, AsciiString.GetHashCode(lowerCaseAscii));
            Assert.Equal(lowerCaseExpected, AsciiString.GetHashCode(lowerCaseString));
            Assert.Equal(lowerCaseExpected, lowerCaseAscii.GetHashCode());

            // Test case insensitive hash codes are equal
            int expectedCaseInsensitive = lowerCaseAscii.GetHashCode();
            Assert.Equal(expectedCaseInsensitive, AsciiString.GetHashCode(upperCaseBuilder));
            Assert.Equal(expectedCaseInsensitive, AsciiString.GetHashCode(upperCaseString));
            Assert.Equal(expectedCaseInsensitive, AsciiString.GetHashCode(lowerCaseString));
            Assert.Equal(expectedCaseInsensitive, AsciiString.GetHashCode(lowerCaseAscii));
            Assert.Equal(expectedCaseInsensitive, AsciiString.GetHashCode(upperCaseAscii));
            Assert.Equal(expectedCaseInsensitive, lowerCaseAscii.GetHashCode());
            Assert.Equal(expectedCaseInsensitive, upperCaseAscii.GetHashCode());

            // Test that opposite cases are equal
            Assert.Equal(lowerCaseAscii.GetHashCode(), AsciiString.GetHashCode(upperCaseString));
            Assert.Equal(upperCaseAscii.GetHashCode(), AsciiString.GetHashCode(lowerCaseString));
        }

        [Fact]
        public void CaseInsensitiveHasherCharBuffer()
        {
            const string S1 = "TRANSFER-ENCODING";
            var array = new char[128];
            const int Offset = 100;
            for (int i = 0; i < S1.Length; ++i)
            {
                array[Offset + i] = S1[i];
            }

            var s = new AsciiString(S1);
            var b = new AsciiString(array, Offset, S1.Length);
            Assert.Equal(AsciiString.GetHashCode(s), AsciiString.GetHashCode(b));
        }

        [Fact]
        public void BooleanUtilityMethods()
        {
            Assert.True(new AsciiString(new byte[] { 1 }).ParseBoolean());
            Assert.False(AsciiString.Empty.ParseBoolean());
            Assert.False(new AsciiString(new byte[] { 0 }).ParseBoolean());
            Assert.True(new AsciiString(new byte[] { 5 }).ParseBoolean());
            Assert.True(new AsciiString(new byte[] { 2, 0 }).ParseBoolean());
        }

        [Fact]
        public void EqualsIgnoreCase()
        {
            Assert.True(AsciiString.ContentEqualsIgnoreCase(null, null));
            Assert.False(AsciiString.ContentEqualsIgnoreCase(null, (StringCharSequence)"foo"));
            Assert.False(AsciiString.ContentEqualsIgnoreCase((StringCharSequence)"bar", null));
            Assert.True(AsciiString.ContentEqualsIgnoreCase((StringCharSequence)"FoO", (StringCharSequence)"fOo"));

            // Test variations (Ascii + String, Ascii + Ascii, String + Ascii)
            Assert.True(AsciiString.ContentEqualsIgnoreCase((AsciiString)"FoO", (StringCharSequence)"fOo"));
            Assert.True(AsciiString.ContentEqualsIgnoreCase((AsciiString)"FoO", (AsciiString)"fOo"));
            Assert.True(AsciiString.ContentEqualsIgnoreCase((StringCharSequence)"FoO", (AsciiString)"fOo"));

            // Test variations (Ascii + String, Ascii + Ascii, String + Ascii)
            Assert.False(AsciiString.ContentEqualsIgnoreCase((AsciiString)"FoO", (StringCharSequence)"bAr"));
            Assert.False(AsciiString.ContentEqualsIgnoreCase((AsciiString)"FoO", (AsciiString)"bAr"));
            Assert.False(AsciiString.ContentEqualsIgnoreCase((StringCharSequence)"FoO", (AsciiString)"bAr"));
        }

        [Fact]
        public void IndexOfIgnoreCase()
        {
            Assert.Equal(-1, AsciiString.IndexOfIgnoreCase(null, (AsciiString)"abc", 1));
            Assert.Equal(-1, AsciiString.IndexOfIgnoreCase((AsciiString)"abc", null, 1));
            Assert.Equal(0, AsciiString.IndexOfIgnoreCase((AsciiString)"", (StringCharSequence)"", 0));
            Assert.Equal(0, AsciiString.IndexOfIgnoreCase((StringCharSequence)"aabaabaa", (AsciiString)"A", 0));
            Assert.Equal(2, AsciiString.IndexOfIgnoreCase((AsciiString)"aabaabaa", (StringCharSequence)"B", 0));
            Assert.Equal(1, AsciiString.IndexOfIgnoreCase((StringCharSequence)"aabaabaa", (AsciiString)"AB", 0));
            Assert.Equal(5, AsciiString.IndexOfIgnoreCase((AsciiString)"aabaabaa", (StringCharSequence)"B", 3));
            Assert.Equal(-1, AsciiString.IndexOfIgnoreCase((StringCharSequence)"aabaabaa", (AsciiString)"B", 9));
            Assert.Equal(2, AsciiString.IndexOfIgnoreCase((AsciiString)"aabaabaa", (StringCharSequence)"B", -1));
            Assert.Equal(2, AsciiString.IndexOfIgnoreCase((StringCharSequence)"aabaabaa", (StringCharSequence)"", 2));
            Assert.Equal(-1, AsciiString.IndexOfIgnoreCase((AsciiString)"abc", (AsciiString)"", 9));
            Assert.Equal(0, AsciiString.IndexOfIgnoreCase((StringCharSequence)"ãabaabaa", (AsciiString)"Ã", 0));
        }

        [Fact]
        public void IndexOfIgnoreCaseAscii()
        {
            Assert.Equal(-1, AsciiString.IndexOfIgnoreCaseAscii(null, (AsciiString)"abc", 1));
            Assert.Equal(-1, AsciiString.IndexOfIgnoreCaseAscii((AsciiString)"abc", null, 1));
            Assert.Equal(0, AsciiString.IndexOfIgnoreCaseAscii((AsciiString)"", (StringCharSequence)"", 0));
            Assert.Equal(0, AsciiString.IndexOfIgnoreCaseAscii((StringCharSequence)"aabaabaa", (AsciiString)"A", 0));
            Assert.Equal(2, AsciiString.IndexOfIgnoreCaseAscii((AsciiString)"aabaabaa", (StringCharSequence)"B", 0));
            Assert.Equal(1, AsciiString.IndexOfIgnoreCaseAscii((StringCharSequence)"aabaabaa", (StringCharSequence)"AB", 0));
            Assert.Equal(5, AsciiString.IndexOfIgnoreCaseAscii((StringCharSequence)"aabaabaa", (AsciiString)"B", 3));
            Assert.Equal(-1, AsciiString.IndexOfIgnoreCaseAscii((AsciiString)"aabaabaa", (StringCharSequence)"B", 9));
            Assert.Equal(2, AsciiString.IndexOfIgnoreCaseAscii((AsciiString)"aabaabaa", new AsciiString("B"), -1));
            Assert.Equal(2, AsciiString.IndexOfIgnoreCaseAscii((StringCharSequence)"aabaabaa", (AsciiString)"", 2));
            Assert.Equal(-1, AsciiString.IndexOfIgnoreCaseAscii((AsciiString)"abc", (StringCharSequence)"", 9));
        }

        [Fact]
        public void Trim()
        {
            Assert.Equal("", AsciiString.Empty.Trim().ToString());
            Assert.Equal("abc", new AsciiString("  abc").Trim().ToString());
            Assert.Equal("abc", new AsciiString("abc  ").Trim().ToString());
            Assert.Equal("abc", new AsciiString("  abc  ").Trim().ToString());
        }

        [Fact]
        public void IndexOfChar()
        {
            Assert.Equal(-1, CharUtil.IndexOf(null, 'a', 0));
            Assert.Equal(-1, ((AsciiString)"").IndexOf('a', 0));
            Assert.Equal(-1, ((AsciiString)"abc").IndexOf('d', 0));
            Assert.Equal(-1, ((AsciiString)"aabaabaa").IndexOf('A', 0));
            Assert.Equal(0, ((AsciiString)"aabaabaa").IndexOf('a', 0));
            Assert.Equal(1, ((AsciiString)"aabaabaa").IndexOf('a', 1));
            Assert.Equal(3, ((AsciiString)"aabaabaa").IndexOf('a', 2));
            Assert.Equal(3, ((AsciiString)"aabdabaa").IndexOf('d', 1));
        }

        [Fact]
        public void StaticIndexOfChar()
        {
            Assert.Equal(-1, CharUtil.IndexOf(null, 'a', 0));
            Assert.Equal(-1, CharUtil.IndexOf((AsciiString)"", 'a', 0));
            Assert.Equal(-1, CharUtil.IndexOf((AsciiString)"abc", 'd', 0));
            Assert.Equal(-1, CharUtil.IndexOf((AsciiString)"aabaabaa", 'A', 0));
            Assert.Equal(0, CharUtil.IndexOf((AsciiString)"aabaabaa", 'a', 0));
            Assert.Equal(1, CharUtil.IndexOf((AsciiString)"aabaabaa", 'a', 1));
            Assert.Equal(3, CharUtil.IndexOf((AsciiString)"aabaabaa", 'a', 2));
            Assert.Equal(3, CharUtil.IndexOf((AsciiString)"aabdabaa", 'd', 1));
        }

        [Fact]
        public void SubStringHashCode()
        {
            var value1 = new AsciiString("123");
            var value2 = new AsciiString("a123".Substring(1));

            //two "123"s
            Assert.Equal(AsciiString.GetHashCode(value1), AsciiString.GetHashCode(value2));
        }
    }
}
