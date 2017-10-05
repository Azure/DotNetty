// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ForCanBeConvertedToForeach
namespace DotNetty.Codecs.Http
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using DotNetty.Common.Utilities;

    using static Common.Utilities.AsciiString;

    public abstract class HttpHeaders : IEnumerable<HeaderEntry<AsciiString, ICharSequence>>
    {
        public abstract bool TryGet(AsciiString name, out ICharSequence value);

        public ICharSequence Get(AsciiString name, ICharSequence defaultValue) => this.TryGet(name, out ICharSequence value) ? value : defaultValue;

        public abstract bool TryGetInt(AsciiString name, out int value);

        public abstract int GetInt(AsciiString name, int defaultValue);

        public abstract bool TryGetShort(AsciiString name, out short value);

        public abstract short GetShort(AsciiString name, short defaultValue);

        public abstract bool TryGetTimeMillis(AsciiString name, out long value);

        public abstract long GetTimeMillis(AsciiString name, long defaultValue);

        public abstract IList<ICharSequence> GetAll(AsciiString name);

        public abstract IList<HeaderEntry<AsciiString, ICharSequence>> Entries();

        public virtual IEnumerable<ICharSequence> ValueCharSequenceIterator(AsciiString name) => this.GetAll(name);

        public abstract bool Contains(AsciiString name);

        public abstract bool IsEmpty { get; }

        public abstract int Size { get; }

        public abstract ISet<AsciiString> Names();

        public abstract HttpHeaders Add(AsciiString name, object value);

        public HttpHeaders Add(AsciiString name, IEnumerable<object> values)
        {
            foreach (object value in values)
            {
                this.Add(name, value);
            }
            return this;
        }

        public virtual HttpHeaders Add(HttpHeaders headers)
        {
            Contract.Requires(headers != null);

            foreach (HeaderEntry<AsciiString, ICharSequence> pair in headers)
            {
                this.Add(pair.Key, pair.Value);
            }
            return this;
        }

        public abstract HttpHeaders AddInt(AsciiString name, int value);

        public abstract HttpHeaders AddShort(AsciiString name, short value);

        public abstract HttpHeaders Set(AsciiString name, object value);

        public abstract HttpHeaders Set(AsciiString name, IEnumerable<object> values);

        public virtual HttpHeaders Set(HttpHeaders headers)
        {
            Contract.Requires(headers != null);

            this.Clear();

            if (headers.IsEmpty)
            {
                return this;
            }

            foreach(HeaderEntry<AsciiString, ICharSequence> pair in headers)
            {
                this.Add(pair.Key, pair.Value);
            }
            return this;
        }

        public HttpHeaders SetAll(HttpHeaders headers)
        {
            Contract.Requires(headers != null);

            if (headers.IsEmpty)
            {
                return this;
            }

            foreach (HeaderEntry<AsciiString, ICharSequence> pair in headers)
            {
                this.Add(pair.Key, pair.Value);
            }

            return this;
        }

        public abstract HttpHeaders SetInt(AsciiString name, int value);

        public abstract HttpHeaders SetShort(AsciiString name, short value);

        public abstract HttpHeaders Remove(AsciiString name);

        public abstract HttpHeaders Clear();

        public virtual bool Contains(AsciiString name, ICharSequence value, bool ignoreCase)
        {
            IEnumerable<ICharSequence> values = this.ValueCharSequenceIterator(name);
            if (ignoreCase)
            {
                foreach (ICharSequence v in values)
                {
                    if (v.ContentEqualsIgnoreCase(value))
                    {
                        return true;
                    }
                }
            }
            else
            {
                foreach (ICharSequence v in this.ValueCharSequenceIterator(name))
                {
                    if (v.ContentEquals(value))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public virtual bool ContainsValue(AsciiString name, ICharSequence value, bool ignoreCase)
        {
            foreach (ICharSequence v in this.ValueCharSequenceIterator(name))
            {
                if (ContainsCommaSeparatedTrimmed(v, value, ignoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        static bool ContainsCommaSeparatedTrimmed(ICharSequence rawNext, ICharSequence expected, bool ignoreCase)
        {
            int begin = 0;
            int end;
            if (ignoreCase)
            {
                if ((end = IndexOf(rawNext, ',', begin)) == -1)
                {
                    if (ContentEqualsIgnoreCase(Trim(rawNext), expected))
                    {
                        return true;
                    }
                }
                else
                {
                    do
                    {
                        if (ContentEqualsIgnoreCase(Trim(rawNext.SubSequence(begin, end)), expected))
                        {
                            return true;
                        }
                        begin = end + 1;
                    }
                    while ((end = IndexOf(rawNext, ',', begin)) != -1);

                    if (begin < rawNext.Count)
                    {
                        if (ContentEqualsIgnoreCase(Trim(rawNext.SubSequence(begin, rawNext.Count)), expected))
                        {
                            return true;
                        }
                    }
                }
            }
            else
            {
                if ((end = IndexOf(rawNext, ',', begin)) == -1)
                {
                    if (ContentEquals(Trim(rawNext), expected))
                    {
                        return true;
                    }
                }
                else
                {
                    do
                    {
                        if (ContentEquals(Trim(rawNext.SubSequence(begin, end)), expected))
                        {
                            return true;
                        }
                        begin = end + 1;
                    }
                    while ((end = IndexOf(rawNext, ',', begin)) != -1);

                    if (begin < rawNext.Count)
                    {
                        if (ContentEquals(Trim(rawNext.SubSequence(begin, rawNext.Count)), expected))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public bool TryGetAsString(AsciiString name, out string value)
        {
            if (this.TryGet(name, out ICharSequence v))
            {
                value = v.ToString();
                return true;
            }
            else
            {
                value = default(string);
                return false;
            }
        }

        public IList<string> GetAllAsString(AsciiString name)
        {
            var values = new List<string>();
            IList<ICharSequence> list = this.GetAll(name);
            foreach (ICharSequence value in list)
            {
                values.Add(value.ToString());
            }

            return values;
        }

        public abstract IEnumerator<HeaderEntry<AsciiString, ICharSequence>> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public override string ToString() => HeadersUtils.ToString(this, this.Size);

        /// <summary>
        /// Deep copy of the headers.
        /// </summary>
        /// <returns>A deap copy of this.</returns>
        public virtual HttpHeaders Copy()
        {
            var copy = new DefaultHttpHeaders();
            copy.Set(this);
            return copy;
        }
    }
}
