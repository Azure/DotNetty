// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Runtime.CompilerServices;

    public static class CharUtil
    {
        public static readonly string Digits = "0123456789ABCDEF";

        public static readonly int MinRadix = 2;
        public static readonly int MaxRadix = 36;

        const string DigitKeys = "0Aa\u0660\u06f0\u0966\u09e6\u0a66\u0ae6\u0b66\u0be7\u0c66\u0ce6\u0d66\u0e50\u0ed0\u0f20\u1040\u1369\u17e0\u1810\uff10\uff21\uff41";
        static readonly char[] DigitValues = "90Z7zW\u0669\u0660\u06f9\u06f0\u096f\u0966\u09ef\u09e6\u0a6f\u0a66\u0aef\u0ae6\u0b6f\u0b66\u0bef\u0be6\u0c6f\u0c66\u0cef\u0ce6\u0d6f\u0d66\u0e59\u0e50\u0ed9\u0ed0\u0f29\u0f20\u1049\u1040\u1371\u1368\u17e9\u17e0\u1819\u1810\uff19\uff10\uff3a\uff17\uff5a\uff37".ToCharArray();

        public static int BinarySearchRange(string data, char c)
        {
            char value = '\u0000';
            int low = 0, mid = -1, high = data.Length - 1;
            while (low <= high)
            {
                mid = (low + high) >> 1;
                value = data[mid];
                if (c > value)
                    low = mid + 1;
                else if (c == value)
                    return mid;
                else
                    high = mid - 1;
            }

            return mid - (c < value ? 1 : 0);
        }

        public static int ParseInt(ICharSequence seq, int start, int end, int radix)
        {
            Contract.Requires(seq != null);
            Contract.Requires(radix >= MinRadix && radix <= MaxRadix);

            if (start == end)
            {
                throw new FormatException();
            }

            int i = start;
            bool negative = seq[i] == '-';
            if (negative && ++i == end)
            {
                throw new FormatException(seq.SubSequence(start, end).ToString());
            }

            return ParseInt(seq, i, end, radix, negative);
        }

        public static int ParseInt(ICharSequence seq) => ParseInt(seq, 0, seq.Count, 10, false);

        public static int ParseInt(ICharSequence seq, int start, int end, int radix, bool negative)
        {
            Contract.Requires(seq != null);
            Contract.Requires(radix >= MinRadix && radix <= MaxRadix);

            int max = int.MinValue / radix;
            int result = 0;
            int currOffset = start;
            while (currOffset < end)
            {
                int digit = Digit((char)(seq[currOffset++] & 0xFF), radix);
                if (digit == -1)
                {
                    throw new FormatException(seq.SubSequence(start, end).ToString());
                }
                if (max > result)
                {
                    throw new FormatException(seq.SubSequence(start, end).ToString());
                }
                int next = result * radix - digit;
                if (next > result)
                {
                    throw new FormatException(seq.SubSequence(start, end).ToString());
                }
                result = next;
            }

            if (!negative)
            {
                result = -result;
                if (result < 0)
                {
                    throw new FormatException(seq.SubSequence(start, end).ToString());
                }
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ParseLong(ICharSequence str, int radix = 10)
        {
            if (str is AsciiString asciiString)
            {
                return asciiString.ParseLong(radix);
            }

            if (str == null
                || radix < MinRadix
                || radix > MaxRadix)
            {
                ThrowFormatException(str);
            }

            // ReSharper disable once PossibleNullReferenceException
            int length = str.Count;
            int i = 0;
            if (length == 0)
            {
                ThrowFormatException(str);
            }
            bool negative = str[i] == '-';
            if (negative && ++i == length)
            {
                ThrowFormatException(str);
            }

            return ParseLong(str, i, radix, negative);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static long ParseLong(ICharSequence str, int offset, int radix, bool negative)
        {
            long max = long.MinValue / radix;
            long result = 0, length = str.Count;
            while (offset < length)
            {
                int digit = Digit(str[offset++], radix);
                if (digit == -1)
                {
                    ThrowFormatException(str);
                }
                if (max > result)
                {
                    ThrowFormatException(str);
                }
                long next = result * radix - digit;
                if (next > result)
                {
                    ThrowFormatException(str);
                }
                result = next;
            }

            if (!negative)
            {
                result = -result;
                if (result < 0)
                {
                    ThrowFormatException(str);
                }
            }

            return result;
        }

        static void ThrowFormatException(ICharSequence str)  => throw new FormatException(str.ToString());

        public static bool IsNullOrEmpty(ICharSequence sequence) => sequence == null || sequence.Count == 0;

        public static ICharSequence[] Split(ICharSequence sequence, params char[] delimiters) => Split(sequence, 0, delimiters);

        public static ICharSequence[] Split(ICharSequence sequence, int startIndex, params char[] delimiters)
        {
            Contract.Requires(sequence != null);
            Contract.Requires(delimiters != null);
            Contract.Requires(startIndex >= 0 && startIndex < sequence.Count);

            List<ICharSequence> result = InternalThreadLocalMap.Get().CharSequenceList();

            int i = startIndex;
            int length = sequence.Count;

            while (i < length)
            {
                while (i < length && IndexOf(delimiters, sequence[i]) >= 0)
                {
                    i++;
                }

                int position = i;
                if (i < length)
                {
                    if (IndexOf(delimiters, sequence[position]) >= 0)
                    {
                        result.Add(sequence.SubSequence(position++, i + 1));
                    }
                    else
                    {
                        ICharSequence seq = null;
                        for (position++; position < length; position++)
                        {
                            if (IndexOf(delimiters, sequence[position]) >= 0)
                            {
                                seq = sequence.SubSequence(i, position);
                                break;
                            }
                        }
                        result.Add(seq ?? sequence.SubSequence(i));
                    }
                    i = position;
                }
            }

            return result.Count == 0 ? new[] { sequence } : result.ToArray();
        }

        internal static bool ContentEquals(ICharSequence left, ICharSequence right)
        {
            if (left == null || right == null)
            {
                return ReferenceEquals(left, right);
            }

            if (ReferenceEquals(left, right))
            {
                return true;
            }
            if (left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                char c1 = left[i];
                char c2 = right[i];
                if (c1 != c2
                    && char.ToUpper(c1).CompareTo(char.ToUpper(c2)) != 0
                    && char.ToLower(c1).CompareTo(char.ToLower(c2)) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool ContentEqualsIgnoreCase(ICharSequence left, ICharSequence right)
        {
            if (left == null || right == null)
            {
                return ReferenceEquals(left, right);
            }

            if (ReferenceEquals(left, right))
            {
                return true;
            }
            if (left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                char c1 = left[i];
                char c2 = right[i];
                if (char.ToLower(c1).CompareTo(char.ToLower(c2)) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool RegionMatches(string value, int thisStart, ICharSequence other, int start, int length)
        {
            Contract.Requires(value != null && other != null);

            if (start < 0
                || other.Count - start < length)
            {
                return false;
            }

            if (thisStart < 0
                || value.Length - thisStart < length)
            {
                return false;
            }

            if (length <= 0)
            {
                return true;
            }

            int o1 = thisStart;
            int o2 = start;
            for (int i = 0; i < length; ++i)
            {
                if (value[o1 + i] != other[o2 + i])
                {
                    return false;
                }
            }

            return true;
        }

        public static bool RegionMatchesIgnoreCase(string value, int thisStart, ICharSequence other, int start, int length)
        {
            Contract.Requires(value != null && other != null);

            if (thisStart < 0
                || length > value.Length - thisStart)
            {
                return false;
            }

            if (start < 0 || length > other.Count - start)
            {
                return false;
            }

            int end = thisStart + length;
            while (thisStart < end)
            {
                char c1 = value[thisStart++];
                char c2 = other[start++];
                if (c1 != c2
                    && char.ToUpper(c1).CompareTo(char.ToUpper(c2)) != 0
                    && char.ToLower(c1).CompareTo(char.ToLower(c2)) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool RegionMatches(IReadOnlyList<char> value, int thisStart, ICharSequence other, int start, int length)
        {
            Contract.Requires(value != null && other != null);

            if (start < 0 || other.Count - start < length)
            {
                return false;
            }

            if (thisStart < 0 || value.Count - thisStart < length)
            {
                return false;
            }

            if (length <= 0)
            {
                return true;
            }

            int o1 = thisStart;
            int o2 = start;
            for (int i = 0; i < length; ++i)
            {
                if (value[o1 + i] != other[o2 + i])
                {
                    return false;
                }
            }

            return true;
        }

        public static bool RegionMatchesIgnoreCase(IReadOnlyList<char> value, int thisStart, ICharSequence other, int start, int length)
        {
            Contract.Requires(value != null && other != null);

            if (thisStart < 0 || length > value.Count - thisStart)
            {
                return false;
            }

            if (start < 0 || length > other.Count - start)
            {
                return false;
            }

            int end = thisStart + length;
            while (thisStart < end)
            {
                char c1 = value[thisStart++];
                char c2 = other[start++];
                if (c1 != c2
                    && char.ToUpper(c1).CompareTo(char.ToUpper(c2)) != 0
                    && char.ToLower(c1).CompareTo(char.ToLower(c2)) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        public static ICharSequence SubstringAfter(this ICharSequence value, char delim)
        {
            int pos = value.IndexOf(delim);
            return pos >= 0 ? value.SubSequence(pos + 1, value.Count) : null;
        }

        public static ICharSequence Trim(ICharSequence sequence)
        {
            Contract.Requires(sequence != null);

            int length = sequence.Count;
            int start = IndexOfFirstNonWhiteSpace(sequence);
            if (start == length)
            {
                return StringCharSequence.Empty;
            }

            int last = IndexOfLastNonWhiteSpaceChar(sequence, start);

            length = last - start + 1;
            return length == sequence.Count 
                ? sequence 
                : sequence.SubSequence(start, last + 1);
        }

        static int IndexOfFirstNonWhiteSpace(IReadOnlyList<char> value)
        {
            Contract.Requires(value != null);

            int i = 0;
            while (i < value.Count && char.IsWhiteSpace(value[i]))
            {
                i++;
            }

            return i;
        }

        static int IndexOfLastNonWhiteSpaceChar(IReadOnlyList<char> value, int start)
        {
            int i = value.Count - 1;
            while (i > start && char.IsWhiteSpace(value[i]))
            {
                i--;
            }

            return i;
        }

        public static bool Contains(IReadOnlyList<char> value, char c)
        {
            if (value != null)
            {
                int length = value.Count;
                for (int i = 0; i < length; i++)
                {
                    if (value[i] == c)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Digit(byte b)
        {
            const byte First = (byte)'0';
            const byte Last = (byte)'9';

            if (b < First || b > Last)
            {
                return -1;
            }

            return b - First;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Digit(char c, int radix)
        {
            if (radix >= MinRadix && radix <= MaxRadix)
            {
                if (c < 128)
                {
                    int result = -1;
                    if ('0' <= c && c <= '9')
                    {
                        result = c - '0';
                    }
                    else if ('a' <= c && c <= 'z')
                    {
                        result = c - ('a' - 10);
                    }
                    else if ('A' <= c && c <= 'Z')
                    {
                        result = c - ('A' - 10);
                    }

                    return result < radix ? result : -1;
                }

                int result1 = BinarySearchRange(DigitKeys, c);
                if (result1 >= 0 && c <= DigitValues[result1 * 2])
                {
                    int value = (char)(c - DigitValues[result1 * 2 + 1]);
                    if (value >= radix)
                    {
                        return -1;
                    }
                    return value;
                }
            }

            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsISOControl(int c) => (c >= 0 && c <= 0x1f) || (c >= 0x7f && c <= 0x9f);

        public static int IndexOf(this ICharSequence cs, char searchChar, int start)
        {
            if (cs == null)
            {
                return AsciiString.IndexNotFound;
            }

            if (cs is StringCharSequence sequence)
            {
                return sequence.IndexOf(searchChar, start);
            }

            if (cs is AsciiString s)
            {
                return s.IndexOf(searchChar, start);
            }

            int sz = cs.Count;
            if (start < 0)
            {
                start = 0;
            }
            for (int i = start; i < sz; i++)
            {
                if (cs[i] == searchChar)
                {
                    return i;
                }
            }

            return -1;
        }

        static int IndexOf(char[] tokens, char value)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i] == value)
                {
                    return i;
                }
            }

            return -1;
        }

        public static int CodePointAt(IReadOnlyList<char> seq, int index)
        {
            Contract.Requires(seq != null);
            Contract.Requires(index >= 0 && index < seq.Count);

            char high = seq[index++];
            if (index >=  seq.Count)
            {
                return high;
            }

            char low = seq[index];

            return IsSurrogatePair(high, low) ? ToCodePoint(high, low) : high;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToCodePoint(char high, char low)
        {
            // See RFC 2781, Section 2.2
            // http://www.faqs.org/rfcs/rfc2781.html
            int h = (high & 0x3FF) << 10;
            int l = low & 0x3FF;
            return (h | l) + 0x10000;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsSurrogatePair(char high, char low) => char.IsHighSurrogate(high) && char.IsLowSurrogate(low);

        internal static int IndexOf(IReadOnlyList<char> value, char ch, int start)
        {
            char upper = char.ToUpper(ch);
            char lower = char.ToLower(ch);
            int i = start;
            while (i < value.Count)
            {
                char c1 = value[i];
                if (c1 == ch
                    && char.ToUpper(c1).CompareTo(upper) != 0
                    && char.ToLower(c1).CompareTo(lower) != 0)
                {
                    return i;
                }

                i++;
            }

            return -1;
        }
    }
}
