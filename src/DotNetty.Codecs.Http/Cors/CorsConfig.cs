// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWhenPossible
namespace DotNetty.Codecs.Http.Cors
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;

    // Configuration for Cross-Origin Resource Sharing (CORS).
    public sealed class CorsConfig
    {
        readonly ISet<ICharSequence> origins;
        readonly bool anyOrigin;
        readonly bool enabled;
        readonly ISet<ICharSequence> exposeHeaders;
        readonly bool allowCredentials;
        readonly long maxAge;
        readonly ISet<HttpMethod> allowedRequestMethods;
        readonly ISet<AsciiString> allowedRequestHeaders;
        readonly bool allowNullOrigin;
        readonly IDictionary<AsciiString, ICallable<object>> preflightHeaders;
        readonly bool shortCircuit;

        internal CorsConfig(CorsConfigBuilder builder)
        {
            this.origins = new HashSet<ICharSequence>(builder.origins, AsciiString.CaseSensitiveHasher);
            this.anyOrigin = builder.anyOrigin;
            this.enabled = builder.enabled;
            this.exposeHeaders = builder.exposeHeaders;
            this.allowCredentials = builder.allowCredentials;
            this.maxAge = builder.maxAge;
            this.allowedRequestMethods = builder.requestMethods;
            this.allowedRequestHeaders = builder.requestHeaders;
            this.allowNullOrigin = builder.allowNullOrigin;
            this.preflightHeaders = builder.preflightHeaders;
            this.shortCircuit = builder.shortCircuit;
        }

        public bool IsCorsSupportEnabled => this.enabled;

        public bool IsAnyOriginSupported => this.anyOrigin;

        public ICharSequence Origin => this.origins.Count == 0 ? CorsHandler.AnyOrigin : this.origins.First();

        public ISet<ICharSequence> Origins => this.origins;

        public bool IsNullOriginAllowed => this.allowNullOrigin;

        public ISet<ICharSequence> ExposedHeaders() => this.exposeHeaders.ToImmutableHashSet();

        public bool IsCredentialsAllowed => this.allowCredentials;

        public long MaxAge => this.maxAge;

        public ISet<HttpMethod> AllowedRequestMethods() => this.allowedRequestMethods.ToImmutableHashSet();

        public ISet<AsciiString> AllowedRequestHeaders() => this.allowedRequestHeaders.ToImmutableHashSet();

        public HttpHeaders PreflightResponseHeaders()
        {
            if (this.preflightHeaders.Count == 0)
            {
                return EmptyHttpHeaders.Default;
            }
            HttpHeaders headers = new DefaultHttpHeaders();
            foreach (KeyValuePair<AsciiString, ICallable<object>> entry in this.preflightHeaders)
            {
                object value = GetValue(entry.Value);
                if (value is IEnumerable<object> values)
                {
                    headers.Add(entry.Key, values);
                }
                else
                {
                    headers.Add(entry.Key, value);
                }
            }
            return headers;
        }

        public bool IsShortCircuit => this.shortCircuit;

        static object GetValue(ICallable<object> callable)
        {
            try
            {
                return callable.Call();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Could not generate value for callable [{callable}]", exception);
            }
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append($"{StringUtil.SimpleClassName(this)}")
                .Append($"[enabled = {this.enabled}");

            builder.Append(", origins=");
            if (this.Origins.Count == 0)
            {
                builder.Append("*");
            }
            else
            {
                builder.Append("(");
                foreach (ICharSequence value in this.Origins)
                {
                    builder.Append($"'{value}'");
                }
                builder.Append(")");
            }

            builder.Append(", exposedHeaders=");
            if (this.exposeHeaders.Count == 0)
            {
                builder.Append("*");
            }
            else
            {
                builder.Append("(");
                foreach (ICharSequence value in this.exposeHeaders)
                {
                    builder.Append($"'{value}'");
                }
                builder.Append(")");
            }

            builder.Append($", isCredentialsAllowed={this.allowCredentials}");
            builder.Append($", maxAge={this.maxAge}");

            builder.Append(", allowedRequestMethods=");
            if (this.allowedRequestMethods.Count == 0)
            {
                builder.Append("*");
            }
            else
            {
                builder.Append("(");
                foreach (HttpMethod value in this.allowedRequestMethods)
                {
                    builder.Append($"'{value}'");
                }
                builder.Append(")");
            }

            builder.Append(", allowedRequestHeaders=");
            if (this.allowedRequestHeaders.Count == 0)
            {
                builder.Append("*");
            }
            else
            {
                builder.Append("(");
                foreach(AsciiString value in this.allowedRequestHeaders)
                {
                    builder.Append($"'{value}'");
                }
                builder.Append(")");
            }

            builder.Append(", preflightHeaders=");
            if (this.preflightHeaders.Count == 0)
            {
                builder.Append("*");
            }
            else
            {
                builder.Append("(");
                foreach (AsciiString value in this.preflightHeaders.Keys)
                {
                    builder.Append($"'{value}'");
                }
                builder.Append(")");
            }

            builder.Append("]");
            return builder.ToString();
        }
    }
}
