// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Text;

    public sealed class StringBuilderCharSequence : ICharSequence, IEquatable<StringBuilderCharSequence>
    {
        readonly StringBuilder builder;
        readonly int offset;

        public StringBuilderCharSequence(int capacity = 0)
        {
            Contract.Requires(capacity >= 0);

            this.builder = new StringBuilder(capacity);
            this.offset = 0;
            this.Count = 0;
        }

        public StringBuilderCharSequence(StringBuilder builder) : this(builder, 0, builder.Length)
        {
        }

        public StringBuilderCharSequence(StringBuilder builder, int offset, int count)
        {
            Contract.Requires(builder != null);
            Contract.Requires(offset >= 0 && count >= 0);
            Contract.Requires(offset <= builder.Length - count);

            this.builder = builder;
            this.offset = offset;
            this.Count = count;
        }

        public ICharSequence SubSequence(int start) => this.SubSequence(start, this.Count);

        public ICharSequence SubSequence(int start, int end)
        {
            Contract.Requires(start >= 0 && end >= start);
            Contract.Requires(end <= this.Count);

            return end == start 
                ?  new StringBuilderCharSequence()
                :  new StringBuilderCharSequence(this.builder, this.offset + start, end - start);
        }

        public int Count { get; private set; }

        public char this[int index]
        {
            get
            {
                Contract.Requires(index >= 0 && index < this.Count);
                return this.builder[this.offset + index];
            }
        }

        public void Append(string value)
        {
            this.builder.Append(value);
            this.Count += value.Length;
        }

        public void Append(string value, int index, int count)
        {
            this.builder.Append(value, index, count);
            this.Count += count;
        }

        public void Append(ICharSequence value)
        {
            if (value == null || value.Count == 0)
            {
                return;
            }

            this.builder.Append(value);
            this.Count += value.Count;
        }

        public void Append(ICharSequence value, int index, int count)
        {
            if (value == null || count == 0)
            {
                return;
            }

            this.Append(value.SubSequence(index, index + count));
        }

        public void Append(char value)
        {
            this.builder.Append(value);
            this.Count++;
        }

        public void Insert(int start, char value)
        {
            Contract.Requires(start >= 0 && start < this.Count);

            this.builder.Insert(this.offset + start, value);
            this.Count++;
        }

        public bool RegionMatches(int thisStart, ICharSequence seq, int start, int length) =>
            CharUtil.RegionMatches(this, this.offset + thisStart, seq, start, length);

        public bool RegionMatchesIgnoreCase(int thisStart, ICharSequence seq, int start, int length) =>
            CharUtil.RegionMatchesIgnoreCase(this, this.offset + thisStart, seq, start, length);

        public int IndexOf(char ch, int start = 0) => CharUtil.IndexOf(this, ch, start);

        public string ToString(int start)
        {
            Contract.Requires(start >= 0 && start < this.Count);

            return this.builder.ToString(this.offset + start, this.Count);
        }

        public override string ToString() => this.Count == 0 ? string.Empty : this.ToString(0);

        public bool Equals(StringBuilderCharSequence other)
        {
            if (other ==  null)
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            if (this.Count != other.Count)
            {
                return false;
            }

            return this.builder.ToString(this.offset, this.Count)
                .Equals(other.builder.ToString(other.offset, this.Count));
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

            if (obj is StringBuilderCharSequence other)
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

        public bool ContentEquals(ICharSequence other) => CharUtil.ContentEquals(this, other);

        public bool ContentEqualsIgnoreCase(ICharSequence other) => CharUtil.ContentEqualsIgnoreCase(this, other);

        public IEnumerator<char> GetEnumerator() => new CharSequenceEnumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
