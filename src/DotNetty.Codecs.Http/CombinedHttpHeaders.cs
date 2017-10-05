// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using DotNetty.Common.Utilities;

    using static Common.Utilities.StringUtil;

    public sealed class CombinedHttpHeaders : DefaultHttpHeaders
    {
        public CombinedHttpHeaders(bool validate) 
            : base(new CombinedHttpHeadersImpl(AsciiString.CaseSensitiveHasher, ValueConverter(validate), NameValidator(validate)))
        {
        }

        public override bool ContainsValue(AsciiString name, ICharSequence value, bool ignoreCase) => 
            base.ContainsValue(name, TrimOws(value), ignoreCase);

        sealed class CombinedHttpHeadersImpl : DefaultHeaders<AsciiString, ICharSequence>
        {
            // An estimate of the size of a header value.
            const int ValueLengthEstimate = 10;

            public CombinedHttpHeadersImpl(IHashingStrategy<AsciiString> nameHashingStrategy, 
                IValueConverter<ICharSequence> valueConverter, INameValidator<ICharSequence> nameValidator)
                : base(nameHashingStrategy, valueConverter, nameValidator)
            {
            }

            public override IEnumerable<ICharSequence> ValueIterator(AsciiString name)
            {
                ICharSequence value = null;
                foreach (ICharSequence v in base.ValueIterator(name))
                {
                    if (value != null)
                    {
                        throw new InvalidOperationException($"{nameof(CombinedHttpHeaders)} should only have one value");
                    }
                    value = v;
                }
                return value != null ? UnescapeCsvFields(value) : Enumerable.Empty<ICharSequence>();
            }

            public override IList<ICharSequence> GetAll(AsciiString name)
            {
                IList<ICharSequence> values = base.GetAll(name);
                if (values.Count == 0)
                {
                    return values;
                }
                if (values.Count != 1)
                {
                    throw new InvalidOperationException($"{nameof(CombinedHttpHeaders)} should only have one value");
                }

                return UnescapeCsvFields(values[0]);
            }

            public override IHeaders<AsciiString, ICharSequence> Add(IHeaders<AsciiString, ICharSequence> headers)
            {
                // Override the fast-copy mechanism used by DefaultHeaders
                if (ReferenceEquals(headers, this))
                {
                    throw new ArgumentException("can't add to itself.");
                }

                if (headers is CombinedHttpHeadersImpl)
                {
                    if (this.IsEmpty)
                    {
                        // Can use the fast underlying copy
                        this.AddImpl(headers);
                    }
                    else
                    {
                        // Values are already escaped so don't escape again
                        foreach (HeaderEntry<AsciiString, ICharSequence> header in headers)
                        {
                            this.AddEscapedValue(header.Key, header.Value);
                        }
                    }
                }
                else
                {
                    foreach (HeaderEntry<AsciiString, ICharSequence> header in headers)
                    {
                        this.Add(header.Key, header.Value);
                    }
                }

                return this;
            }

            public override IHeaders<AsciiString, ICharSequence> Set(IHeaders<AsciiString, ICharSequence> headers)
            {
                if (ReferenceEquals(headers, this))
                {
                    return this;
                }
                this.Clear();
                return this.Add(headers);
            }

            public override IHeaders<AsciiString, ICharSequence> SetAll(IHeaders<AsciiString, ICharSequence> headers)
            {
                if (ReferenceEquals(headers, this))
                {
                    return this;
                }
                foreach (AsciiString key in headers.Names())
                {
                    this.Remove(key);
                }
                return this.Add(headers);
            }

            public override IHeaders<AsciiString, ICharSequence> Add(AsciiString name, ICharSequence value) => 
                this.AddEscapedValue(name, EscapeCsv(value));

            public override IHeaders<AsciiString, ICharSequence> Add(AsciiString name, IEnumerable<ICharSequence> values) => 
                this.AddEscapedValue(name, CommaSeparate(values));

            public override IHeaders<AsciiString, ICharSequence> AddObject(AsciiString name, object value) => 
                this.AddEscapedValue(name, EscapeCsv(this.ValueConverter.ConvertObject(value)));

            public override IHeaders<AsciiString, ICharSequence> AddObject(AsciiString name, IEnumerable<object> values) => 
                this.AddEscapedValue(name, this.CommaSeparate(values));

            public override IHeaders<AsciiString, ICharSequence> AddObject(AsciiString name, params object[] values) => 
                this.AddEscapedValue(name, this.CommaSeparate(values));

            public override IHeaders<AsciiString, ICharSequence> Set(AsciiString name, IEnumerable<ICharSequence> values)
            {
                base.Set(name, CommaSeparate(values));
                return this;
            }

            public override IHeaders<AsciiString, ICharSequence> SetObject(AsciiString name, object value)
            {
                ICharSequence charSequence = EscapeCsv(this.ValueConverter.ConvertObject(value));
                base.Set(name, charSequence);
                return this;
            }

            public override IHeaders<AsciiString, ICharSequence> SetObject(AsciiString name, IEnumerable<object> values)
            {
                base.Set(name, this.CommaSeparate(values));
                return this;
            }

            CombinedHttpHeadersImpl AddEscapedValue(AsciiString name, ICharSequence escapedValue)
            {
                if (!this.TryGet(name, out ICharSequence currentValue))
                {
                    base.Add(name, escapedValue);
                }
                else
                {
                    base.Set(name, CommaSeparateEscapedValues(currentValue, escapedValue));
                }

                return this;
            }

            ICharSequence CommaSeparate(IEnumerable<object> values)
            {
                StringBuilderCharSequence sb = values is ICollection collection
                    ? new StringBuilderCharSequence(collection.Count * ValueLengthEstimate)
                    : new StringBuilderCharSequence();

                foreach (object value in values)
                {
                    if (sb.Count > 0)
                    {
                        sb.Append(Comma);
                    }

                    sb.Append(EscapeCsv(this.ValueConverter.ConvertObject(value)));
                }

                return sb;
            }

            static ICharSequence CommaSeparate(IEnumerable<ICharSequence> values)
            {
                StringBuilderCharSequence sb = values is ICollection collection
                    ? new StringBuilderCharSequence(collection.Count * ValueLengthEstimate)
                    : new StringBuilderCharSequence();

                foreach (ICharSequence value in values)
                {
                    if (sb.Count > 0)
                    {
                        sb.Append(Comma);
                    }

                    sb.Append(EscapeCsv(value));
                }

                return sb;
            }

            static ICharSequence CommaSeparateEscapedValues(ICharSequence currentValue, ICharSequence value)
            {
                var builder = new StringBuilderCharSequence(currentValue.Count + 1 + value.Count);
                builder.Append(currentValue);
                builder.Append(Comma);
                builder.Append(value);

                return builder;
            }

            static ICharSequence EscapeCsv(ICharSequence value) => StringUtil.EscapeCsv(value, true);
        }
    }
}
