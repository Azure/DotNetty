// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable InconsistentNaming
namespace DotNetty.Codecs.Http.Cors
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.Contracts;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;

    public sealed class CorsConfigBuilder
    {
        public static CorsConfigBuilder ForAnyOrigin() => new CorsConfigBuilder();

        public static CorsConfigBuilder ForOrigin(ICharSequence origin)
        {
            return CorsHandler.AnyOrigin.ContentEquals(origin)  // * AnyOrigin
                ? new CorsConfigBuilder() 
                : new CorsConfigBuilder(origin);
        }

        public static CorsConfigBuilder ForOrigins(params ICharSequence[] origins) => new CorsConfigBuilder(origins);

        internal readonly ISet<ICharSequence> origins;
        internal readonly bool anyOrigin;
        internal bool allowNullOrigin;
        internal bool enabled = true;
        internal bool allowCredentials;
        internal readonly HashSet<ICharSequence> exposeHeaders = new HashSet<ICharSequence>(AsciiString.CaseSensitiveHasher);
        internal long maxAge;
        internal readonly ISet<HttpMethod> requestMethods = new HashSet<HttpMethod>();
        internal readonly ISet<AsciiString> requestHeaders = new HashSet<AsciiString>();
        internal readonly Dictionary<AsciiString, ICallable<object>> preflightHeaders = new Dictionary<AsciiString, ICallable<object>>();
        internal bool noPreflightHeaders;
        internal bool shortCircuit;

        CorsConfigBuilder(params ICharSequence[] origins)
        {
            this.origins = new HashSet<ICharSequence>(origins);
            this.anyOrigin = false;
        }

        CorsConfigBuilder()
        {
            this.anyOrigin = true;
            this.origins = ImmutableHashSet<ICharSequence>.Empty;
        }

        public CorsConfigBuilder AllowNullOrigin()
        {
            this.allowNullOrigin = true;
            return this;
        }

        public CorsConfigBuilder Disable()
        {
            this.enabled = false;
            return this;
        }

        public CorsConfigBuilder ExposeHeaders(params ICharSequence[] headers)
        {
            foreach (ICharSequence header in headers)
            {
                this.exposeHeaders.Add(header);
            }
            return this;
        }

        public CorsConfigBuilder ExposeHeaders(params string[] headers)
        {
            foreach (string header in headers)
            {
                this.exposeHeaders.Add(new StringCharSequence(header));
            }
            return this;
        }

        public CorsConfigBuilder AllowCredentials()
        {
            this.allowCredentials = true;
            return this;
        }

        public CorsConfigBuilder MaxAge(long max)
        {
            this.maxAge = max;
            return this;
        }

        public CorsConfigBuilder AllowedRequestMethods(params HttpMethod[] methods)
        {
            this.requestMethods.UnionWith(methods);
            return this;
        }

        public CorsConfigBuilder AllowedRequestHeaders(params AsciiString[] headers)
        {
            this.requestHeaders.UnionWith(headers);
            return this;
        }

        public CorsConfigBuilder AllowedRequestHeaders(params ICharSequence[] headers)
        {
            foreach (ICharSequence header in headers)
            {
                this.requestHeaders.Add(new AsciiString(header));
            }
            return this;
        }

        public CorsConfigBuilder PreflightResponseHeader(AsciiString name, params object[] values)
        {
            Contract.Requires(values != null);

            if (values.Length == 1)
            {
                this.preflightHeaders.Add(name, new ConstantValueGenerator(values[0]));
            }
            else
            {
                this.PreflightResponseHeader(name, new List<object>(values));
            }
            return this;
        }

        public CorsConfigBuilder PreflightResponseHeader(AsciiString name, ICollection<object> value)
        {
            this.preflightHeaders.Add(name, new ConstantValueGenerator(value));
            return this;
        }

        public CorsConfigBuilder PreflightResponseHeader(AsciiString name, ICallable<object> valueGenerator)
        {
            this.preflightHeaders.Add(name, valueGenerator);
            return this;
        }

        public CorsConfigBuilder NoPreflightResponseHeaders()
        {
            this.noPreflightHeaders = true;
            return this;
        }

        public CorsConfigBuilder ShortCircuit()
        {
            this.shortCircuit = true;
            return this;
        }

        public CorsConfig Build()
        {
            if (this.preflightHeaders.Count == 0 && !this.noPreflightHeaders)
            {
                this.preflightHeaders.Add(HttpHeaderNames.Date, DateValueGenerator.Default);
                this.preflightHeaders.Add(HttpHeaderNames.ContentLength, new ConstantValueGenerator(new AsciiString("0")));
            }
            return new CorsConfig(this);
        }

        //  This class is used for preflight HTTP response values that do not need to be
        //  generated, but instead the value is "static" in that the same value will be returned
        //  for each call.
        sealed class ConstantValueGenerator : ICallable<object>
        {
            readonly object value;

            internal ConstantValueGenerator(object value)
            {
                Contract.Requires(value != null);
                this.value = value;
            }

            public object Call() => this.value;
        }

        // This callable is used for the DATE preflight HTTP response HTTP header.
        // It's value must be generated when the response is generated, hence will be
        // different for every call.
        sealed class DateValueGenerator : ICallable<object>
        {
            internal static readonly DateValueGenerator Default = new DateValueGenerator();

            public object Call() => new DateTime();
        }
    }
}

