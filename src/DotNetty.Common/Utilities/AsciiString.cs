// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable UseStringInterpolation
namespace DotNetty.Common.Utilities
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using DotNetty.Common.Internal;

    public sealed class AsciiString : ICharSequence, IEquatable<AsciiString>, IComparable<AsciiString>, IComparable
    {
        public static readonly AsciiString Empty = Cached(string.Empty);
        const int MaxCharValue = 255;
        const byte Replacement = (byte)'?';
        public static readonly int IndexNotFound = -1;

        public static readonly IHashingStrategy<ICharSequence> CaseInsensitiveHasher = new CaseInsensitiveHashingStrategy();
        public static readonly IHashingStrategy<ICharSequence> CaseSensitiveHasher = new CaseSensitiveHashingStrategy();

        static readonly ICharEqualityComparator DefaultCharComparator = new DefaultCharEqualityComparator();
        static readonly ICharEqualityComparator GeneralCaseInsensitiveComparator = new GeneralCaseInsensitiveCharEqualityComparator();
        static readonly ICharEqualityComparator AsciiCaseInsensitiveCharComparator = new AsciiCaseInsensitiveCharEqualityComparator();

        sealed class CaseInsensitiveHashingStrategy : IHashingStrategy<ICharSequence>
        {
            public int HashCode(ICharSequence obj) => AsciiString.GetHashCode(obj);

            int IEqualityComparer<ICharSequence>.GetHashCode(ICharSequence obj) => this.HashCode(obj);

            public bool Equals(ICharSequence a, ICharSequence b) => ContentEqualsIgnoreCase(a, b);
        }

        sealed class CaseSensitiveHashingStrategy : IHashingStrategy<ICharSequence>
        {
            public int HashCode(ICharSequence obj) => AsciiString.GetHashCode(obj);

            int IEqualityComparer<ICharSequence>.GetHashCode(ICharSequence obj) => this.HashCode(obj);

            public bool Equals(ICharSequence a, ICharSequence b) => ContentEquals(a, b);
        }

        readonly byte[] value;
        readonly int offset;
        readonly int length;

        int hash;

        //Used to cache the ToString() value.
        string stringValue;

        // Called by AppendableCharSequence for http headers
        internal AsciiString(byte[] value)
        {
            this.value = value;
            this.offset = 0;
            this.length = value.Length;
        }

        public AsciiString(byte[] value, bool copy) : this(value, 0, value.Length, copy)
        {
        }

        public AsciiString(byte[] value, int start, int length, bool copy)
        {
            if (copy)
            {
                this.value = new byte[length];
                PlatformDependent.CopyMemory(value, start, this.value, 0, length);
                this.offset = 0;
            }
            else
            {
                if (MathUtil.IsOutOfBounds(start, length, value.Length))
                {
                    ThrowIndexOutOfRangeException_Start(start, length, value.Length);
                }

                this.value = value;
                this.offset = start;
            }

            this.length = length;
        }

        public AsciiString(char[] value) : this(value, 0, value.Length)
        {
        }

        public unsafe AsciiString(char[] value, int start, int length)
        {
            if (MathUtil.IsOutOfBounds(start, length, value.Length))
            {
                ThrowIndexOutOfRangeException_Start(start, length, value.Length);
            }

            this.value = new byte[length];
            fixed (char* chars = value)
                fixed (byte* bytes = this.value)
                    GetBytes(chars + start, length, bytes);

            this.offset = 0;
            this.length = length;
        }

        public AsciiString(char[] value, Encoding encoding) : this(value, encoding, 0, value.Length)
        {
        }

        public AsciiString(char[] value, Encoding encoding, int start, int length)
        {
            this.value = encoding.GetBytes(value, start, length);
            this.offset = 0;
            this.length = this.value.Length;
        }

        public AsciiString(ICharSequence value) : this(value, 0, value.Count)
        {
        }

        public AsciiString(ICharSequence value, int start, int length)
        {
            if (MathUtil.IsOutOfBounds(start, length, value.Count))
            {
                ThrowIndexOutOfRangeException_Start(start, length, value.Count);
            }

            this.value = new byte[length];
            for (int i = 0, j = start; i < length; i++, j++)
            {
                this.value[i] = CharToByte(value[j]);
            }

            this.offset = 0;
            this.length = length;
        }

        public AsciiString(string value, Encoding encoding) : this(value, encoding, 0, value.Length)
        {
        }

        public AsciiString(string value, Encoding encoding, int start, int length)
        {
            int count = encoding.GetMaxByteCount(length);
            var bytes = new byte[count];
            count = encoding.GetBytes(value, start, length, bytes, 0);

            this.value = new byte[count];
            PlatformDependent.CopyMemory(bytes, 0, this.value, 0, count);

            this.offset = 0;
            this.length = this.value.Length;
        }

        public AsciiString(string value) : this(value, 0, value.Length)
        {
        }

        public AsciiString(string value, int start, int length)
        {
            if (MathUtil.IsOutOfBounds(start, length, value.Length))
            {
                ThrowIndexOutOfRangeException_Start(start, length, value.Length);
            }

            this.value = new byte[value.Length];
            for (int i = 0; i < value.Length; i++)
            {
                this.value[i] = CharToByte(value[i]);
            }

            this.offset = 0;
            this.length = value.Length;
        }

        public int ForEachByte(IByteProcessor visitor) => this.ForEachByte0(0, this.length, visitor);

        public int ForEachByte(int index, int count, IByteProcessor visitor)
        {
            if (MathUtil.IsOutOfBounds(index, count, this.length))
            {
                ThrowIndexOutOfRangeException_Index(index, count, this.length);
            }
            return this.ForEachByte0(index, count, visitor);
        }

        int ForEachByte0(int index, int count, IByteProcessor visitor)
        {
            int len = this.offset + index + count;
            for (int i = this.offset + index; i < len; ++i)
            {
                if (!visitor.Process(this.value[i]))
                {
                    return i - this.offset;
                }
            }

            return -1;
        }

        public int ForEachByteDesc(IByteProcessor visitor) => this.ForEachByteDesc0(0, this.length, visitor);

        public int ForEachByteDesc(int index, int count, IByteProcessor visitor)
        {
            if (MathUtil.IsOutOfBounds(index, count, this.length))
            {
                ThrowIndexOutOfRangeException_Index(index, count, this.length);
            }

            return this.ForEachByteDesc0(index, count, visitor);
        }

        int ForEachByteDesc0(int index, int count, IByteProcessor visitor)
        {
            int end = this.offset + index;
            for (int i = this.offset + index + count - 1; i >= end; --i)
            {
                if (!visitor.Process(this.value[i]))
                {
                    return i - this.offset;
                }
            }

            return -1;
        }

        public byte ByteAt(int index)
        {
            // We must do a range check here to enforce the access does not go outside our sub region of the array.
            // We rely on the array access itself to pick up the array out of bounds conditions
            if (index < 0 || index >= this.length)
            {
                ThrowIndexOutOfRangeException_Index(index, this.length);
            }

            return this.value[index + this.offset];
        }

        public bool IsEmpty => this.length == 0;

        public int Count => this.length;

        /// <summary>
        /// During normal use cases the AsciiString should be immutable, but if the
        /// underlying array is shared, and changes then this needs to be called.
        /// </summary>
        public void ArrayChanged()
        {
            this.stringValue = null;
            this.hash = 0;
        }

        public byte[] Array => this.value;

        public int Offset => this.offset;

        public bool IsEntireArrayUsed => this.offset == 0 && this.length == this.value.Length;

        public byte[] ToByteArray() => this.ToByteArray(0, this.length);

        public byte[] ToByteArray(int start, int end)
        {
            int count = end - start;
            var bytes = new byte[count];
            PlatformDependent.CopyMemory(this.value, this.offset + start, bytes, 0, count);

            return bytes;
        }

        public void Copy(int srcIdx, byte[] dst, int dstIdx, int count)
        {
            Contract.Requires(dst != null && dst.Length >= count);

            if (MathUtil.IsOutOfBounds(srcIdx, count, this.length))
            {
                ThrowIndexOutOfRangeException_SrcIndex(srcIdx, count, this.length);
            }
            if (count == 0)
            {
                return;
            }

            PlatformDependent.CopyMemory(this.value, srcIdx + this.offset, dst, dstIdx, count);
        }

        public char this[int index] => ByteToChar(this.ByteAt(index));

        public bool Contains(ICharSequence sequence) => this.IndexOf(sequence) >= 0;

        public int CompareTo(ICharSequence other)
        {
            if (ReferenceEquals(this, other))
            {
                return 0;
            }

            int length1 = this.length;
            int length2 = other.Count;
            int minLength = Math.Min(length1, length2);
            for (int i = 0, j = this.offset; i < minLength; i++, j++)
            {
                int result = ByteToChar(this.value[j]) - other[i];
                if (result != 0)
                {
                    return result;
                }
            }

            return length1 - length2;
        }

        public AsciiString Concat(ICharSequence charSequence)
        {
            int thisLen = this.length;
            int thatLen = charSequence.Count;
            if (thatLen == 0)
            {
                return this;
            }

            byte[] newValue;
            if (charSequence is AsciiString that)
            {
                if (this.IsEmpty)
                {
                    return that;
                }

                newValue = new byte[thisLen + thatLen];
                PlatformDependent.CopyMemory(this.value, this.offset, newValue, 0, thisLen);
                PlatformDependent.CopyMemory(that.value, that.offset, newValue, thisLen, thatLen);

                return new AsciiString(newValue, false);
            }

            if (this.IsEmpty)
            {
                return new AsciiString(charSequence);
            }

            newValue = new byte[thisLen + thatLen];
            PlatformDependent.CopyMemory(this.value, this.offset, newValue, 0, thisLen);
            for (int i = thisLen, j = 0; i < newValue.Length; i++, j++)
            {
                newValue[i] = CharToByte(charSequence[j]);
            }

            return new AsciiString(newValue, false);
        }

        public bool EndsWith(ICharSequence suffix)
        {
            int suffixLen = suffix.Count;
            return this.RegionMatches(this.length - suffixLen, suffix, 0, suffixLen);
        }

        public bool ContentEqualsIgnoreCase(ICharSequence other)
        {
            if (other == null || other.Count != this.length)
            {
                return false;
            }

            if (other is AsciiString rhs)
            {
                for (int i = this.offset, j = rhs.offset; i < this.length; ++i, ++j)
                {
                    if (!EqualsIgnoreCase(this.value[i], rhs.value[j]))
                    {
                        return false;
                    }
                }
                return true;
            }

            for (int i = this.offset, j = 0; i < this.length; ++i, ++j)
            {
                if (!EqualsIgnoreCase(ByteToChar(this.value[i]), other[j]))
                {
                    return false;
                }
            }

            return true;
        }

        public char[] ToCharArray() => this.ToCharArray(0, this.length);

        public char[] ToCharArray(int start, int end)
        {
            int count = end - start;
            if (count == 0)
            {
                return  EmptyArrays.EmptyChars;
            }

            if (MathUtil.IsOutOfBounds(start, count, this.length))
            {
                ThrowIndexOutOfRangeException_SrcIndex(start, count, this.length);
            }

            var buffer = new char[count];
            for (int i = 0, j = start + this.offset; i < count; i++, j++)
            {
                buffer[i] = ByteToChar(this.value[j]);
            }

            return buffer;
        }

        public void Copy(int srcIdx, char[] dst, int dstIdx, int count)
        {
            Contract.Requires(dst != null);

            if (MathUtil.IsOutOfBounds(srcIdx, count, this.length))
            {
                ThrowIndexOutOfRangeException_SrcIndex(srcIdx, count, this.length);
            }

            int dstEnd = dstIdx + count;
            for (int i = dstIdx, j = srcIdx + this.offset; i < dstEnd; i++, j++)
            {
                dst[i] = ByteToChar(this.value[j]);
            }
        }

        public ICharSequence SubSequence(int start) => (AsciiString)this.SubSequence(start, this.length);

        public ICharSequence SubSequence(int start, int end) => this.SubSequence(start, end, true);

        public AsciiString SubSequence(int start, int end, bool copy)
        {
            if (MathUtil.IsOutOfBounds(start, end - start, this.length))
            {
                ThrowIndexOutOfRangeException_StartEnd(start, end, this.length);
            }

            if (start == 0 && end == this.length)
            {
                return this;
            }

            return end == start ? Empty : new AsciiString(this.value, start + this.offset, end - start, copy);
        }

        public int IndexOf(ICharSequence sequence) => this.IndexOf(sequence, 0);

        public int IndexOf(ICharSequence subString, int start)
        {
            if (start < 0)
            {
                start = 0;
            }

            int thisLen = this.length;

            int subCount = subString.Count;
            if (subCount <= 0)
            {
                return start < thisLen ? start : thisLen;
            }
            if (subCount > thisLen - start)
            {
                return -1;
            }

            char firstChar = subString[0];
            if (firstChar > MaxCharValue)
            {
                return -1;
            }

            var indexOfVisitor = new IndexOfProcessor((byte)firstChar);
            for (; ; )
            {
                int i = this.ForEachByte(start, thisLen - start, indexOfVisitor);
                if (i == -1 || subCount + i > thisLen)
                {
                    return -1; // handles subCount > count || start >= count
                }
                int o1 = i, o2 = 0;
                while (++o2 < subCount && ByteToChar(this.value[++o1 + this.offset]) == subString[o2])
                {
                    // Intentionally empty
                }
                if (o2 == subCount)
                {
                    return i;
                }
                start = i + 1;
            }
        }

        public int IndexOf(char ch, int start)
        {
            if (start < 0)
            {
                start = 0;
            }

            int thisLen = this.length;
            if (ch > MaxCharValue)
            {
                return -1;
            }

            return this.ForEachByte(start, thisLen - start, new IndexOfProcessor((byte)ch));
        }

        // Use count instead of count - 1 so lastIndexOf("") answers count
        public int LastIndexOf(ICharSequence charSequence) => this.LastIndexOf(charSequence, this.length);

        public int LastIndexOf(ICharSequence subString, int start)
        {
            int thisLen = this.length;
            int subCount = subString.Count;

            if (subCount > thisLen || start < 0)
            {
                return -1;
            }

            if (subCount <= 0)
            {
                return start < thisLen ? start : thisLen;
            }

            start = Math.Min(start, thisLen - subCount);

            // count and subCount are both >= 1
            char firstChar = subString[0];
            if (firstChar > MaxCharValue)
            {
                return -1;
            }
            var indexOfVisitor = new IndexOfProcessor((byte)firstChar);
            for (; ;)
            {
                int i = this.ForEachByteDesc(start, thisLen - start, indexOfVisitor);
                if (i == -1)
                {
                    return -1;
                }
                int o1 = i, o2 = 0;
                while (++o2 < subCount && ByteToChar(this.value[++o1 + this.offset]) == subString[o2])
                {
                    // Intentionally empty
                }
                if (o2 == subCount)
                {
                    return i;
                }
                start = i - 1;
            }
        }

        public bool RegionMatches(int thisStart, ICharSequence seq, int start, int count)
        {
            Contract.Requires(seq != null);

            if (start < 0 || seq.Count - start < count)
            {
                return false;
            }

            int thisLen = this.length;
            if (thisStart < 0 || thisLen - thisStart < count)
            {
                return false;
            }

            if (count <= 0)
            {
                return true;
            }

            int thatEnd = start + count;
            for (int i = start, j = thisStart + this.offset; i < thatEnd; i++, j++)
            {
                if (ByteToChar(this.value[j]) != seq[i])
                {
                    return false;
                }
            }

            return true;
        }

        public bool RegionMatchesIgnoreCase(int thisStart, ICharSequence seq, int start, int count)
        {
            Contract.Requires(seq != null);

            int thisLen = this.length;
            if (thisStart < 0 || count > thisLen - thisStart)
            {
                return false;
            }
            if (start < 0 || count > seq.Count - start)
            {
                return false;
            }

            thisStart += this.offset;
            int thisEnd = thisStart + count;
            while (thisStart < thisEnd)
            {
                if (!EqualsIgnoreCase(ByteToChar(this.value[thisStart++]), seq[start++]))
                {
                    return false;
                }
            }

            return true;
        }

        public AsciiString Replace(char oldChar, char newChar)
        {
            if (oldChar > MaxCharValue)
            {
                return this;
            }

            byte oldCharByte = CharToByte(oldChar);
            int index = this.ForEachByte(new IndexOfProcessor(oldCharByte));
            if (index == -1)
            {
                return this;
            }

            byte newCharByte = CharToByte(newChar);
            var buffer = new byte[this.length];
            for (int i = 0, j = this.offset; i < buffer.Length; i++, j++)
            {
                byte b = this.value[j];
                if (b == oldCharByte)
                {
                    b = newCharByte;
                }
                buffer[i] = b;
            }

            return new AsciiString(buffer, false);
        }

        public bool StartsWith(ICharSequence prefix) => this.StartsWith(prefix, 0);

        public bool StartsWith(ICharSequence prefix, int start) => this.RegionMatches(start, prefix, 0, prefix.Count);

        public AsciiString ToLowerCase()
        {
            bool lowercased = true;
            int i, j;
            int len = this.length + this.offset;
            for (i = this.offset; i < len; ++i)
            {
                byte b = this.value[i];
                if (b >= 'A' && b <= 'Z')
                {
                    lowercased = false;
                    break;
                }
            }

            // Check if this string does not contain any uppercase characters.
            if (lowercased)
            {
                return this;
            }

            var newValue = new byte[this.length];
            for (i = 0, j = this.offset; i < newValue.Length; ++i, ++j)
            {
                newValue[i] = ToLowerCase(this.value[j]);
            }

            return new AsciiString(newValue, false);
        }

        public AsciiString ToUpperCase()
        {
            bool uppercased = true;
            int i, j;
            int len = this.length + this.offset;
            for (i = this.offset; i < len; ++i)
            {
                byte b = this.value[i];
                if (b >= 'a' && b <= 'z')
                {
                    uppercased = false;
                    break;
                }
            }

            // Check if this string does not contain any lowercase characters.
            if (uppercased)
            {
                return this;
            }

            var newValue = new byte[this.length];
            for (i = 0, j = this.offset; i < newValue.Length; ++i, ++j)
            {
                newValue[i] = ToUpperCase(this.value[j]);
            }

            return new AsciiString(newValue, false);
        }

        public static ICharSequence Trim(ICharSequence c)
        {
            if (c is AsciiString asciiString)
            {
                return asciiString.Trim();
            }
            int start = 0;
            int last = c.Count - 1;
            int end = last;
            while (start <= end && c[start] <= ' ')
            {
                start++;
            }
            while (end >= start && c[end] <= ' ')
            {
                end--;
            }
            if (start == 0 && end == last)
            {
                return c;
            }
            return c.SubSequence(start, end + 1);
        }

        public AsciiString Trim()
        {
            int start = this.offset;
            int last = this.offset + this.length - 1;
            int end = last;
            while (start <= end && this.value[start] <= ' ')
            {
                start++;
            }
            while (end >= start && this.value[end] <= ' ')
            {
                end--;
            }
            if (start == 0 && end == last)
            {
                return this;
            }

            return new AsciiString(this.value, start, end - start + 1, false);
        }

        public unsafe bool ContentEquals(string a)
        {
            if (a == null)
            {
                return false;
            }
            if (this.stringValue != null)
            {
                return this.stringValue.Equals(a);
            }
            if (this.length != a.Length)
            {
                return false;
            }

            if (this.length > 0)
            {
                fixed (char* p = a)
                    fixed (byte* b = &this.value[this.offset])
                        for (int i = 0; i < this.length; ++i)
                        {
                            if (CharToByte(*(p + i)) != *(b + i) )
                            {
                                return false;
                            }
                        }
            }

            return true;
        }

        public bool ContentEquals(ICharSequence a)
        {
            if (a == null || a.Count != this.length)
            {
                return false;
            }

            if (a is AsciiString asciiString)
            {
                return this.Equals(asciiString);
            }

            for (int i = this.offset, j = 0; j < a.Count; ++i, ++j)
            {
                if (ByteToChar(this.value[i]) != a[j])
                {
                    return false;
                }
            }

            return true;
        }

        public AsciiString[] Split(char delim)
        {
            List<AsciiString> res = InternalThreadLocalMap.Get().AsciiStringList();

            int start = 0;
            int count = this.length;
            for (int i = start; i < count; i++)
            {
                if (this[i] == delim)
                {
                    if (start == i)
                    {
                        res.Add(Empty);
                    }
                    else
                    {
                        res.Add(new AsciiString(this.value, start + this.offset, i - start, false));
                    }
                    start = i + 1;
                }
            }

            if (start == 0)
            { 
                // If no delimiter was found in the value
                res.Add(this);
            }
            else
            {
                if (start != count)
                {
                    // Add the last element if it's not empty.
                    res.Add(new AsciiString(this.value, start + this.offset, count - start, false));
                }
                else
                {
                    // Truncate trailing empty elements.
                    while (res.Count > 0)
                    {
                        int i = res.Count - 1;
                        if (!res[i].IsEmpty)
                        {
                            res.RemoveAt(i);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            var strings = new AsciiString[res.Count];
            res.CopyTo(strings);
            return strings;
        }

        // ReSharper disable NonReadonlyMemberInGetHashCode
        public override int GetHashCode()
        {
            int h = this.hash;
            if (h == 0)
            {
                h = PlatformDependent.HashCodeAscii(this.value, this.offset, this.length);
                this.hash = h;
            }

            return h;
        }
        // ReSharper restore NonReadonlyMemberInGetHashCode

        public bool Equals(AsciiString other)
        {
            if (other == null)
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.length == other.length
                && this.GetHashCode() == other.GetHashCode()
                && PlatformDependent.ByteArrayEquals(this.value, this.offset, other.value, other.offset, this.length);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is AsciiString ascii)
            {
                return this.Equals(ascii);
            }
            if (obj is ICharSequence seq)
            {
                return this.ContentEquals(seq);
            }

            return false;
        }

        public override string ToString()
        {
            if (this.stringValue != null)
            {
                return this.stringValue;
            }

            this.stringValue = this.ToString(0);
            return this.stringValue;
        }

        public string ToString(int start) => this.ToString(start, this.length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe string ToString(int start, int end)
        {
            int count = end - start;
            if (MathUtil.IsOutOfBounds(start, count, this.length))
            {
                ThrowIndexOutOfRangeException_SrcIndex(start, count, this.length);
            }
            if (count == 0)
            {
                return string.Empty;
            }

            fixed (byte* p = &this.value[this.offset + start])
            {
                return Marshal.PtrToStringAnsi((IntPtr)p, count);
            }
        }

        public bool ParseBoolean() => this.length >= 1 && this.value[this.offset] != 0;

        public char ParseChar() => this.ParseChar(0);

        public char ParseChar(int start)
        {
            if (start + 1 >= this.length)
            {
                throw new IndexOutOfRangeException($"2 bytes required to convert to character. index {start} would go out of bounds.");
            }

            int startWithOffset = start + this.offset;

            return (char)((ByteToChar(this.value[startWithOffset]) << 8) 
                | ByteToChar(this.value[startWithOffset + 1]));
        }

        public short ParseShort() => this.ParseShort(0, this.length, 10);

        public short ParseShort(int radix) => this.ParseShort(0, this.length, radix);

        public short ParseShort(int start, int end) => this.ParseShort(start, end, 10);

        public short ParseShort(int start, int end, int radix)
        {
            int intValue = this.ParseInt(start, end, radix);
            short result = (short)intValue;
            if (result != intValue)
            {
                throw new FormatException(this.SubSequence(start, end).ToString());
            }

            return result;
        }

        public int ParseInt() => this.ParseInt(0, this.length, 10);

        public int ParseInt(int radix) => this.ParseInt(0, this.length, radix);

        public int ParseInt(int start, int end) => this.ParseInt(start, end, 10);

        public int ParseInt(int start, int end, int radix)
        {
            if (radix < CharUtil.MinRadix || radix > CharUtil.MaxRadix)
            {
                throw new FormatException($"Radix must be from {CharUtil.MinRadix} to {CharUtil.MaxRadix}");
            }
            if (start == end)
            {
                throw new FormatException($"Content is empty because {start} and {end} are the same.");
            }

            int i = start;
            bool negative = this.ByteAt(i) == '-';
            if (negative && ++i == end)
            {
                throw new FormatException(this.SubSequence(start, end).ToString());
            }

            return this.ParseInt(i, end, radix, negative);
        }

        int ParseInt(int start, int end, int radix, bool negative)
        {
            int max = int.MinValue / radix;
            int result = 0;
            int currOffset = start;
            while (currOffset < end)
            {
                int digit = CharUtil.Digit((char)(this.value[currOffset++ + this.offset]), radix);
                if (digit == -1)
                {
                    throw new FormatException(this.SubSequence(start, end).ToString());
                }
                if (max > result)
                {
                    throw new FormatException(this.SubSequence(start, end).ToString());
                }
                int next = result * radix - digit;
                if (next > result)
                {
                    throw new FormatException(this.SubSequence(start, end).ToString());
                }
                result = next;
            }

            if (!negative)
            {
                result = -result;
                if (result < 0)
                {
                    throw new FormatException(this.SubSequence(start, end).ToString());
                }
            }

            return result;
        }

        public long ParseLong() => this.ParseLong(0, this.length, 10);

        public long ParseLong(int radix) => this.ParseLong(0, this.length, radix);

        public long ParseLong(int start, int end) => this.ParseLong(start, end, 10);

        public long ParseLong(int start, int end, int radix)
        {
            if (radix < CharUtil.MinRadix || radix > CharUtil.MaxRadix)
            {
                throw new FormatException($"Radix must be from {CharUtil.MinRadix} to {CharUtil.MaxRadix}");
            }

            if (start == end)
            {
                throw new FormatException($"Content is empty because {start} and {end} are the same.");
            }

            int i = start;
            bool negative = this.ByteAt(i) == '-';
            if (negative && ++i == end)
            {
                throw new FormatException(this.SubSequence(start, end).ToString());
            }

            return this.ParseLong(i, end, radix, negative);
        }

        long ParseLong(int start, int end, int radix, bool negative)
        {
            long max = long.MinValue / radix;
            long result = 0;
            int currOffset = start;
            while (currOffset < end)
            {
                int digit = CharUtil.Digit((char)(this.value[currOffset++ + this.offset]), radix);
                if (digit == -1)
                {
                    throw new FormatException(this.SubSequence(start, end).ToString());
                }
                if (max > result)
                {
                    throw new FormatException(this.SubSequence(start, end).ToString());
                }
                long next = result * radix - digit;
                if (next > result)
                {
                    throw new FormatException(this.SubSequence(start, end).ToString());
                }
                result = next;
            }

            if (!negative)
            {
                result = -result;
                if (result < 0)
                {
                    throw new FormatException(this.SubSequence(start, end).ToString());
                }
            }

            return result;
        }

        public float ParseFloat() => this.ParseFloat(0, this.length);

        public float ParseFloat(int start, int end) => Convert.ToSingle(this.ToString(start, end));

        public double ParseDouble() => this.ParseDouble(0, this.length);

        public double ParseDouble(int start, int end) => Convert.ToDouble(this.ToString(start, end));

        public static AsciiString Of(string value) => new AsciiString(value);

        public static AsciiString Of(ICharSequence charSequence) => charSequence is AsciiString s ? s : new AsciiString(charSequence);

        public static AsciiString Cached(string value)
        {
            var asciiString = new AsciiString(value);
            asciiString.stringValue = value;
            return asciiString;
        }

        public static int GetHashCode(ICharSequence value)
        {
            if (value == null)
            {
                return 0;
            }
            if (value is AsciiString)
            {
                return value.GetHashCode();
            }

            return  PlatformDependent.HashCodeAscii(value);
        }

        public static bool Contains(ICharSequence a, ICharSequence b) => Contains(a, b, DefaultCharComparator);

        public static bool ContainsIgnoreCase(ICharSequence a, ICharSequence b) => Contains(a, b, AsciiCaseInsensitiveCharComparator);

        public static bool ContentEqualsIgnoreCase(ICharSequence a, ICharSequence b)
        {
            if (a == null || b == null)
            {
                return ReferenceEquals(a, b);
            }

            if (a is AsciiString stringA)
            {
                return stringA.ContentEqualsIgnoreCase(b);
            }
            if (b is AsciiString stringB)
            {
                return stringB.ContentEqualsIgnoreCase(a);
            }

            if (a.Count != b.Count)
            {
                return false;
            }
            for (int i = 0, j = 0; i < a.Count; ++i, ++j)
            {
                if (!EqualsIgnoreCase(a[i], b[j]))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool ContainsContentEqualsIgnoreCase(ICollection<ICharSequence> collection, ICharSequence value)
        {
            foreach (ICharSequence v in collection)
            {
                if (ContentEqualsIgnoreCase(value, v))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ContainsAllContentEqualsIgnoreCase(ICollection<ICharSequence> a, ICollection<AsciiString> b)
        {
            foreach (AsciiString v in b)
            {
                if (!ContainsContentEqualsIgnoreCase(a, v))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool ContentEquals(ICharSequence a, ICharSequence b)
        {
            if (a == null || b == null)
            {
                return ReferenceEquals(a, b);
            }

            if (a is AsciiString stringA)
            {
                return stringA.ContentEquals(b);
            }
            if (b is AsciiString stringB)
            {
                return stringB.ContentEquals(a);
            }

            if (a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; ++i)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        static bool Contains(ICharSequence a, ICharSequence b, ICharEqualityComparator comparator)
        {
            if (a == null || b == null || a.Count < b.Count)
            {
                return false;
            }
            if (b.Count == 0)
            {
                return true;
            }

            int bStart = 0;
            for (int i = 0; i < a.Count; ++i)
            {
                if (comparator.CharEquals(b[bStart], a[i]))
                {
                    // If b is consumed then true.
                    if (++bStart == b.Count)
                    {
                        return true;
                    }
                }
                else if (a.Count - i < b.Count)
                {
                    // If there are not enough characters left in a for b to be contained, then false.
                    return false;
                }
                else
                {
                    bStart = 0;
                }
            }

            return false;
        }

        static bool RegionMatchesCharSequences(ICharSequence cs, int csStart, 
            ICharSequence seq, int start, int length, ICharEqualityComparator charEqualityComparator)
        {
            //general purpose implementation for CharSequences
            if (csStart < 0 || length > cs.Count - csStart)
            {
                return false;
            }
            if (start < 0 || length > seq.Count - start)
            {
                return false;
            }

            int csIndex = csStart;
            int csEnd = csIndex + length;
            int stringIndex = start;

            while (csIndex < csEnd)
            {
                char c1 = cs[csIndex++];
                char c2 = seq[stringIndex++];

                if (!charEqualityComparator.CharEquals(c1, c2))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool RegionMatches(ICharSequence cs, bool ignoreCase, int csStart, ICharSequence seq, int start, int length)
        {
            if (cs == null || seq == null)
            {
                return false;
            }
            if (cs is StringCharSequence stringCharSequence && seq is StringCharSequence)
            {
                return ignoreCase 
                    ? stringCharSequence.RegionMatchesIgnoreCase(csStart, seq, start, length) 
                    : stringCharSequence.RegionMatches (csStart, seq, start, length);
            }
            if (cs is AsciiString asciiString)
            {
                return ignoreCase 
                    ? asciiString.RegionMatchesIgnoreCase(csStart, seq, start, length) 
                    : asciiString.RegionMatches(csStart, seq, start, length);
            }

            return RegionMatchesCharSequences(cs, csStart, seq, start, length,
                ignoreCase ? GeneralCaseInsensitiveComparator : DefaultCharComparator);
        }

        public static bool RegionMatchesAscii(ICharSequence cs, bool ignoreCase, int csStart, ICharSequence seq, int start, int length)
        {
            if (cs == null || seq == null)
            {
                return false;
            }

            if (!ignoreCase && cs is StringCharSequence && seq is StringCharSequence)
            {
                //we don't call regionMatches from String for ignoreCase==true. It's a general purpose method,
                //which make complex comparison in case of ignoreCase==true, which is useless for ASCII-only strings.
                //To avoid applying this complex ignore-case comparison, we will use regionMatchesCharSequences
                return cs.RegionMatches(csStart, seq, start, length);
            }

            if (cs is AsciiString asciiString)
            {
                return ignoreCase 
                    ? asciiString.RegionMatchesIgnoreCase(csStart, seq, start, length) 
                    : asciiString.RegionMatches(csStart, seq, start, length);
            }

            return RegionMatchesCharSequences(cs, csStart, seq, start, length,
                ignoreCase ? AsciiCaseInsensitiveCharComparator : DefaultCharComparator);
        }

        public static int IndexOfIgnoreCase(ICharSequence str, ICharSequence searchStr, int startPos)
        {
            if (str == null || searchStr == null)
            {
                return IndexNotFound;
            }

            if (startPos < 0)
            {
                startPos = 0;
            }
            int searchStrLen = searchStr.Count;
            int endLimit = str.Count - searchStrLen + 1;
            if (startPos > endLimit)
            {
                return IndexNotFound;
            }
            if (searchStrLen == 0)
            {
                return startPos;
            }
            for (int i = startPos; i < endLimit; i++)
            {
                if (RegionMatches(str, true, i, searchStr, 0, searchStrLen))
                {
                    return i;
                }
            }

            return IndexNotFound;
        }

        public static int IndexOfIgnoreCaseAscii(ICharSequence str, ICharSequence searchStr, int startPos)
        {
            if (str == null || searchStr == null)
            {
                return IndexNotFound;
            }

            if (startPos < 0)
            {
                startPos = 0;
            }
            int searchStrLen = searchStr.Count;
            int endLimit = str.Count - searchStrLen + 1;
            if (startPos > endLimit)
            {
                return IndexNotFound;
            }
            if (searchStrLen == 0)
            {
                return startPos;
            }
            for (int i = startPos; i < endLimit; i++)
            {
                if (RegionMatchesAscii(str, true, i, searchStr, 0, searchStrLen))
                {
                    return i;
                }
            }

            return IndexNotFound;
        }

        public static int IndexOf(ICharSequence cs, char searchChar, int start)
        {
            if (cs is StringCharSequence stringCharSequence)
            {
                return stringCharSequence.IndexOf(searchChar, start);
            }
            else if (cs is AsciiString asciiString)
            {
                return asciiString.IndexOf(searchChar, start);
            }
            if (cs == null)
            {
                return IndexNotFound;
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

            return IndexNotFound;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool EqualsIgnoreCase(byte a, byte b) => a == b || ToLowerCase(a) == ToLowerCase(b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool EqualsIgnoreCase(char a, char b) => a == b || ToLowerCase(a) == ToLowerCase(b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static byte ToLowerCase(byte b) => IsUpperCase(b) ? (byte)(b + 32) : b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static char ToLowerCase(char c) => IsUpperCase(c) ? (char)(c + 32) : c;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static byte ToUpperCase(byte b) => IsLowerCase(b) ? (byte)(b - 32) : b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsLowerCase(byte value) => value >= 'a' && value <= 'z';

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUpperCase(byte value) => value >= 'A' && value <= 'Z';

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUpperCase(char value) => value >= 'A' && value <= 'Z';

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte CharToByte(char c) => c > MaxCharValue ? Replacement : unchecked((byte)c);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char ByteToChar(byte b) => (char)(b);

        public static explicit operator string(AsciiString value) => value?.ToString() ?? string.Empty;

        public static explicit operator AsciiString(string value) => value != null ? new AsciiString(value) : Empty;

        static unsafe void GetBytes(char* chars, int length, byte* bytes)
        {
            char* charEnd = chars + length;
            while (chars < charEnd)
            {
                char ch = *(chars++);
                // ByteToChar
                if (ch > MaxCharValue)
                {
                    *(bytes++) = Replacement; 
                }
                else
                {
                    *(bytes++) = unchecked((byte)ch);
                }
            }
        }

        public int HashCode(bool ignoreCase) =>  !ignoreCase ? this.GetHashCode() : CaseInsensitiveHasher.GetHashCode(this);

        //
        // Compares the specified string to this string using the ASCII values of the characters. Returns 0 if the strings
        // contain the same characters in the same order. Returns a negative integer if the first non-equal character in
        // this string has an ASCII value which is less than the ASCII value of the character at the same position in the
        // specified string, or if this string is a prefix of the specified string. Returns a positive integer if the first
        // non-equal character in this string has a ASCII value which is greater than the ASCII value of the character at
        // the same position in the specified string, or if the specified string is a prefix of this string.
        // 
        public int CompareTo(AsciiString other)
        {
            if (ReferenceEquals(this, other))
            {
                return 0;
            }

            int length1 = this.length;
            int length2 = other.length;
            int minLength = Math.Min(length1, length2);
            for (int i = 0, j = this.offset; i < minLength; i++, j++)
            {
                int result = ByteToChar(this.value[j]) - other[i];
                if (result != 0)
                {
                    return result;
                }
            }

            return length1 - length2;
        }

        public int CompareTo(object obj) => this.CompareTo(obj as AsciiString);

        public IEnumerator<char> GetEnumerator() => new CharSequenceEnumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        static void ThrowIndexOutOfRangeException_Start(int start, int length, int count)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("expected: 0 <= start({0}) <= start + length({1}) <= value.length({2})", start, length, count));
            }
        }

        static void ThrowIndexOutOfRangeException_StartEnd(int start, int end, int length)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("expected: 0 <= start({0}) <= end ({1}) <= length({2})", start, end, length));
            }
        }

        static void ThrowIndexOutOfRangeException_SrcIndex(int start, int count, int length)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("expected: 0 <= start({0}) <= srcIdx + length({1}) <= srcLen({2})", start, count, length));
            }
        }

        static void ThrowIndexOutOfRangeException_Index(int index, int length, int count)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("expected: 0 <= index({0} <= start + length({1}) <= length({2})", index, length, count));
            }
        }

        static void ThrowIndexOutOfRangeException_Index(int index, int length)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("index: {0} must be in the range [0,{1})", index, length));
            }
        }

        interface ICharEqualityComparator
        {
            bool CharEquals(char a, char b);
        }

        sealed class DefaultCharEqualityComparator : ICharEqualityComparator
        {
            public bool CharEquals(char a, char b) => a == b;
        }

        sealed class GeneralCaseInsensitiveCharEqualityComparator : ICharEqualityComparator
        {
            public bool CharEquals(char a, char b) => 
                char.ToUpper(a) == char.ToUpper(b) || char.ToLower(a) == char.ToLower(b);
        }

        sealed class AsciiCaseInsensitiveCharEqualityComparator : ICharEqualityComparator
        {
            public bool CharEquals(char a, char b) => EqualsIgnoreCase(a, b);
        }
    }
}
