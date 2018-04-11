// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace DotNetty.Common.Internal
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Runtime.CompilerServices;
    using System.Text;
    using DotNetty.Common.Utilities;

    //
    // This is used exclusively for http headers as a buffer
    //
    // In original Netty, this is backed by char array because http parsing
    // converts each byte to char, then chars to string which implements ICharSequence 
    // in java and can be used in the same way as AsciiString.
    //
    // This approach performs poorly on .net because DotNetty only uses AsciiString 
    // for headers. DotNetty converts each byte to char, then chars back to bytes 
    // again when reading out to AsciiString. 
    //
    // Each byte to char and each char to byte forwards and backwards!
    //
    // Therefore this is backed by bytes directly in DotNetty to avoid double conversions, 
    // and all chars are assumed to be ASCII!
    // 
    public sealed class AppendableCharSequence : ICharSequence, IAppendable, IEquatable<AppendableCharSequence>
    {
        byte[] chars;
        int pos;

        public AppendableCharSequence(int length)
        {
            Contract.Requires(length > 0);

            this.chars = new byte[length];
        }

        public AppendableCharSequence(byte[] chars)
        {
            Contract.Requires(chars.Length > 0);

            this.chars = chars;
            this.pos = chars.Length;
        }

        public IEnumerator<char> GetEnumerator() => new CharSequenceEnumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public int Count => this.pos;

        public char this[int index]
        {
            get
            {
                Contract.Requires(index <= this.pos);
                return AsciiString.ByteToChar(this.chars[index]);
            }
        }

        public ref byte[] Bytes => ref this.chars;

        public ICharSequence SubSequence(int start) => this.SubSequence(start, this.pos);

        public ICharSequence SubSequence(int start, int end)
        {
            int length = end - start;
            var data = new byte[length];
            PlatformDependent.CopyMemory(this.chars, start, data, 0, length);
            return new AppendableCharSequence(data);
        }

        public int IndexOf(char ch, int start = 0) => CharUtil.IndexOf(this, ch, start);

        public bool RegionMatches(int thisStart, ICharSequence seq, int start, int length) =>
            CharUtil.RegionMatches(this, thisStart, seq, start, length);

        public bool RegionMatchesIgnoreCase(int thisStart, ICharSequence seq, int start, int length) =>
            CharUtil.RegionMatchesIgnoreCase(this, thisStart, seq, start, length);

        public bool ContentEquals(ICharSequence other) => CharUtil.ContentEquals(this, other);

        public bool ContentEqualsIgnoreCase(ICharSequence other) => CharUtil.ContentEqualsIgnoreCase(this, other);

        public bool Equals(AppendableCharSequence other)
        {
            if (other == null)
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.pos == other.pos 
                && PlatformDependent.ByteArrayEquals(this.chars, 0, other.chars, 0, this.pos);
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

            if (obj is AppendableCharSequence other)
            {
                return this.Equals(other);
            }
            if (obj is ICharSequence seq)
            {
                return this.ContentEquals(seq);
            }

            return false;
        }

        public int HashCode(bool ignoreCase) => ignoreCase
            ? StringComparer.OrdinalIgnoreCase.GetHashCode(this.ToString())
            : StringComparer.Ordinal.GetHashCode(this.ToString());

        public override int GetHashCode() => this.HashCode(true);

        public IAppendable Append(char c) => this.Append((byte)c);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IAppendable Append(byte c)
        {
            if (this.pos == this.chars.Length)
            {
                byte[] old = this.chars;
                this.chars = new byte[old.Length << 1];
                PlatformDependent.CopyMemory(old, 0, this.chars, 0, old.Length);
            }
            this.chars[this.pos++] = c;
            return this;
        }

        public IAppendable Append(ICharSequence sequence) => this.Append(sequence, 0, sequence.Count);

        public IAppendable Append(ICharSequence sequence, int start, int end)
        {
            Contract.Requires(sequence.Count >= end);

            int length = end - start;
            if (length > this.chars.Length - this.pos)
            {
                this.chars = Expand(this.chars, this.pos + length, this.pos);
            }

            if (sequence is AppendableCharSequence seq)
            {
                // Optimize append operations via array copy
                byte[] src = seq.chars;
                PlatformDependent.CopyMemory(src, start, this.chars, this.pos, length);
                this.pos += length;

                return this;
            }

            for (int i = start; i < end; i++)
            {
                this.chars[this.pos++] = (byte)sequence[i];
            }

            return this;
        }

        // Reset the {@link AppendableCharSequence}. Be aware this will only reset the current internal position and not
        // shrink the internal char array.
        public void Reset() => this.pos = 0;

        public string ToString(int start)
        {
            Contract.Requires(start >= 0 && start < this.pos);
            return Encoding.ASCII.GetString(this.chars, start, this.pos);
        }

        public override string ToString() => this.pos == 0 ? string.Empty : this.ToString(0);

        public AsciiString ToAsciiString() => this.pos == 0 ? AsciiString.Empty : new AsciiString(this.chars, 0, this.pos, true);

        // Create a new ascii string, this method assumes all chars has been sanitized
        // to ascii chars when appending to the array
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe AsciiString SubStringUnsafe(int start, int end)
        {
            var bytes = new byte[end - start];
            fixed (byte* src = &this.chars[start])
            fixed (byte* dst = bytes)
            {
                PlatformDependent.CopyMemory(src, dst, bytes.Length);
            }
            return new AsciiString(bytes);
        }

        static byte[] Expand(byte[] array, int neededSpace, int size)
        {
            int newCapacity = array.Length;
            do
            {
                // double capacity until it is big enough
                newCapacity <<= 1;

                if (newCapacity < 0)
                {
                    throw new InvalidOperationException($"New capacity {newCapacity} must be positive");
                }
            }
            while (neededSpace > newCapacity);

            var newArray = new byte[newCapacity];
            PlatformDependent.CopyMemory(array, 0, newArray, 0, size);
            return newArray;
        }
    }
}
