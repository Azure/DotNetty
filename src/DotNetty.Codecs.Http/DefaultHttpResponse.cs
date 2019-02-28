// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace DotNetty.Codecs.Http
{
    using System.Diagnostics.Contracts;
    using System.Text;

    public class DefaultHttpResponse : DefaultHttpMessage, IHttpResponse
    {
        HttpResponseStatus status;

        public DefaultHttpResponse(HttpVersion version, HttpResponseStatus status, bool validateHeaders = true, bool singleFieldHeaders = false)
            : base(version, validateHeaders, singleFieldHeaders)
        {
            Contract.Requires(status != null);

            this.status = status;
        }

        public DefaultHttpResponse(HttpVersion version, HttpResponseStatus status, HttpHeaders headers) 
            : base(version, headers)
        {
            Contract.Requires(status != null);

            this.status = status;
        }

        public HttpResponseStatus Status => this.status;

        public IHttpResponse SetStatus(HttpResponseStatus value)
        {
            Contract.Requires(value != null);
            this.status = value;
            return this;
        }

        public override string ToString() => HttpMessageUtil.AppendResponse(new StringBuilder(256), this).ToString();
    }
}
