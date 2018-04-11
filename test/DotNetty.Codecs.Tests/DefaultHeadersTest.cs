// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using DotNetty.Common.Utilities;
    using Xunit;

    using static Common.Utilities.AsciiString;

    public sealed class DefaultHeadersTest
    {
        sealed class TestDefaultHeaders : DefaultHeaders<ICharSequence, ICharSequence>
        {
            public TestDefaultHeaders() : this(CharSequenceValueConverter.Default)
            {
            }

            public TestDefaultHeaders(IValueConverter<ICharSequence> converter) : base(converter)
            {
            }
        }

        static TestDefaultHeaders NewInstance() => new TestDefaultHeaders();

        [Fact]
        public void AddShouldIncreaseAndRemoveShouldDecreaseTheSize()
        {
            TestDefaultHeaders headers = NewInstance();
            Assert.Equal(0, headers.Size);
            headers.Add(Of("name1"), new[] { Of("value1"), Of("value2") });
            Assert.Equal(2, headers.Size);
            headers.Add(Of("name2"), new[] { Of("value3"), Of("value4") });
            Assert.Equal(4, headers.Size);
            headers.Add(Of("name3"), Of("value5"));
            Assert.Equal(5, headers.Size);

            headers.Remove(Of("name3"));
            Assert.Equal(4, headers.Size);
            headers.Remove(Of("name1"));
            Assert.Equal(2, headers.Size);
            headers.Remove(Of("name2"));
            Assert.Equal(0, headers.Size);
            Assert.True(headers.IsEmpty);
        }

        [Fact]
        public void AfterClearHeadersShouldBeEmpty()
        {
            TestDefaultHeaders headers = NewInstance();
            headers.Add(Of("name1"), Of("value1"));
            headers.Add(Of("name2"), Of("value2"));
            Assert.Equal(2, headers.Size);
            headers.Clear();
            Assert.Equal(0, headers.Size);
            Assert.True(headers.IsEmpty);
            Assert.False(headers.Contains(Of("name1")));
            Assert.False(headers.Contains(Of("name2")));
        }

        [Fact]
        public void RemovingANameForASecondTimeShouldReturnFalse()
        {
            TestDefaultHeaders headers = NewInstance();
            headers.Add(Of("name1"), Of("value1"));
            headers.Add(Of("name2"), Of("value2"));
            Assert.True(headers.Remove(Of("name2")));
            Assert.False(headers.Remove(Of("name2")));
        }

        [Fact]
        public void MultipleValuesPerNameShouldBeAllowed()
        {
            TestDefaultHeaders headers = NewInstance();
            headers.Add(Of("name"), Of("value1"));
            headers.Add(Of("name"), Of("value2"));
            headers.Add(Of("name"), Of("value3"));
            Assert.Equal(3, headers.Size);

            IList<ICharSequence> values = headers.GetAll(Of("name"));
            Assert.Equal(3, values.Count);
            Assert.True(values.Contains(Of("value1")));
            Assert.True(values.Contains(Of("value2")));
            Assert.True(values.Contains(Of("value3")));
        }

        [Fact]
        public void MultipleValuesPerNameIterator()
        {
            TestDefaultHeaders headers = NewInstance();
            headers.Add(Of("name"), Of("value1"));
            headers.Add(Of("name"), Of("value2"));
            headers.Add(Of("name"), Of("value3"));
            Assert.Equal(3, headers.Size);

            var values = new List<ICharSequence>();
            foreach (ICharSequence value in headers.ValueIterator(Of("name")))
            {
                values.Add(value);
            }
            Assert.Equal(3, values.Count);
            Assert.Contains(Of("value1"), values);
            Assert.Contains(Of("value2"), values);
            Assert.Contains(Of("value3"), values);
        }

        [Fact]
        public void MultipleValuesPerNameIteratorEmpty()
        {
            TestDefaultHeaders headers = NewInstance();
            var values = new List<ICharSequence>();
            foreach (ICharSequence value in headers.ValueIterator(Of("name")))
            {
                values.Add(value);
            }
            Assert.Empty(values);
        }

        [Fact]
        public void Contains()
        {
            TestDefaultHeaders headers = NewInstance();
            headers.AddBoolean(Of("boolean"), true);
            Assert.True(headers.ContainsBoolean(Of("boolean"), true));
            Assert.False(headers.ContainsBoolean(Of("boolean"), false));

            headers.AddLong(Of("long"), long.MaxValue);
            Assert.True(headers.ContainsLong(Of("long"), long.MaxValue));
            Assert.False(headers.ContainsLong(Of("long"), long.MinValue));

            headers.AddInt(Of("int"), int.MinValue);
            Assert.True(headers.ContainsInt(Of("int"), int.MinValue));
            Assert.False(headers.ContainsInt(Of("int"), int.MaxValue));

            headers.AddShort(Of("short"), short.MaxValue);
            Assert.True(headers.ContainsShort(Of("short"), short.MaxValue));
            Assert.False(headers.ContainsShort(Of("short"), short.MinValue));

            headers.AddChar(Of("char"), char.MaxValue);
            Assert.True(headers.ContainsChar(Of("char"), char.MaxValue));
            Assert.False(headers.ContainsChar(Of("char"), char.MinValue));

            headers.AddByte(Of("byte"), byte.MaxValue);
            Assert.True(headers.ContainsByte(Of("byte"), byte.MaxValue));
            Assert.False(headers.ContainsByte(Of("byte"), byte.MinValue));

            headers.AddDouble(Of("double"), double.MaxValue);
            Assert.True(headers.ContainsDouble(Of("double"), double.MaxValue));
            Assert.False(headers.ContainsDouble(Of("double"), double.MinValue));

            headers.AddFloat(Of("float"), float.MaxValue);
            Assert.True(headers.ContainsFloat(Of("float"), float.MaxValue));
            Assert.False(headers.ContainsFloat(Of("float"), float.MinValue));

            long millis = (long)Math.Floor(DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerMillisecond);
            headers.AddTimeMillis(Of("millis"), millis);
            Assert.True(headers.ContainsTimeMillis(Of("millis"), millis));
            // This test doesn't work on midnight, January 1, 1970 UTC
            Assert.False(headers.ContainsTimeMillis(Of("millis"), 0));

            headers.AddObject(Of("object"), "Hello World");
            Assert.True(headers.ContainsObject(Of("object"), "Hello World"));
            Assert.False(headers.ContainsObject(Of("object"), ""));

            headers.Add(Of("name"), Of("value"));
            Assert.True(headers.Contains(Of("name"), Of("value")));
            Assert.False(headers.Contains(Of("name"), Of("value1")));
        }

        [Fact]
        public void Copy()
        {
            IHeaders<ICharSequence, ICharSequence> headers = NewInstance();
            headers.AddBoolean(Of("boolean"), true);
            headers.AddLong(Of("long"), long.MaxValue);
            headers.AddInt(Of("int"), int.MinValue);
            headers.AddShort(Of("short"), short.MaxValue);
            headers.AddChar(Of("char"), char.MaxValue);
            headers.AddByte(Of("byte"), byte.MaxValue);
            headers.AddDouble(Of("double"), double.MaxValue);
            headers.AddFloat(Of("float"), float.MaxValue);
            long millis = (long)Math.Floor(DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerMillisecond);
            headers.AddTimeMillis(Of("millis"), millis);
            headers.AddObject(Of("object"), "Hello World");
            headers.Add(Of("name"), Of("value"));

            headers = NewInstance().Add(headers);

            Assert.True(headers.ContainsBoolean(Of("boolean"), true));
            Assert.False(headers.ContainsBoolean(Of("boolean"), false));

            Assert.True(headers.ContainsLong(Of("long"), long.MaxValue));
            Assert.False(headers.ContainsLong(Of("long"), long.MinValue));

            Assert.True(headers.ContainsInt(Of("int"), int.MinValue));
            Assert.False(headers.ContainsInt(Of("int"), int.MaxValue));

            Assert.True(headers.ContainsShort(Of("short"), short.MaxValue));
            Assert.False(headers.ContainsShort(Of("short"), short.MinValue));

            Assert.True(headers.ContainsChar(Of("char"), char.MaxValue));
            Assert.False(headers.ContainsChar(Of("char"), char.MinValue));

            Assert.True(headers.ContainsByte(Of("byte"), byte.MaxValue));
            Assert.False(headers.ContainsLong(Of("byte"), byte.MinValue));

            Assert.True(headers.ContainsDouble(Of("double"), double.MaxValue));
            Assert.False(headers.ContainsDouble(Of("double"), double.MinValue));

            Assert.True(headers.ContainsFloat(Of("float"), float.MaxValue));
            Assert.False(headers.ContainsFloat(Of("float"), float.MinValue));

            Assert.True(headers.ContainsTimeMillis(Of("millis"), millis));
            // This test doesn't work on midnight, January 1, 1970 UTC
            Assert.False(headers.ContainsTimeMillis(Of("millis"), 0));

            Assert.True(headers.ContainsObject(Of("object"), "Hello World"));
            Assert.False(headers.ContainsObject(Of("object"), ""));

            Assert.True(headers.Contains(Of("name"), Of("value")));
            Assert.False(headers.Contains(Of("name"), Of("value1")));
        }

        [Fact]
        public void CanMixConvertedAndNormalValues()
        {
            TestDefaultHeaders headers = NewInstance();
            headers.Add(Of("name"), Of("value"));
            headers.AddInt(Of("name"), 100);
            headers.AddBoolean(Of("name"), false);

            Assert.Equal(3, headers.Size);
            Assert.True(headers.Contains(Of("name")));
            Assert.True(headers.Contains(Of("name"), Of("value")));
            Assert.True(headers.ContainsInt(Of("name"), 100));
            Assert.True(headers.ContainsBoolean(Of("name"), false));
        }

        [Fact]
        public void GetAndRemove()
        {
            TestDefaultHeaders headers = NewInstance();
            headers.Add(Of("name1"), Of("value1"));
            headers.Add(Of("name2"), new [] { Of("value2"), Of("value3")});
            headers.Add(Of("name3"), new [] { Of("value4"), Of("value5"), Of("value6") });

            Assert.Equal(Of("value1"), headers.GetAndRemove(Of("name1"), Of("defaultvalue")));
            Assert.True(headers.TryGetAndRemove(Of("name2"), out ICharSequence value));
            Assert.Equal(Of("value2"), value);
            Assert.False(headers.TryGetAndRemove(Of("name2"), out value));
            Assert.Null(value);
            Assert.True(new [] { Of("value4"), Of("value5"), Of("value6") }.SequenceEqual(headers.GetAllAndRemove(Of("name3"))));
            Assert.Equal(0, headers.Size);
            Assert.False(headers.TryGetAndRemove(Of("noname"), out value));
            Assert.Null(value);
            Assert.Equal(Of("defaultvalue"), headers.GetAndRemove(Of("noname"), Of("defaultvalue")));
        }

        [Fact]
        public void WhenNameContainsMultipleValuesGetShouldReturnTheFirst()
        {
            TestDefaultHeaders headers = NewInstance();
            headers.Add(Of("name1"), new []{ Of("value1"), Of("value2")});
            Assert.True(headers.TryGet(Of("name1"), out ICharSequence value));
            Assert.Equal(Of("value1"), value);
        }

        [Fact]
        public void GetWithDefaultValue()
        {
            TestDefaultHeaders headers = NewInstance();
            headers.Add(Of("name1"), Of("value1"));

            Assert.Equal(Of("value1"), headers.Get(Of("name1"), Of("defaultvalue")));
            Assert.Equal(Of("defaultvalue"), headers.Get(Of("noname"), Of("defaultvalue")));
        }

        [Fact]
        public void SetShouldOverWritePreviousValue()
        {
            TestDefaultHeaders headers = NewInstance();
            headers.Set(Of("name"), Of("value1"));
            headers.Set(Of("name"), Of("value2"));
            Assert.Equal(1, headers.Size);
            Assert.Equal(1, headers.GetAll(Of("name")).Count);
            Assert.Equal(Of("value2"), headers.GetAll(Of("name"))[0]);
            Assert.True(headers.TryGet(Of("name"), out ICharSequence value));
            Assert.Equal(Of("value2"), value);
        }

        [Fact]
        public void SetAllShouldOverwriteSomeAndLeaveOthersUntouched()
        {
            TestDefaultHeaders h1 = NewInstance();

            h1.Add(Of("name1"), Of("value1"));
            h1.Add(Of("name2"), Of("value2"));
            h1.Add(Of("name2"), Of("value3"));
            h1.Add(Of("name3"), Of("value4"));

            TestDefaultHeaders h2 = NewInstance();
            h2.Add(Of("name1"), Of("value5"));
            h2.Add(Of("name2"), Of("value6"));
            h2.Add(Of("name1"), Of("value7"));

            TestDefaultHeaders expected = NewInstance();
            expected.Add(Of("name1"), Of("value5"));
            expected.Add(Of("name2"), Of("value6"));
            expected.Add(Of("name1"), Of("value7"));
            expected.Add(Of("name3"), Of("value4"));

            h1.SetAll(h2);

            Assert.True(expected.Equals(h1));
        }

        [Fact]
        public void HeadersWithSameNamesAndValuesShouldBeEquivalent()
        {
            TestDefaultHeaders headers1 = NewInstance();
            headers1.Add(Of("name1"), Of("value1"));
            headers1.Add(Of("name2"), Of("value2"));
            headers1.Add(Of("name2"), Of("value3"));

            TestDefaultHeaders headers2 = NewInstance();
            headers2.Add(Of("name1"), Of("value1"));
            headers2.Add(Of("name2"), Of("value2"));
            headers2.Add(Of("name2"), Of("value3"));

            Assert.True(headers1.Equals(headers2));
            Assert.True(headers2.Equals(headers1));
            Assert.Equal(headers1.GetHashCode(), headers2.GetHashCode());
            Assert.Equal(headers1.GetHashCode(), headers1.GetHashCode());
            Assert.Equal(headers2.GetHashCode(), headers2.GetHashCode());
        }

        [Fact]
        public void EmptyHeadersShouldBeEqual()
        {
            TestDefaultHeaders headers1 = NewInstance();
            TestDefaultHeaders headers2 = NewInstance();
            Assert.NotSame(headers1, headers2);
            Assert.True(headers1.Equals(headers2));
            Assert.Equal(headers1.GetHashCode(), headers2.GetHashCode());
        }

        [Fact]
        public void HeadersWithSameNamesButDifferentValuesShouldNotBeEquivalent()
        {
            TestDefaultHeaders headers1 = NewInstance();
            headers1.Add(Of("name1"), Of("value1"));
            TestDefaultHeaders headers2 = NewInstance();
            headers1.Add(Of("name1"), Of("value2"));
            Assert.False(headers1.Equals(headers2));
        }

        [Fact]
        public void SubsetOfHeadersShouldNotBeEquivalent()
        {
            TestDefaultHeaders headers1 = NewInstance();
            headers1.Add(Of("name1"), Of("value1"));
            headers1.Add(Of("name2"), Of("value2"));
            TestDefaultHeaders headers2 = NewInstance();
            headers1.Add(Of("name1"), Of("value1"));
            Assert.False(headers1.Equals(headers2));
        }

        [Fact]
        public void HeadersWithDifferentNamesAndValuesShouldNotBeEquivalent()
        {
            TestDefaultHeaders h1 = NewInstance();
            h1.Set(Of("name1"), Of("value1"));
            TestDefaultHeaders h2 = NewInstance();
            h2.Set(Of("name2"), Of("value2"));
            Assert.False(h1.Equals(h2));
            Assert.False(h2.Equals(h1));
        }

        [Fact]
        public void IterateEmptyHeaders()
        {
            TestDefaultHeaders headers = NewInstance();
            var list = new List<HeaderEntry<ICharSequence, ICharSequence>>(headers);
            Assert.Empty(list);
        }

        [Fact]
        public void IteratorShouldReturnAllNameValuePairs()
        {
            TestDefaultHeaders headers1 = NewInstance();
            headers1.Add(Of("name1"), new[] { Of("value1"), Of("value2") });
            headers1.Add(Of("name2"), Of("value3"));
            headers1.Add(Of("name3"), new[] { Of("value4"), Of("value5"), Of("value6") });
            headers1.Add(Of("name1"), new[] { Of("value7"), Of("value8") });
            Assert.Equal(8, headers1.Size);

            TestDefaultHeaders headers2 = NewInstance();
            foreach (HeaderEntry<ICharSequence, ICharSequence> entry in headers1)
            {
                headers2.Add(entry.Key, entry.Value);
            }

            Assert.True(headers1.Equals(headers2));
        }

        [Fact]
        public void IteratorSetValueShouldChangeHeaderValue()
        {
            TestDefaultHeaders headers = NewInstance();
            headers.Add(Of("name1"), new[] { Of("value1"), Of("value2"), Of("value3")});
            headers.Add(Of("name2"), Of("value4"));
            Assert.Equal(4, headers.Size);

            foreach(HeaderEntry<ICharSequence, ICharSequence> header in headers)
            {
                if (Of("name1").Equals(header.Key) && Of("value2").Equals(header.Value))
                {
                    header.SetValue(Of("updatedvalue2"));
                    Assert.Equal(Of("updatedvalue2"), header.Value);
                }
                if (Of("name1").Equals(header.Key) && Of("value3").Equals(header.Value))
                {
                    header.SetValue(Of("updatedvalue3"));
                    Assert.Equal(Of("updatedvalue3"), header.Value);
                }
            }

            Assert.Equal(4, headers.Size);
            Assert.True(headers.Contains(Of("name1"), Of("updatedvalue2")));
            Assert.False(headers.Contains(Of("name1"), Of("value2")));
            Assert.True(headers.Contains(Of("name1"), Of("updatedvalue3")));
            Assert.False(headers.Contains(Of("name1"), Of("value3")));
        }

        [Fact]
        public void EntryEquals()
        {
            IHeaders<ICharSequence, ICharSequence> same1 = NewInstance().Add(Of("name"), Of("value"));
            IHeaders<ICharSequence, ICharSequence> same2 = NewInstance().Add(Of("name"), Of("value"));
            Assert.True(same1.Equals(same2));
            Assert.Equal(same1.GetHashCode(), same2.GetHashCode());

            IHeaders<ICharSequence, ICharSequence> nameDifferent1 = NewInstance().Add(Of("name1"), Of("value"));
            IHeaders<ICharSequence, ICharSequence> nameDifferent2 = NewInstance().Add(Of("name2"), Of("value"));
            Assert.False(nameDifferent1.Equals(nameDifferent2));
            Assert.NotEqual(nameDifferent1.GetHashCode(), nameDifferent2.GetHashCode());

            IHeaders<ICharSequence, ICharSequence> valueDifferent1 = NewInstance().Add(Of("name"), Of("value1"));
            IHeaders<ICharSequence, ICharSequence> valueDifferent2 = NewInstance().Add(Of("name"), Of("value2"));
            Assert.False(valueDifferent1.Equals(valueDifferent2));
            Assert.NotEqual(valueDifferent1.GetHashCode(), valueDifferent2.GetHashCode());
        }

        [Fact]
        public void GetAllReturnsEmptyListForUnknownName()
        {
            TestDefaultHeaders headers = NewInstance();
            Assert.Equal(0, headers.GetAll(Of("noname")).Count);
        }

        [Fact]
        public void SetHeadersShouldClearAndOverwrite()
        {
            TestDefaultHeaders headers1 = NewInstance();
            headers1.Add(Of("name"), Of("value"));

            TestDefaultHeaders headers2 = NewInstance();
            headers2.Add(Of("name"), Of("newvalue"));
            headers2.Add(Of("name1"), Of("value1"));

            headers1.Set(headers2);
            Assert.True(headers1.Equals(headers2));
        }

        [Fact]
        public void SetAllHeadersShouldOnlyOverwriteHeaders()
        {
            TestDefaultHeaders headers1 = NewInstance();
            headers1.Add(Of("name"), Of("value"));
            headers1.Add(Of("name1"), Of("value1"));

            TestDefaultHeaders headers2 = NewInstance();
            headers2.Add(Of("name"), Of("newvalue"));
            headers2.Add(Of("name2"), Of("value2"));

            TestDefaultHeaders expected = NewInstance();
            expected.Add(Of("name"), Of("newvalue"));
            expected.Add(Of("name1"), Of("value1"));
            expected.Add(Of("name2"), Of("value2"));

            headers1.SetAll(headers2);
            Assert.True(headers1.Equals(expected));
        }

        [Fact]
        public void AddSelf()
        {
            TestDefaultHeaders headers = NewInstance();
            Assert.Throws<ArgumentException>(() => headers.Add(headers));
        }

        [Fact]
        public void SetSelfIsNoOp()
        {
            TestDefaultHeaders headers = NewInstance();
            headers.Add(Of("name"), Of("value"));
            headers.Set(headers);
            Assert.Equal(1, headers.Size);
        }

        [Fact]
        public void ConvertToString()
        {
            TestDefaultHeaders headers = NewInstance();
            headers.Add(Of("name1"), Of("value1"));
            headers.Add(Of("name1"), Of("value2"));
            headers.Add(Of("name2"), Of("value3"));
            Assert.Equal("TestDefaultHeaders[name1: value1, name1: value2, name2: value3]", headers.ToString());

            headers = NewInstance();
            headers.Add(Of("name1"), Of("value1"));
            headers.Add(Of("name2"), Of("value2"));
            headers.Add(Of("name3"), Of("value3"));
            Assert.Equal("TestDefaultHeaders[name1: value1, name2: value2, name3: value3]", headers.ToString());

            headers = NewInstance();
            headers.Add(Of("name1"), Of("value1"));
            Assert.Equal("TestDefaultHeaders[name1: value1]", headers.ToString());

            headers = NewInstance();
            Assert.Equal("TestDefaultHeaders[]", headers.ToString());
        }

        sealed class ThrowConverter : IValueConverter<ICharSequence>
        {
            public ICharSequence ConvertObject(object value) => throw new ArgumentException();

            public ICharSequence ConvertBoolean(bool value) => throw new ArgumentException();

            public bool ConvertToBoolean(ICharSequence value) => throw new ArgumentException();

            public ICharSequence ConvertByte(byte value) => throw new ArgumentException();

            public byte ConvertToByte(ICharSequence value) => throw new ArgumentException();

            public ICharSequence ConvertChar(char value) => throw new ArgumentException();

            public char ConvertToChar(ICharSequence value) => throw new ArgumentException();

            public ICharSequence ConvertShort(short value) => throw new ArgumentException();

            public short ConvertToShort(ICharSequence value) => throw new ArgumentException();

            public ICharSequence ConvertInt(int value) => throw new ArgumentException();

            public int ConvertToInt(ICharSequence value) => throw new ArgumentException();

            public ICharSequence ConvertLong(long value) => throw new ArgumentException();

            public long ConvertToLong(ICharSequence value) => throw new ArgumentException();

            public ICharSequence ConvertTimeMillis(long value) => throw new ArgumentException();

            public long ConvertToTimeMillis(ICharSequence value) => throw new ArgumentException();

            public ICharSequence ConvertFloat(float value) => throw new ArgumentException();

            public float ConvertToFloat(ICharSequence value) => throw new ArgumentException();

            public ICharSequence ConvertDouble(double value) => throw new ArgumentException();

            public double ConvertToDouble(ICharSequence value) => throw new ArgumentException();
        }

        [Fact]
        public void NotThrowWhenConvertFails()
        {
            var headers = new TestDefaultHeaders(new ThrowConverter());

            headers.Set(Of("name1"), Of(""));
            Assert.False(headers.TryGetInt(Of("name1"), out int _));
            Assert.Equal(1, headers.GetInt(Of("name1"), 1));

            Assert.False(headers.TryGetBoolean(Of(""), out bool _));
            Assert.False(headers.GetBoolean(Of("name1"), false));

            Assert.False(headers.TryGetByte(Of("name1"), out byte _));
            Assert.Equal(1, headers.GetByte(Of("name1"), 1));

            Assert.False(headers.TryGetChar(Of("name"), out char _));
            Assert.Equal('n', headers.GetChar(Of("name1"), 'n'));

            Assert.False(headers.TryGetDouble(Of("name"), out double _));
            Assert.Equal(1, headers.GetDouble(Of("name1"), 1), 0);

            Assert.False(headers.TryGetFloat(Of("name"), out float _));
            Assert.Equal(float.MaxValue, headers.GetFloat(Of("name1"), float.MaxValue), 0);

            Assert.False(headers.TryGetLong(Of("name"), out long _));
            Assert.Equal(long.MaxValue, headers.GetLong(Of("name1"), long.MaxValue));

            Assert.False(headers.TryGetShort(Of("name"), out short _));
            Assert.Equal(short.MaxValue, headers.GetShort(Of("name1"), short.MaxValue));

            Assert.False(headers.TryGetTimeMillis(Of("name"), out long _));
            Assert.Equal(long.MaxValue, headers.GetTimeMillis(Of("name1"), long.MaxValue));
        }

        [Fact]
        public void GetBooleanInvalidValue()
        {
            TestDefaultHeaders headers = NewInstance();
            headers.Set(Of("name1"), new StringCharSequence("invalid"));
            headers.Set(Of("name2"), new AsciiString("invalid"));
            headers.Set(Of("name3"), new StringBuilderCharSequence(new StringBuilder("invalid")));

            Assert.False(headers.GetBoolean(Of("name1"), false));
            Assert.False(headers.GetBoolean(Of("name2"), false));
            Assert.False(headers.GetBoolean(Of("name3"), false));
        }

        [Fact]
        public void GetBooleanFalseValue()
        {
            TestDefaultHeaders headers = NewInstance();
            headers.Set(Of("name1"), new StringCharSequence("false"));
            headers.Set(Of("name2"), new AsciiString("false"));
            headers.Set(Of("name3"), new StringBuilderCharSequence(new StringBuilder("false")));

            Assert.False(headers.GetBoolean(Of("name1"), true));
            Assert.False(headers.GetBoolean(Of("name2"), true));
            Assert.False(headers.GetBoolean(Of("name3"), true));
        }

        [Fact]
        public void GetBooleanTrueValue()
        {
            TestDefaultHeaders headers = NewInstance();
            headers.Set(Of("name1"), new StringCharSequence("true"));
            headers.Set(Of("name2"), new AsciiString("true"));
            headers.Set(Of("name3"), new StringBuilderCharSequence(new StringBuilder("true")));

            Assert.True(headers.GetBoolean(Of("name1"), false));
            Assert.True(headers.GetBoolean(Of("name2"), false));
            Assert.True(headers.GetBoolean(Of("name3"), false));
        }
    }
}
