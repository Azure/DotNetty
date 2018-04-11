// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace DotNetty.Codecs.Http
{
    using System.Diagnostics.Contracts;

    public abstract class DefaultHttpMessage : DefaultHttpObject, IHttpMessage
    {
        const int HashCodePrime = 31;
        HttpVersion version;
        readonly HttpHeaders headers;

        protected DefaultHttpMessage(HttpVersion version) : this(version, true, false)
        {
        }

        protected DefaultHttpMessage(HttpVersion version, bool validateHeaders, bool singleFieldHeaders)
        {
            Contract.Requires(version != null);

            this.version = version;
            this.headers = singleFieldHeaders 
                ? new CombinedHttpHeaders(validateHeaders) 
                : new DefaultHttpHeaders(validateHeaders);
        }

        protected DefaultHttpMessage(HttpVersion version, HttpHeaders headers)
        {
            Contract.Requires(version != null);
            Contract.Requires(headers != null);

            this.version = version;
            this.headers = headers;
        }

        public HttpHeaders Headers => this.headers;

        public HttpVersion ProtocolVersion => this.version;

        public override int GetHashCode()
        {
            int result = 1;
            result = HashCodePrime * result + this.headers.GetHashCode();
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            result = HashCodePrime * result + this.version.GetHashCode();
            result = HashCodePrime * result + base.GetHashCode();
            return result;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is DefaultHttpMessage other))
            {
                return false;
            }

            return this.headers.Equals(other.headers)
                && this.version.Equals(other.version)
                && base.Equals(obj);
        }

        public IHttpMessage SetProtocolVersion(HttpVersion value)
        {
            Contract.Requires(value != null);
            this.version = value;
            return this;
        }
    }
}
