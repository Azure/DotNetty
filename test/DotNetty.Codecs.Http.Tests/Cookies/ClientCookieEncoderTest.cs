// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.Cookies
{
    using System;
    using DotNetty.Codecs.Http.Cookies;
    using Xunit;

    public sealed class ClientCookieEncoderTest
    {
        [Fact]
        public void EncodingMultipleClientCookies()
        {
            const string C1 = "myCookie=myValue";
            const string C2 = "myCookie2=myValue2";
            const string C3 = "myCookie3=myValue3";
            ICookie cookie1 = new DefaultCookie("myCookie", "myValue")
            {
                Domain = ".adomainsomewhere",
                MaxAge = 50,
                Path = "/apathsomewhere",
                IsSecure = true
            };
            ICookie cookie2 = new DefaultCookie("myCookie2", "myValue2")
            {
                Domain = ".anotherdomainsomewhere",
                Path = "/anotherpathsomewhere",
                IsSecure = false
            };
            ICookie cookie3 = new DefaultCookie("myCookie3", "myValue3");
            string encodedCookie = ClientCookieEncoder.StrictEncoder.Encode(cookie1, cookie2, cookie3);

            // Cookies should be sorted into decreasing order of path length, as per RFC6265.
            // When no path is provided, we assume maximum path length (so cookie3 comes first).
            Assert.Equal(C3 + "; " + C2 + "; " + C1, encodedCookie);
        }

        [Fact]
        public void WrappedCookieValue()
        {
            string cookie = ClientCookieEncoder.StrictEncoder.Encode(new DefaultCookie("myCookie", "\"foo\""));
            Assert.Equal("myCookie=\"foo\"", cookie);
        }

        [Fact]
        public void RejectCookieValueWithSemicolon() => 
            Assert.Throws<ArgumentException>(() => ClientCookieEncoder.StrictEncoder.Encode(new DefaultCookie("myCookie", "foo;bar")));
    }
}
