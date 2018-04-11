// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http
{
    using System.Diagnostics.Contracts;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common;

    public class DefaultFullHttpResponse : DefaultHttpResponse, IFullHttpResponse
    {
        readonly IByteBuffer content;
        readonly HttpHeaders trailingHeaders;

        // Used to cache the value of the hash code and avoid {@link IllegalReferenceCountException}.
        int hash;

        public DefaultFullHttpResponse(HttpVersion version, HttpResponseStatus status)
            : this(version, status, Unpooled.Buffer(0))
        {
        }

        public DefaultFullHttpResponse(HttpVersion version, HttpResponseStatus status, IByteBuffer content)
            : this(version, status, content, true)
        {
        }

        public DefaultFullHttpResponse(HttpVersion version, HttpResponseStatus status, bool validateHeaders)
            : this(version, status, Unpooled.Buffer(0), validateHeaders, false)
        {
        }

        public DefaultFullHttpResponse(HttpVersion version, HttpResponseStatus status, bool validateHeaders,
            bool singleFieldHeaders)
            : this(version, status, Unpooled.Buffer(0), validateHeaders, singleFieldHeaders)
        {
        }

        public DefaultFullHttpResponse(HttpVersion version, HttpResponseStatus status,
            IByteBuffer content, bool validateHeaders)
            : this(version, status, content, validateHeaders, false)
        {
        }

        public DefaultFullHttpResponse(HttpVersion version, HttpResponseStatus status, 
            IByteBuffer content, bool validateHeaders, bool singleFieldHeaders)
            : base(version, status, validateHeaders, singleFieldHeaders)
        {
            Contract.Requires(content != null);

            this.content = content;
            this.trailingHeaders = singleFieldHeaders 
                ? new CombinedHttpHeaders(validateHeaders)
                : new DefaultHttpHeaders(validateHeaders);
        }

        public DefaultFullHttpResponse(HttpVersion version, HttpResponseStatus status, IByteBuffer content, HttpHeaders headers, HttpHeaders trailingHeaders)
            : base(version, status, headers)
        {
            Contract.Requires(content != null);
            Contract.Requires(trailingHeaders != null);

            this.content = content;
            this.trailingHeaders = trailingHeaders;
        }

        public HttpHeaders TrailingHeaders => this.trailingHeaders;

        public IByteBuffer Content => this.content;

        public int ReferenceCount => this.content.ReferenceCount;

        public IReferenceCounted Retain()
        {
            this.content.Retain();
            return this;
        }

        public IReferenceCounted Retain(int increment)
        {
            this.content.Retain(increment);
            return this;
        }

        public IReferenceCounted Touch()
        {
            this.content.Touch();
            return this;
        }

        public IReferenceCounted Touch(object hint)
        {
            this.content.Touch(hint);
            return this;
        }

        public bool Release() => this.content.Release();

        public bool Release(int decrement) => this.content.Release(decrement);

        public IByteBufferHolder Copy() => this.Replace(this.content.Copy());

        public IByteBufferHolder Duplicate() => this.Replace(this.content.Duplicate());

        public IByteBufferHolder RetainedDuplicate() => this.Replace(this.content.RetainedDuplicate());

        public IByteBufferHolder Replace(IByteBuffer newContent) => 
            new DefaultFullHttpResponse(this.ProtocolVersion, this.Status, newContent, this.Headers, this.trailingHeaders);

        public override int GetHashCode()
        {
            // ReSharper disable NonReadonlyMemberInGetHashCode
            int hashCode = this.hash;
            if (hashCode == 0)
            {
                if (this.content.ReferenceCount != 0)
                {
                    try
                    {
                        hashCode = 31 + this.content.GetHashCode();
                    }
                    catch (IllegalReferenceCountException)
                    {
                        // Handle race condition between checking refCnt() == 0 and using the object.
                        hashCode = 31;
                    }
                }
                else
                {
                    hashCode = 31;
                }
                hashCode = 31 * hashCode + this.trailingHeaders.GetHashCode();
                hashCode = 31 * hashCode + base.GetHashCode();
                this.hash = hashCode;
            }
            // ReSharper restore NonReadonlyMemberInGetHashCode
            return hashCode;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is DefaultFullHttpResponse other))
            {
                return false;
            }
            return base.Equals(other) 
                && this.content.Equals(other.content) 
                && this.trailingHeaders.Equals(other.trailingHeaders);
        }

        public override string ToString() => HttpMessageUtil.AppendFullResponse(new StringBuilder(256), this).ToString();
    }
}
