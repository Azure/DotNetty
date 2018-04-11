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

    public class DefaultFullHttpRequest : DefaultHttpRequest, IFullHttpRequest
    {
        readonly IByteBuffer content;
        readonly HttpHeaders trailingHeader;
        // Used to cache the value of the hash code and avoid {@link IllegalReferenceCountException}.
        int hash;

        public DefaultFullHttpRequest(HttpVersion httpVersion, HttpMethod method, string uri) 
            : this(httpVersion, method, uri, Unpooled.Buffer(0))
        {
        }

        public DefaultFullHttpRequest(HttpVersion httpVersion, HttpMethod method, string uri, IByteBuffer content)
            : this(httpVersion, method, uri, content, true)
        {
        }

        public DefaultFullHttpRequest(HttpVersion httpVersion, HttpMethod method, string uri, bool validateHeaders)
            : this(httpVersion, method, uri, Unpooled.Buffer(0), validateHeaders)
        {
        }

        public DefaultFullHttpRequest(HttpVersion httpVersion, HttpMethod method, string uri, 
            IByteBuffer content, bool validateHeaders)
            : base(httpVersion, method, uri, validateHeaders)
        {
            Contract.Requires(content != null);

            this.content = content;
            this.trailingHeader = new DefaultHttpHeaders(validateHeaders);
        }

        public DefaultFullHttpRequest(HttpVersion httpVersion, HttpMethod method, string uri, 
            IByteBuffer content, HttpHeaders headers, HttpHeaders trailingHeader) 
            : base(httpVersion, method, uri, headers)
        {
            Contract.Requires(content != null);
            Contract.Requires(trailingHeader != null);

            this.content = content;
            this.trailingHeader = trailingHeader;
        }

        public HttpHeaders TrailingHeaders => this.trailingHeader;

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
            new DefaultFullHttpRequest(this.ProtocolVersion, this.Method, this.Uri, newContent, this.Headers, this.trailingHeader);

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
                hashCode = 31 * hashCode + this.trailingHeader.GetHashCode();
                hashCode = 31 * hashCode + base.GetHashCode();

                this.hash = hashCode;
            }
            // ReSharper restore NonReadonlyMemberInGetHashCode
            return hashCode;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is DefaultFullHttpRequest other))
            {
                return false;
            }
            return base.Equals(other) 
                && this.content.Equals(other.content) 
                && this.trailingHeader.Equals(other.trailingHeader);
        }

        public override string ToString() => HttpMessageUtil.AppendFullRequest(new StringBuilder(256), this).ToString();
    }
}
