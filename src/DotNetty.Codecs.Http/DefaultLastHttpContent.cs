// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http
{
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    public class DefaultLastHttpContent : DefaultHttpContent, ILastHttpContent
    {
        readonly HttpHeaders trailingHeaders;
        readonly bool validateHeaders;

        public DefaultLastHttpContent() : this(Unpooled.Buffer(0), true)
        {
        }

        public DefaultLastHttpContent(IByteBuffer content) : this(content, true)
        {
        }

        public DefaultLastHttpContent(IByteBuffer content, bool validateHeaders)
            : base(content)
        {
            this.trailingHeaders = new TrailingHttpHeaders(validateHeaders);
            this.validateHeaders = validateHeaders;
        }

        public HttpHeaders TrailingHeaders => this.trailingHeaders;

        public override IByteBufferHolder Replace(IByteBuffer buffer)
        {
            var dup = new DefaultLastHttpContent(this.Content, this.validateHeaders);
            dup.TrailingHeaders.Set(this.trailingHeaders);
            return dup;
        }

        public override string ToString()
        {
            var buf = new StringBuilder(base.ToString());
            buf.Append(StringUtil.Newline);
            this.AppendHeaders(buf);

            // Remove the last newline.
            buf.Length = buf.Length - StringUtil.Newline.Length;
            return buf.ToString();
        }

        void AppendHeaders(StringBuilder buf)
        {
            foreach (HeaderEntry<AsciiString, ICharSequence> e in this.trailingHeaders)
            {
                buf.Append($"{e.Key}: {e.Value}{StringUtil.Newline}");
            }
        }

        sealed class TrailerNameValidator : INameValidator<ICharSequence>
        {
            public void ValidateName(ICharSequence name)
            {
                DefaultHttpHeaders.HttpNameValidator.ValidateName(name);
                if (HttpHeaderNames.ContentLength.ContentEqualsIgnoreCase(name)
                    || HttpHeaderNames.TransferEncoding.ContentEqualsIgnoreCase(name)
                    || HttpHeaderNames.Trailer.ContentEqualsIgnoreCase(name))
                {
                    ThrowHelper.ThrowArgumentException_TrailingHeaderName(name);
                }
            }
        }

        sealed class TrailingHttpHeaders : DefaultHttpHeaders
        {
            static readonly TrailerNameValidator TrailerNameValidator = new TrailerNameValidator();

            public TrailingHttpHeaders(bool validate) 
                : base(validate, validate ? TrailerNameValidator : NotNullValidator)
            {
            }
        }
    }
}
