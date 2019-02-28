// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable PossibleUnintendedReferenceComparison
// ReSharper disable EmptyGeneralCatchClause
namespace DotNetty.Codecs
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Utilities;

    using static Common.Internal.MathUtil;
    using static HeadersUtils;

    public class DefaultHeaders<TKey, TValue> : IHeaders<TKey, TValue>
        where TKey : class
    {
        const int HashCodeSeed = unchecked((int)0xc2b2ae35);

        static readonly DefaultHashingStrategy<TValue> DefaultValueHashingStrategy = new DefaultHashingStrategy<TValue>();
        static readonly DefaultHashingStrategy<TKey> DefaultKeyHashingStragety = new DefaultHashingStrategy<TKey>();
        static readonly NullNameValidator<TKey> DefaultKeyNameValidator = new NullNameValidator<TKey>();

        readonly HeaderEntry<TKey, TValue>[] entries;
        readonly HeaderEntry<TKey, TValue> head;

        readonly byte hashMask;
        protected readonly IValueConverter<TValue> ValueConverter;
        readonly INameValidator<TKey> nameValidator;
        readonly IHashingStrategy<TKey> hashingStrategy;
        int size;

        public DefaultHeaders(IValueConverter<TValue> valueConverter)
            : this(DefaultKeyHashingStragety, valueConverter, DefaultKeyNameValidator, 16)
        {
        }

        public DefaultHeaders(IValueConverter<TValue> valueConverter, INameValidator<TKey> nameValidator)
            : this(DefaultKeyHashingStragety, valueConverter, nameValidator, 16)
        {
        }

        public DefaultHeaders(IHashingStrategy<TKey> nameHashingStrategy, IValueConverter<TValue> valueConverter, INameValidator<TKey> nameValidator) 
            : this(nameHashingStrategy, valueConverter, nameValidator, 16)
        {
        }

        public DefaultHeaders(IHashingStrategy<TKey> nameHashingStrategy,
            IValueConverter<TValue> valueConverter, INameValidator<TKey> nameValidator, int arraySizeHint)
        {
            if (ReferenceEquals(nameHashingStrategy, null)) ThrowArgumentNullException(nameof(nameHashingStrategy));
            if (ReferenceEquals(valueConverter, null)) ThrowArgumentNullException(nameof(valueConverter));
            if (ReferenceEquals(nameValidator, null)) ThrowArgumentNullException(nameof(nameValidator));

            this.hashingStrategy = nameHashingStrategy;
            this.ValueConverter = valueConverter;
            this.nameValidator = nameValidator;

            // Enforce a bound of [2, 128] because hashMask is a byte. The max possible value of hashMask is one less
            // than the length of this array, and we want the mask to be > 0.
            this.entries = new HeaderEntry<TKey, TValue>[FindNextPositivePowerOfTwo(Math.Max(2, Math.Min(arraySizeHint, 128)))];
            this.hashMask = (byte)(this.entries.Length - 1);
            this.head = new HeaderEntry<TKey, TValue>();
        }

        public bool TryGet(TKey name, out TValue value)
        {
            if (name == null) ThrowArgumentNullException(nameof(name));

            bool found = false;
            int h = this.hashingStrategy.HashCode(name);
            int i = this.Index(h);
            HeaderEntry<TKey, TValue> e = this.entries[i];
            value = default(TValue);
            // loop until the first header was found
            while (e != null)
            {
                if (e.Hash == h && this.hashingStrategy.Equals(name, e.key))
                {
                    value = e.value;
                    found = true;
                }

                e = e.Next;
            }
            return found;
        }

        public TValue Get(TKey name, TValue defaultValue) => this.TryGet(name, out TValue value) ? value : defaultValue;

        public bool TryGetAndRemove(TKey name, out TValue value)
        {
            if (name == null) ThrowArgumentNullException(nameof(name));

            int h = this.hashingStrategy.HashCode(name);
            return this.TryRemove0(h, this.Index(h), name, out value);
        }

        public TValue GetAndRemove(TKey name, TValue defaultValue) => this.TryGetAndRemove(name, out TValue value) ? value : defaultValue;

        public virtual IList<TValue> GetAll(TKey name)
        {
            if (name == null) ThrowArgumentNullException(nameof(name));

            var values = new List<TValue>();
            int h = this.hashingStrategy.HashCode(name);
            int i = this.Index(h);
            HeaderEntry<TKey, TValue> e = this.entries[i];
            while (e != null)
            {
                if (e.Hash == h && this.hashingStrategy.Equals(name, e.key))
                {
                    values.Insert(0, e.value);
                }

                e = e.Next;
            }
            return values;
        }

        public virtual IEnumerable<TValue> ValueIterator(TKey name) => new ValueEnumerator(this, name);

        public IList<TValue> GetAllAndRemove(TKey name)
        {
            IList<TValue> all = this.GetAll(name);
            this.Remove(name);
            return all;
        }

        public bool Contains(TKey name) => this.TryGet(name, out _);

        public bool ContainsObject(TKey name, object value)
        {
            if (value == null) ThrowArgumentNullException(nameof(value));

            return this.Contains(name, this.ValueConverter.ConvertObject(value));
        }

        public bool ContainsBoolean(TKey name, bool value) => this.Contains(name, this.ValueConverter.ConvertBoolean(value));

        public bool ContainsByte(TKey name, byte value) => this.Contains(name, this.ValueConverter.ConvertByte(value));

        public bool ContainsChar(TKey name, char value) => this.Contains(name, this.ValueConverter.ConvertChar(value));

        public bool ContainsShort(TKey name, short value) => this.Contains(name, this.ValueConverter.ConvertShort(value));

        public bool ContainsInt(TKey name, int value) => this.Contains(name, this.ValueConverter.ConvertInt(value));

        public bool ContainsLong(TKey name, long value) => this.Contains(name, this.ValueConverter.ConvertLong(value));

        public bool ContainsFloat(TKey name, float value) => this.Contains(name, this.ValueConverter.ConvertFloat(value));

        public bool ContainsDouble(TKey name, double value) => this.Contains(name, this.ValueConverter.ConvertDouble(value));

        public bool ContainsTimeMillis(TKey name, long value) => this.Contains(name, this.ValueConverter.ConvertTimeMillis(value));

        public bool Contains(TKey name, TValue value) => this.Contains(name, value, DefaultValueHashingStrategy);

        public bool Contains(TKey name, TValue value, IHashingStrategy<TValue> valueHashingStrategy)
        {
            if (name == null) ThrowArgumentNullException(nameof(name));

            int h = this.hashingStrategy.HashCode(name);
            int i = this.Index(h);
            HeaderEntry<TKey, TValue> e = this.entries[i];
            while (e != null)
            {
                if (e.Hash == h && this.hashingStrategy.Equals(name, e.key) 
                    && valueHashingStrategy.Equals(value, e.value))
                {
                    return true;
                }
                e = e.Next;
            }
            return false;
        }

        public int Size => this.size;

        public bool IsEmpty => this.head == this.head.After;

        public ISet<TKey> Names()
        {
            if (this.IsEmpty)
            {
                return ImmutableHashSet<TKey>.Empty;
            }

            var names = new HashSet<TKey>(this.hashingStrategy);
            HeaderEntry<TKey, TValue> e = this.head.After;
            while (e != this.head)
            {
                names.Add(e.key);
                e = e.After;
            }
            return names;
        }

        public virtual IHeaders<TKey, TValue> Add(TKey name, TValue value)
        {
            if (ReferenceEquals(value, null)) ThrowArgumentNullException(nameof(value));

            this.nameValidator.ValidateName(name);
            int h = this.hashingStrategy.HashCode(name);
            int i = this.Index(h);
            this.Add0(h, i, name, value);
            return this;
        }

        public virtual IHeaders<TKey, TValue> Add(TKey name, IEnumerable<TValue> values)
        {
            this.nameValidator.ValidateName(name);
            int h = this.hashingStrategy.HashCode(name);
            int i = this.Index(h);
            foreach (TValue v in values)
            {
                this.Add0(h, i, name, v);
            }
            return this;
        }

        public virtual IHeaders<TKey, TValue> AddObject(TKey name, object value)
        {
            if (value == null) ThrowArgumentNullException(nameof(value));

            return this.Add(name, this.ValueConverter.ConvertObject(value));
        }

        public virtual IHeaders<TKey, TValue> AddObject(TKey name, IEnumerable<object> values)
        {
            foreach (object value in values)
            {
                this.AddObject(name, value);
            }
            return this;
        }

        public virtual IHeaders<TKey, TValue> AddObject(TKey name, params object[] values)
        {
            // ReSharper disable once ForCanBeConvertedToForeach
            // Avoid enumerator allocations
            for (int i = 0; i < values.Length; i++)
            {
                this.AddObject(name, values[i]);
            }

            return this;
        }

        public IHeaders<TKey, TValue> AddInt(TKey name, int value) => this.Add(name, this.ValueConverter.ConvertInt(value));

        public IHeaders<TKey, TValue> AddLong(TKey name, long value) => this.Add(name, this.ValueConverter.ConvertLong(value));

        public IHeaders<TKey, TValue> AddDouble(TKey name, double value) => this.Add(name, this.ValueConverter.ConvertDouble(value));

        public IHeaders<TKey, TValue> AddTimeMillis(TKey name, long value) => this.Add(name, this.ValueConverter.ConvertTimeMillis(value));

        public IHeaders<TKey, TValue> AddChar(TKey name, char value) => this.Add(name, this.ValueConverter.ConvertChar(value));

        public IHeaders<TKey, TValue> AddBoolean(TKey name, bool value) => this.Add(name, this.ValueConverter.ConvertBoolean(value));

        public IHeaders<TKey, TValue> AddFloat(TKey name, float value) =>  this.Add(name, this.ValueConverter.ConvertFloat(value));

        public IHeaders<TKey, TValue> AddByte(TKey name, byte value) => this.Add(name, this.ValueConverter.ConvertByte(value));

        public IHeaders<TKey, TValue> AddShort(TKey name, short value) => this.Add(name, this.ValueConverter.ConvertShort(value));

        public virtual IHeaders<TKey, TValue> Add(IHeaders<TKey, TValue> headers)
        {
            if (ReferenceEquals(headers, this))
            {
                ThrowArgumentException("can't add to itself.");
            }
            this.AddImpl(headers);
            return this;
        }

        protected void AddImpl(IHeaders<TKey, TValue> headers)
        {
            if (headers is DefaultHeaders<TKey, TValue> defaultHeaders)
            {
                HeaderEntry<TKey, TValue> e = defaultHeaders.head.After;

                if (defaultHeaders.hashingStrategy == this.hashingStrategy
                    && defaultHeaders.nameValidator == this.nameValidator)
                {
                    // Fastest copy
                    while (e != defaultHeaders.head)
                    {
                        this.Add0(e.Hash, this.Index(e.Hash), e.key, e.value);
                        e = e.After;
                    }
                }
                else
                {
                    // Fast copy
                    while (e != defaultHeaders.head)
                    {
                        this.Add(e.key, e.value);
                        e = e.After;
                    }
                }
            }
            else
            {
                // Slow copy
                foreach (HeaderEntry<TKey, TValue> header in headers)
                {
                    this.Add(header.key, header.value);
                }
            }
        }

        public IHeaders<TKey, TValue> Set(TKey name, TValue value)
        {
            if (ReferenceEquals(value, null)) ThrowArgumentNullException(nameof(value));

            this.nameValidator.ValidateName(name);
            int h = this.hashingStrategy.HashCode(name);
            int i = this.Index(h);
            this.TryRemove0(h, i, name, out _);
            this.Add0(h, i, name, value);
            return this;
        }

        public virtual IHeaders<TKey, TValue> Set(TKey name, IEnumerable<TValue> values)
        {
            if (ReferenceEquals(values, null)) ThrowArgumentNullException(nameof(values));

            this.nameValidator.ValidateName(name);
            int h = this.hashingStrategy.HashCode(name);
            int i = this.Index(h);

            this.TryRemove0(h, i, name, out _);
            // ReSharper disable once PossibleNullReferenceException
            foreach (TValue v in values)
            {
                if (v ==  null)
                {
                    break;
                }
                this.Add0(h, i, name, v);
            }

            return this;
        }

        public virtual IHeaders<TKey, TValue> SetObject(TKey name, object value)
        {
            if (value == null) ThrowArgumentNullException(nameof(value));

            TValue convertedValue = this.ValueConverter.ConvertObject(value);
            return this.Set(name, convertedValue);
        }

        public virtual IHeaders<TKey, TValue> SetObject(TKey name, IEnumerable<object> values)
        {
            if (ReferenceEquals(values, null)) ThrowArgumentNullException(nameof(values));

            this.nameValidator.ValidateName(name);
            int h = this.hashingStrategy.HashCode(name);
            int i = this.Index(h);

            this.TryRemove0(h, i, name, out _);
            // ReSharper disable once PossibleNullReferenceException
            foreach (object v in values)
            {
                if (v == null)
                {
                    break;
                }
                this.Add0(h, i, name, this.ValueConverter.ConvertObject(v));
            }

            return this;
        }

        public IHeaders<TKey, TValue> SetInt(TKey name, int value) => this.Set(name, this.ValueConverter.ConvertInt(value));

        public IHeaders<TKey, TValue> SetLong(TKey name, long value) => this.Set(name, this.ValueConverter.ConvertLong(value));

        public IHeaders<TKey, TValue> SetDouble(TKey name, double value) => this.Set(name, this.ValueConverter.ConvertDouble(value));

        public IHeaders<TKey, TValue> SetTimeMillis(TKey name, long value) => this.Set(name, this.ValueConverter.ConvertTimeMillis(value));

        public IHeaders<TKey, TValue> SetFloat(TKey name, float value) => this.Set(name, this.ValueConverter.ConvertFloat(value));

        public IHeaders<TKey, TValue> SetChar(TKey name, char value) => this.Set(name, this.ValueConverter.ConvertChar(value));

        public IHeaders<TKey, TValue> SetBoolean(TKey name, bool value) => this.Set(name, this.ValueConverter.ConvertBoolean(value));

        public IHeaders<TKey, TValue> SetByte(TKey name, byte value) => this.Set(name, this.ValueConverter.ConvertByte(value));

        public IHeaders<TKey, TValue> SetShort(TKey name, short value) => this.Set(name, this.ValueConverter.ConvertShort(value));

        public virtual IHeaders<TKey, TValue> Set(IHeaders<TKey, TValue> headers)
        {
            if (!ReferenceEquals(headers, this))
            {
                this.Clear();
                this.AddImpl(headers);
            }
            return this;
        }

        public virtual IHeaders<TKey, TValue> SetAll(IHeaders<TKey, TValue> headers)
        {
            if (!ReferenceEquals(headers, this))
            {
                foreach (TKey key in headers.Names())
                {
                    this.Remove(key);
                }
                this.AddImpl(headers);
            }
            return this;
        }

        public bool Remove(TKey name) => this.TryGetAndRemove(name, out _);

        public IHeaders<TKey, TValue> Clear()
        {
            Array.Clear(this.entries, 0, this.entries.Length);
            this.head.Before = this.head.After = this.head;
            this.size = 0;
            return this;
        }

        public IEnumerator<HeaderEntry<TKey, TValue>> GetEnumerator() => new HeaderEnumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public bool TryGetBoolean(TKey name, out bool value)
        {
            if (this.TryGet(name, out TValue v))
            {
                try
                {
                    value = this.ValueConverter.ConvertToBoolean(v);
                    return true;
                }
                catch (Exception)
                {
                    // Ignore
                }
            }

            value = default(bool);
            return false;
        }

        public bool GetBoolean(TKey name, bool defaultValue) => this.TryGetBoolean(name, out bool value) ? value : defaultValue;

        public bool TryGetByte(TKey name, out byte value)
        {
            if (this.TryGet(name, out TValue v))
            {
                try
                {
                    value = this.ValueConverter.ConvertToByte(v);
                    return true;
                }
                catch (Exception)
                {
                    // Ignore
                }
            }

            value = default(byte);
            return false;
        }

        public byte GetByte(TKey name, byte defaultValue) => this.TryGetByte(name, out byte value) ? value : defaultValue;

        public bool TryGetChar(TKey name, out char value)
        {
            if (this.TryGet(name, out TValue v))
            {
                try
                {
                    value = this.ValueConverter.ConvertToChar(v);
                    return true;
                }
                catch (Exception)
                {
                    // Ignore
                }
            }

            value = default(char);
            return false;
        }

        public char GetChar(TKey name, char defaultValue) => this.TryGetChar(name, out char value) ? value : defaultValue;

        public bool TryGetShort(TKey name, out short value)
        {
            if (this.TryGet(name, out TValue v))
            {
                try
                {
                    value = this.ValueConverter.ConvertToShort(v);
                    return true;
                }
                catch (Exception)
                {
                    // Ignore
                }
            }

            value = default(short);
            return false;
        }

        public short GetShort(TKey name, short defaultValue) => this.TryGetShort(name, out short value) ? value : defaultValue;

        public bool TryGetInt(TKey name, out int value)
        {
            if (this.TryGet(name, out TValue v))
            {
                try
                {
                    value = this.ValueConverter.ConvertToInt(v);
                    return true;
                }
                catch(Exception)
                {
                    // Ignore
                }
            }

            value = default(int);
            return false;
        }

        public int GetInt(TKey name, int defaultValue) => this.TryGetInt(name, out int value) ? value : defaultValue;

        public bool TryGetLong(TKey name, out long value)
        {
            if (this.TryGet(name, out TValue v))
            {
                try
                {
                    value = this.ValueConverter.ConvertToLong(v);
                    return true;
                }
                catch (Exception)
                {
                    // Ignore
                }
            }

            value = default(long);
            return false;
        }

        public long GetLong(TKey name, long defaultValue) => this.TryGetLong(name, out long value) ? value : defaultValue;

        public bool TryGetFloat(TKey name, out float value)
        {
            if (this.TryGet(name, out TValue v))
            {
                try
                {
                    value = this.ValueConverter.ConvertToFloat(v);
                    return true;
                }
                catch (Exception)
                {
                    // Ignore
                }
            }

            value = default(float);
            return false;
        }

        public float GetFloat(TKey name, float defaultValue) => this.TryGetFloat(name, out float value) ? value : defaultValue;

        public bool TryGetDouble(TKey name, out double value)
        {
            if (this.TryGet(name, out TValue v))
            {
                try
                {
                    value = this.ValueConverter.ConvertToDouble(v);
                    return true;
                }
                catch (Exception)
                {
                    // Ignore
                }
            }

            value = default(double);
            return false;
        }

        public double GetDouble(TKey name, double defaultValue) => this.TryGetDouble(name, out double value) ? value : defaultValue;

        public bool TryGetTimeMillis(TKey name, out long value)
        {
            if (this.TryGet(name, out TValue v))
            {
                try
                {
                    value = this.ValueConverter.ConvertToTimeMillis(v);
                    return true;
                }
                catch (Exception)
                {
                    // Ignore
                }
            }

            value = default(long);
            return false;
        }

        public long GetTimeMillis(TKey name, long defaultValue) => this.TryGetTimeMillis(name, out long value) ? value : defaultValue;

        public bool TryGetBooleanAndRemove(TKey name, out bool value)
        {
            if (this.TryGetAndRemove(name, out TValue v))
            {
                try
                {
                    value = this.ValueConverter.ConvertToBoolean(v);
                    return true;
                }
                catch (Exception)
                {
                    // Ignore
                }
            }

            value = default(bool);
            return false;
        }

        public bool GetBooleanAndRemove(TKey name, bool defaultValue) => this.TryGetBooleanAndRemove(name, out bool value) ? value : defaultValue;

        public bool TryGetByteAndRemove(TKey name, out byte value)
        {
            if (this.TryGetAndRemove(name, out TValue v))
            {
                try
                {
                    value = this.ValueConverter.ConvertToByte(v);
                    return true;
                }
                catch (Exception)
                {
                    // Ignore
                }
            }
            value = default(byte);
            return false;
        }

        public byte GetByteAndRemove(TKey name, byte defaultValue) => this.TryGetByteAndRemove(name, out byte value) ? value : defaultValue;

        public bool TryGetCharAndRemove(TKey name, out char value)
        {
            if (this.TryGetAndRemove(name, out TValue v))
            {
                try
                {
                    value = this.ValueConverter.ConvertToChar(v);
                    return true;
                }
                catch (Exception)
                {
                    // Ignore
                }
            }

            value = default(char);
            return false;
        }

        public char GetCharAndRemove(TKey name, char defaultValue) => this.TryGetCharAndRemove(name, out char value) ? value : defaultValue;

        public bool TryGetShortAndRemove(TKey name, out short value)
        {
            if (this.TryGetAndRemove(name, out TValue v))
            {
                try
                {
                    value = this.ValueConverter.ConvertToShort(v);
                    return true;
                }
                catch (Exception)
                {
                    // Ignore
                }
            }

            value = default(short);
            return false;
        }

        public short GetShortAndRemove(TKey name, short defaultValue) => this.TryGetShortAndRemove(name, out short value) ? value : defaultValue;

        public bool TryGetIntAndRemove(TKey name, out int value)
        {
            if (this.TryGetAndRemove(name, out TValue v))
            {
                try
                {
                    value = this.ValueConverter.ConvertToInt(v);
                    return true;
                }
                catch (Exception)
                {
                    // Ignore
                }
            }

            value = default(int);
            return false;
        }

        public int GetIntAndRemove(TKey name, int defaultValue) => this.TryGetIntAndRemove(name, out int value) ? value : defaultValue;

        public bool TryGetLongAndRemove(TKey name, out long value)
        {
            if (this.TryGetAndRemove(name, out TValue v))
            {
                try
                {
                    value = this.ValueConverter.ConvertToLong(v);
                    return true;
                }
                catch (Exception)
                {
                    // Ignore
                }
            }

            value = default(long);
            return false;
        }

        public long GetLongAndRemove(TKey name, long defaultValue) => this.TryGetLongAndRemove(name, out long value) ? value : defaultValue;

        public bool TryGetFloatAndRemove(TKey name, out float value)
        {
            if (this.TryGetAndRemove(name, out TValue v))
            {
                try
                {
                    value = this.ValueConverter.ConvertToFloat(v);
                    return true;
                }
                catch (Exception)
                {
                    // Ignore
                }
            }

            value = default(float);
            return false;
        }

        public float GetFloatAndRemove(TKey name, float defaultValue) => this.TryGetFloatAndRemove(name, out float value) ? value : defaultValue;

        public bool TryGetDoubleAndRemove(TKey name, out double value)
        {
            if (this.TryGetAndRemove(name, out TValue v))
            {
                try
                {
                    value = this.ValueConverter.ConvertToDouble(v);
                    return true;
                }
                catch (Exception)
                {
                    // Ignore
                }
            }

            value = default(double);
            return false;
        }

        public double GetDoubleAndRemove(TKey name, double defaultValue) => this.TryGetDoubleAndRemove(name, out double value) ? value : defaultValue;

        public bool TryGetTimeMillisAndRemove(TKey name, out long value)
        {
            if (this.TryGetAndRemove(name, out TValue v))
            {
                try
                {
                    value = this.ValueConverter.ConvertToTimeMillis(v);
                    return true;
                }
                catch (Exception)
                {
                    // Ignore
                }
            }

            value = default(long);
            return false;
        }

        public long GetTimeMillisAndRemove(TKey name, long defaultValue) => this.TryGetTimeMillisAndRemove(name, out long value) ? value : defaultValue;

        public override bool Equals(object obj) => obj is IHeaders<TKey, TValue> headers && this.Equals(headers, DefaultValueHashingStrategy);

        public override int GetHashCode() => this.HashCode(DefaultValueHashingStrategy);

        public bool Equals(IHeaders<TKey, TValue> h2, IHashingStrategy<TValue> valueHashingStrategy)
        {
            if (h2.Size != this.size)
            {
                return false;
            }

            if (ReferenceEquals(this, h2))
            {
                return true;
            }

            foreach (TKey name in this.Names())
            {
                IList<TValue> otherValues = h2.GetAll(name);
                IList<TValue> values = this.GetAll(name);
                if (otherValues.Count != values.Count)
                {
                    return false;
                }
                for (int i = 0; i < otherValues.Count; i++)
                {
                    if (!valueHashingStrategy.Equals(otherValues[i], values[i]))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public int HashCode(IHashingStrategy<TValue> valueHashingStrategy)
        {
            int result = HashCodeSeed;
            foreach (TKey name in this.Names())
            {
                result = 31 * result + this.hashingStrategy.HashCode(name);
                IList<TValue> values = this.GetAll(name);
                for (int i = 0; i < values.Count; ++i)
                {
                    result = 31 * result + valueHashingStrategy.HashCode(values[i]);
                }
            }
            return result;
        }

        public override string ToString() => HeadersUtils.ToString(this, this.size);

        protected HeaderEntry<TKey, TValue> NewHeaderEntry(int h, TKey name, TValue value, HeaderEntry<TKey, TValue> next) =>
            new HeaderEntry<TKey, TValue>(h, name, value, next, this.head);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int Index(int hash) => hash & this.hashMask;

        void Add0(int h, int i, TKey name, TValue value)
        {
            // Update the hash table.
            this.entries[i] = this.NewHeaderEntry(h, name, value, this.entries[i]);
            ++this.size;
        }

        bool TryRemove0(int h, int i, TKey name, out TValue value)
        {
            value = default(TValue);

            HeaderEntry<TKey, TValue> e = this.entries[i];
            if (e == null)
            {
                return false;
            }

            bool result = false;

            HeaderEntry<TKey, TValue> next = e.Next;
            while (next != null)
            {
                if (next.Hash == h && this.hashingStrategy.Equals(name, next.key))
                {
                    value = next.value;
                    e.Next = next.Next;
                    next.Remove();
                    --this.size;
                    result = true;
                }
                else
                {
                    e = next;
                }

                next = e.Next;
            }

            e = this.entries[i];
            if (e.Hash == h && this.hashingStrategy.Equals(name, e.key))
            {
                if (!result)
                {
                    value = e.value;
                    result = true;
                }
                this.entries[i] = e.Next;
                e.Remove();
                --this.size;
            }

            return result;
        }

        public DefaultHeaders<TKey, TValue> Copy()
        {
            var copy = new DefaultHeaders<TKey, TValue>(this.hashingStrategy,  this.ValueConverter, this.nameValidator, this.entries.Length);
            copy.AddImpl(this);
            return copy;
        }

        struct ValueEnumerator : IEnumerator<TValue>, IEnumerable<TValue>
        {
            readonly IHashingStrategy<TKey> hashingStrategy;
            readonly int hash;
            readonly TKey name;
            readonly HeaderEntry<TKey, TValue> head;
            HeaderEntry<TKey, TValue> node;
            TValue current;

            public ValueEnumerator(DefaultHeaders<TKey, TValue> headers, TKey name)
            {
                if (name == null) ThrowArgumentNullException(nameof(name));

                this.hashingStrategy = headers.hashingStrategy;
                this.hash = this.hashingStrategy.HashCode(name);
                this.name = name;
                this.node = this.head = headers.entries[headers.Index(this.hash)];
                this.current = default(TValue);
            }

            bool IEnumerator.MoveNext()
            {
                if (this.node == null)
                {
                    return false;
                }

                this.current = this.node.value;
                this.CalculateNext(this.node.Next);
                return true;
            }

            void CalculateNext(HeaderEntry<TKey, TValue> entry)
            {
                while (entry != null)
                {
                    if (entry.Hash == this.hash && this.hashingStrategy.Equals(this.name, entry.key))
                    {
                        this.node = entry;
                        return;
                    }
                    entry = entry.Next;
                }
                this.node = null;
            }

            TValue IEnumerator<TValue>.Current => this.current;

            object IEnumerator.Current => this.current;

            void IEnumerator.Reset()
            {
                this.node = this.head;
                this.current = default(TValue);
            }

            void IDisposable.Dispose()
            {
                this.node = null;
                this.current = default(TValue);
            }

            public IEnumerator<TValue> GetEnumerator() => this;

            IEnumerator IEnumerable.GetEnumerator() => this;
        }

        struct HeaderEnumerator : IEnumerator<HeaderEntry<TKey, TValue>>
        {
            readonly HeaderEntry<TKey, TValue> head;
            readonly int size;

            HeaderEntry<TKey, TValue> node;
            int index;

            public HeaderEnumerator(DefaultHeaders<TKey, TValue> headers)
            {
                this.head = headers.head;
                this.size = headers.size;
                this.node = this.head;
                this.index = 0;
            }

            public HeaderEntry<TKey, TValue> Current => this.node;

            object IEnumerator.Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    if (this.index == 0 || this.index == this.size + 1)
                    {
                        ThrowInvalidOperationException("Enumerator not initialized or completed.");
                    }
                    return this.node;
                }
            }

            public bool MoveNext()
            {
                if (this.node == null)
                {
                    this.index = this.size + 1;
                    return false;
                }

                this.index++;
                this.node = this.node.After;
                if (this.node == this.head)
                {
                    this.node = null;
                    return false;
                }
                return true;
            }

            public void Reset()
            {
                this.node = this.head.After;
                this.index = 0;
            }

            public void Dispose()
            {
                this.node = null;
                this.index = 0;
            }
        }
    }

    public sealed class HeaderEntry<TKey, TValue>
        where TKey : class
    {
        internal readonly int Hash;
        // ReSharper disable InconsistentNaming
        internal readonly TKey key;
        internal TValue value;
        // ReSharper restore InconsistentNaming

        internal HeaderEntry<TKey, TValue> Next;
        internal HeaderEntry<TKey, TValue> Before;
        internal HeaderEntry<TKey, TValue> After;

        public HeaderEntry(int hash, TKey key)
        {
            this.Hash = hash;
            this.key = key;
        }

        internal HeaderEntry()
        {
            this.Hash = -1;
            this.key = default(TKey);
            this.Before = this;
            this.After = this;
        }

        internal HeaderEntry(int hash, TKey key, TValue value,
            HeaderEntry<TKey, TValue> next, HeaderEntry<TKey, TValue> head)
        {
            this.Hash = hash;
            this.key = key;
            this.value = value;
            this.Next = next;

            this.After = head;
            this.Before = head.Before;
            // PointNeighborsToThis
            this.Before.After = this;
            this.After.Before = this;
        }

        internal void Remove()
        {
            this.Before.After = this.After;
            this.After.Before = this.Before;
        }

        public TKey Key => this.key;

        public TValue Value => this.value;

        public TValue SetValue(TValue newValue)
        {
            if (ReferenceEquals(newValue, null)) ThrowArgumentNullException(nameof(newValue));

            TValue oldValue = this.value;
            this.value = newValue;
            return oldValue;
        }

        public override string ToString() => $"{this.key}={this.value}";

        // ReSharper disable once MergeConditionalExpression
        public override bool Equals(object obj) => obj is HeaderEntry<TKey, TValue> other 
            && (this.key == null ? other.key == null : this.key.Equals(other.key))
            && (ReferenceEquals(this.value, null) ? ReferenceEquals(other.value, null) : this.value.Equals(other.value));

        // ReSharper disable NonReadonlyMemberInGetHashCode
        public override int GetHashCode() => (this.key == null ? 0 : this.key.GetHashCode()) 
                ^ (ReferenceEquals(this.value, null) ? 0 : this.value.GetHashCode());
        // ReSharper restore NonReadonlyMemberInGetHashCode
    }
}
