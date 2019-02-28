// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using DotNetty.Codecs;
    using DotNetty.Common.Utilities;

    public class DefaultHttpHeaders : HttpHeaders
    {
        const int HighestInvalidValueCharMask = ~15;
        internal static readonly INameValidator<ICharSequence> HttpNameValidator = new HeaderNameValidator();
        internal static readonly INameValidator<ICharSequence> NotNullValidator = new NullNameValidator<ICharSequence>();

        sealed class NameProcessor : IByteProcessor
        {
            public bool Process(byte value)
            {
                ValidateHeaderNameElement(value);
                return true;
            }
        }

        sealed class HeaderNameValidator : INameValidator<ICharSequence>
        {
            static readonly NameProcessor ByteProcessor = new NameProcessor();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ValidateName(ICharSequence name)
            {
                if (name == null || name.Count == 0)
                {
                    ThrowHelper.ThrowArgumentException_HeaderName();
                }
                if (name is AsciiString asciiString)
                {
                    asciiString.ForEachByte(ByteProcessor);
                }
                else
                {
                    // Go through each character in the name
                    Debug.Assert(name != null);
                    // ReSharper disable once ForCanBeConvertedToForeach
                    // Avoid new enumerator instance
                    for (int index = 0; index < name.Count; ++index)
                    {
                        ValidateHeaderNameElement(name[index]);
                    }
                }
            }
        }

        readonly DefaultHeaders<AsciiString, ICharSequence> headers;

        public DefaultHttpHeaders() : this(true)
        {
        }

        public DefaultHttpHeaders(bool validate) : this(validate, NameValidator(validate))
        {
        }

        protected DefaultHttpHeaders(bool validate, INameValidator<ICharSequence> nameValidator) 
            : this(new DefaultHeaders<AsciiString, ICharSequence>(AsciiString.CaseInsensitiveHasher, 
                ValueConverter(validate), nameValidator))
        {
        }

        protected DefaultHttpHeaders(DefaultHeaders<AsciiString, ICharSequence> headers)
        {
            this.headers = headers;
        }

        public override HttpHeaders Add(HttpHeaders httpHeaders)
        {
            if (httpHeaders is DefaultHttpHeaders defaultHttpHeaders)
            {
                this.headers.Add(defaultHttpHeaders.headers);
                return this;
            }
            return base.Add(httpHeaders);
        }

        public override HttpHeaders Set(HttpHeaders httpHeaders)
        {
            if (httpHeaders is DefaultHttpHeaders defaultHttpHeaders)
            {
                this.headers.Set(defaultHttpHeaders.headers);
                return this;
            }
            return base.Set(httpHeaders);
        }

        public override HttpHeaders Add(AsciiString name, object value)
        {
            this.headers.AddObject(name, value);
            return this;
        }

        public override HttpHeaders AddInt(AsciiString name, int value)
        {
            this.headers.AddInt(name, value);
            return this;
        }

        public override HttpHeaders AddShort(AsciiString name, short value)
        {
            this.headers.AddShort(name, value);
            return this;
        }

        public override HttpHeaders Remove(AsciiString name)
        {
            this.headers.Remove(name);
            return this;
        }

        public override HttpHeaders Set(AsciiString name, object value)
        {
            this.headers.SetObject(name, value);
            return this;
        }

        public override HttpHeaders Set(AsciiString name, IEnumerable<object> values)
        {
            this.headers.SetObject(name, values);
            return this;
        }

        public override HttpHeaders SetInt(AsciiString name, int value)
        {
            this.headers.SetInt(name, value);
            return this;
        }

        public override HttpHeaders SetShort(AsciiString name, short value)
        {
            this.headers.SetShort(name, value);
            return this;
        }

        public override HttpHeaders Clear()
        {
            this.headers.Clear();
            return this;
        }

        public override bool TryGet(AsciiString name, out ICharSequence value) => this.headers.TryGet(name, out value);

        public override bool TryGetInt(AsciiString name, out int value) => this.headers.TryGetInt(name, out value);

        public override int GetInt(AsciiString name, int defaultValue) => this.headers.GetInt(name, defaultValue);

        public override bool TryGetShort(AsciiString name, out short value) => this.headers.TryGetShort(name, out value);

        public override short GetShort(AsciiString name, short defaultValue) => this.headers.GetShort(name, defaultValue);

        public override bool TryGetTimeMillis(AsciiString name, out long value) => this.headers.TryGetTimeMillis(name, out value);

        public override long GetTimeMillis(AsciiString name, long defaultValue) => this.headers.GetTimeMillis(name, defaultValue);

        public override IList<ICharSequence> GetAll(AsciiString name) => this.headers.GetAll(name);

        public override IEnumerable<ICharSequence> ValueCharSequenceIterator(AsciiString name) => this.headers.ValueIterator(name);

        public override IList<HeaderEntry<AsciiString, ICharSequence>> Entries()
        {
            if (this.IsEmpty)
            {
                return ImmutableList<HeaderEntry<AsciiString, ICharSequence>>.Empty;
            }
            var entriesConverted = new List<HeaderEntry<AsciiString, ICharSequence>>(this.headers.Size);
            foreach(HeaderEntry<AsciiString, ICharSequence> entry in this)
            {
                entriesConverted.Add(entry);
            }
            return entriesConverted;
        }

        public override IEnumerator<HeaderEntry<AsciiString, ICharSequence>> GetEnumerator() => this.headers.GetEnumerator();

        public override bool Contains(AsciiString name) => this.headers.Contains(name);

        public override bool IsEmpty => this.headers.IsEmpty;

        public override int Size => this.headers.Size;

        public override bool Contains(AsciiString name, ICharSequence value, bool ignoreCase) =>  
            this.headers.Contains(name, value, 
                ignoreCase ? AsciiString.CaseInsensitiveHasher : AsciiString.CaseSensitiveHasher);

        public override ISet<AsciiString> Names() => this.headers.Names();

        public override bool Equals(object obj) => obj is DefaultHttpHeaders other 
            && this.headers.Equals(other.headers, AsciiString.CaseSensitiveHasher);

        public override int GetHashCode() => this.headers.HashCode(AsciiString.CaseSensitiveHasher);

        public override HttpHeaders Copy() => new DefaultHttpHeaders(this.headers.Copy());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ValidateHeaderNameElement(byte value)
        {
            switch (value)
            {
                case 0x00:
                case 0x09: //'\t':
                case 0x0a: //'\n':
                case 0x0b:
                case 0x0c: //'\f':
                case 0x0d: //'\r':
                case 0x20: //' ':
                case 0x2c: //',':
                case 0x3a: //':':
                case 0x3b: //';':
                case 0x3d: //'=':
                    ThrowHelper.ThrowArgumentException_HeaderValue(value);
                    break;
                default:
                    // Check to see if the character is not an ASCII character, or invalid
                    if (value > 127)
                    {
                        ThrowHelper.ThrowArgumentException_HeaderValueNonAscii(value);
                    }
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ValidateHeaderNameElement(char value)
        {
            switch (value)
            {
                case '\x00':
                case '\t':
                case '\n':
                case '\x0b':
                case '\f':
                case '\r':
                case ' ':
                case ',':
                case ':':
                case ';':
                case '=':
                    ThrowHelper.ThrowArgumentException_HeaderValue(value);
                    break;
                default:
                    // Check to see if the character is not an ASCII character, or invalid
                    if (value > 127)
                    {
                        ThrowHelper.ThrowArgumentException_HeaderValueNonAscii(value);
                    }
                    break;
            }
        }

        protected static IValueConverter<ICharSequence> ValueConverter(bool validate) => 
            validate ? DefaultHeaderValueConverterAndValidator : DefaultHeaderValueConverter;

        protected static INameValidator<ICharSequence> NameValidator(bool validate) => 
            validate ? HttpNameValidator : NotNullValidator;

        static readonly HeaderValueConverter DefaultHeaderValueConverter = new HeaderValueConverter();

        class HeaderValueConverter : CharSequenceValueConverter
        {
            public override ICharSequence ConvertObject(object value)
            {
                if (value is ICharSequence seq)
                {
                    return seq;
                }
                if (value is DateTime time)
                {
                    return new StringCharSequence(DateFormatter.Format(time));
                }
                return new StringCharSequence(value.ToString());
            }
        }

        static readonly HeaderValueConverterAndValidator DefaultHeaderValueConverterAndValidator = new HeaderValueConverterAndValidator();

        sealed class HeaderValueConverterAndValidator : HeaderValueConverter
        {
            public override ICharSequence ConvertObject(object value)
            {
                ICharSequence seq = base.ConvertObject(value);
                int state = 0;
                // Start looping through each of the character
                // ReSharper disable once ForCanBeConvertedToForeach
                // Avoid enumerator allocation
                for (int index = 0; index < seq.Count; index++)
                {
                    state = ValidateValueChar(state, seq[index]);
                }

                if (state != 0)
                {
                    ThrowHelper.ThrowArgumentException_HeaderValueEnd(seq);
                }
                return seq;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static int ValidateValueChar(int state, char character)
            {
                // State:
                // 0: Previous character was neither CR nor LF
                // 1: The previous character was CR
                // 2: The previous character was LF
                if ((character & HighestInvalidValueCharMask) == 0)
                {
                    // Check the absolutely prohibited characters.
                    switch (character)
                    {
                        case '\x00': // NULL
                            ThrowHelper.ThrowArgumentException_HeaderValueNullChar();
                            break;
                        case '\x0b': // Vertical tab
                            ThrowHelper.ThrowArgumentException_HeaderValueVerticalTabChar();
                            break;
                        case '\f':
                            ThrowHelper.ThrowArgumentException_HeaderValueFormFeed();
                            break;
                    }
                }

                // Check the CRLF (HT | SP) pattern
                switch (state)
                {
                    case 0:
                        switch (character)
                        {
                            case '\r':
                                return 1;
                            case '\n':
                                return 2;
                        }
                        break;
                    case 1:
                        switch (character)
                        {
                            case '\n':
                                return 2;
                            default:
                                ThrowHelper.ThrowArgumentException_NewLineAfterLineFeed();
                                break;
                        }
                        break;
                    case 2:
                        switch (character)
                        {
                            case '\t':
                            case ' ':
                                return 0;
                            default:
                                ThrowHelper.ThrowArgumentException_TabAndSpaceAfterLineFeed();
                                break;
                        }
                        break;
                }

                return state;
            }
        }
    }
}
