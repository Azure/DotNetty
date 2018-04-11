// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.Cors
{
    using System.Collections.Generic;
    using DotNetty.Codecs.Http.Cors;
    using DotNetty.Common.Utilities;
    using Xunit;

    public sealed class CorsConfigTest
    {
        [Fact]
        public void Disabled()
        {
            CorsConfig cors = CorsConfigBuilder.ForAnyOrigin().Disable().Build();
            Assert.False(cors.IsCorsSupportEnabled);
        }

        [Fact]
        public void AnyOrigin()
        {
            CorsConfig cors = CorsConfigBuilder.ForAnyOrigin().Build();
            Assert.True(cors.IsAnyOriginSupported);
            Assert.Equal("*", cors.Origin.ToString());
            Assert.Equal(0, cors.Origins.Count);
        }

        [Fact]
        public void WildcardOrigin()
        {
            CorsConfig cors = CorsConfigBuilder.ForOrigin(CorsHandler.AnyOrigin).Build();
            Assert.True(cors.IsAnyOriginSupported);
            Assert.Equal("*", cors.Origin.ToString());
            Assert.Equal(0, cors.Origins.Count);
        }

        [Fact]
        public void Origin()
        {
            CorsConfig cors = CorsConfigBuilder.ForOrigin((StringCharSequence)"http://localhost:7888").Build();
            Assert.Equal("http://localhost:7888", cors.Origin.ToString());
            Assert.False(cors.IsAnyOriginSupported);
        }

        [Fact]
        public void Origins()
        {
            ICharSequence[] origins = { (StringCharSequence)"http://localhost:7888", (StringCharSequence)"https://localhost:7888"};
            CorsConfig cors = CorsConfigBuilder.ForOrigins(origins).Build();
            Assert.Equal(2, cors.Origins.Count);
            Assert.True(cors.Origins.Contains(origins[0]));
            Assert.True(cors.Origins.Contains(origins[1]));
            Assert.False(cors.IsAnyOriginSupported);
        }

        [Fact]
        public void ExposeHeaders()
        {
            CorsConfig cors = CorsConfigBuilder.ForAnyOrigin()
                .ExposeHeaders((StringCharSequence)"custom-header1", (StringCharSequence)"custom-header2").Build();
            Assert.True(cors.ExposedHeaders().Contains((StringCharSequence)"custom-header1"));
            Assert.True(cors.ExposedHeaders().Contains((StringCharSequence)"custom-header2"));
        }

        [Fact]
        public void AllowCredentials()
        {
            CorsConfig cors = CorsConfigBuilder.ForAnyOrigin().AllowCredentials().Build();
            Assert.True(cors.IsCredentialsAllowed);
        }

        [Fact]
        public void MaxAge()
        {
            CorsConfig cors = CorsConfigBuilder.ForAnyOrigin().MaxAge(3000).Build();
            Assert.Equal(3000, cors.MaxAge);
        }

        [Fact]
        public void RequestMethods()
        {
            CorsConfig cors = CorsConfigBuilder.ForAnyOrigin()
                .AllowedRequestMethods(HttpMethod.Post, HttpMethod.Get).Build();
            Assert.True(cors.AllowedRequestMethods().Contains(HttpMethod.Post));
            Assert.True(cors.AllowedRequestMethods().Contains(HttpMethod.Get));
        }

        [Fact]
        public void RequestHeaders()
        {
            CorsConfig cors = CorsConfigBuilder.ForAnyOrigin()
                .AllowedRequestHeaders((AsciiString)"preflight-header1", (AsciiString)"preflight-header2").Build();
            Assert.True(cors.AllowedRequestHeaders().Contains((AsciiString)"preflight-header1"));
            Assert.True(cors.AllowedRequestHeaders().Contains((AsciiString)"preflight-header2"));
        }

        [Fact]
        public void PreflightResponseHeadersSingleValue()
        {
            CorsConfig cors = CorsConfigBuilder.ForAnyOrigin()
                .PreflightResponseHeader((AsciiString)"SingleValue", (StringCharSequence)"value").Build();
            Assert.Equal((AsciiString)"value", cors.PreflightResponseHeaders().Get((AsciiString)"SingleValue", null));
        }

        [Fact]
        public void PreflightResponseHeadersMultipleValues()
        {
            CorsConfig cors = CorsConfigBuilder.ForAnyOrigin()
                .PreflightResponseHeader((AsciiString)"MultipleValues", (StringCharSequence)"value1", (StringCharSequence)"value2").Build();
            IList<ICharSequence> values = cors.PreflightResponseHeaders().GetAll((AsciiString)"MultipleValues");
            Assert.NotNull(values);
            Assert.True(values.Contains((AsciiString)"value1"));
            Assert.True(values.Contains((AsciiString)"value2"));
        }

        [Fact]
        public void DefaultPreflightResponseHeaders()
        {
            CorsConfig cors = CorsConfigBuilder.ForAnyOrigin().Build();
            Assert.NotNull(cors.PreflightResponseHeaders().Get(HttpHeaderNames.Date, null));
            Assert.Equal("0", cors.PreflightResponseHeaders().Get(HttpHeaderNames.ContentLength, null));
        }

        [Fact]
        public void EmptyPreflightResponseHeaders()
        {
            CorsConfig cors = CorsConfigBuilder.ForAnyOrigin().NoPreflightResponseHeaders().Build();
            Assert.Same(EmptyHttpHeaders.Default, cors.PreflightResponseHeaders());
        }

        [Fact]
        public void ShortCircuit()
        {
            CorsConfig cors = CorsConfigBuilder.ForOrigin((AsciiString)"http://localhost:8080")
                .ShortCircuit().Build();
            Assert.True(cors.IsShortCircuit);
        }
    }
}
