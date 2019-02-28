// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.Cookies
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using DotNetty.Codecs.Http.Cookies;
    using Xunit;

    public sealed class ServerCookieEncoderTest
    {
        [Fact]
        public void EncodingSingleCookieV0()
        {
            const int MaxAge = 50;
            const string Result = "myCookie=myValue; Max-Age=50; Expires=(.+?); Path=/apathsomewhere; Domain=.adomainsomewhere; Secure";

            var cookie = new DefaultCookie("myCookie", "myValue")
            {
                Domain = ".adomainsomewhere",
                MaxAge = MaxAge,
                Path = "/apathsomewhere",
                IsSecure = true
            };
            string encodedCookie = ServerCookieEncoder.StrictEncoder.Encode(cookie);

            var regex = new Regex(Result, RegexOptions.Compiled);
            MatchCollection matches = regex.Matches(encodedCookie);
            Assert.Single(matches);

            Match match = matches[0];
            Assert.NotNull(match);
             
            DateTime? expiresDate = DateFormatter.ParseHttpDate(match.Groups[1].Value);
            Assert.True(expiresDate.HasValue, $"Parse http date failed : {match.Groups[1].Value}");
            long diff = (expiresDate.Value.Ticks - DateTime.UtcNow.Ticks) / TimeSpan.TicksPerSecond;
            // 2 secs should be fine
            Assert.True(Math.Abs(diff - MaxAge) <= 2, $"Expecting difference of MaxAge < 2s, but was {diff}s (MaxAge={MaxAge})");
        }

        [Fact]
        public void EncodingWithNoCookies()
        {
            string encodedCookie1 = ClientCookieEncoder.StrictEncoder.Encode(default(ICookie[]));
            IList<string> encodedCookie2 = ServerCookieEncoder.StrictEncoder.Encode(default(ICookie[]));

            Assert.Null(encodedCookie1);
            Assert.NotNull(encodedCookie2);
            Assert.Empty(encodedCookie2);
        }

        [Fact]
        public void EncodingMultipleCookiesStrict()
        {
            var result = new List<string>
            {
                "cookie2=value2",
                "cookie1=value3"
            };
            ICookie cookie1 = new DefaultCookie("cookie1", "value1");
            ICookie cookie2 = new DefaultCookie("cookie2", "value2");
            ICookie cookie3 = new DefaultCookie("cookie1", "value3");

            IList<string> encodedCookies = ServerCookieEncoder.StrictEncoder.Encode(cookie1, cookie2, cookie3);
            Assert.Equal(result, encodedCookies);
        }

        [Fact]
        public void IllegalCharInCookieNameMakesStrictEncoderThrowsException()
        {
            var illegalChars = new HashSet<char>();

            // CTLs
            for (int i = 0x00; i <= 0x1F; i++)
            {
                illegalChars.Add((char)i);
            }
            illegalChars.Add((char)0x7F);

            var separaters = new []
            {
                '(', ')', '<', '>', '@', ',', ';', ':', '\\', '"', '/', '[', ']',
                '?', '=', '{', '}', ' ', '\t'
            };

            // separators
            foreach(char c in separaters)
            {
                illegalChars.Add(c);
            }

            foreach (char c in illegalChars)
            {
                Assert.Throws<ArgumentException>(() => ServerCookieEncoder.StrictEncoder.Encode(
                    new DefaultCookie("foo" + c + "bar", "value")));
            }
        }

        [Fact]
        public void IllegalCharInCookieValueMakesStrictEncoderThrowsException()
        {
            var illegalChars = new HashSet<char>();
            // CTLs
            for (int i = 0x00; i <= 0x1F; i++)
            {
                illegalChars.Add((char)i);
            }
            illegalChars.Add((char)0x7F);


            // whitespace, DQUOTE, comma, semicolon, and backslash
            var separaters = new[]
            {
                ' ', '"', ',', ';', '\\'
            };

            foreach(char c in separaters)
            {
                illegalChars.Add(c);
            }

            foreach (char c in illegalChars)
            {
                Assert.Throws<ArgumentException>(() => ServerCookieEncoder.StrictEncoder.Encode(
                    new DefaultCookie("name", "value" + c)));
            }
        }

        [Fact]
        public void EncodingMultipleCookiesLax()
        {
            var result = new List<string>
            {
                "cookie1=value1",
                "cookie2=value2",
                "cookie1=value3"
            };

            ICookie cookie1 = new DefaultCookie("cookie1", "value1");
            ICookie cookie2 = new DefaultCookie("cookie2", "value2");
            ICookie cookie3 = new DefaultCookie("cookie1", "value3");
            IList<string> encodedCookies = ServerCookieEncoder.LaxEncoder.Encode(cookie1, cookie2, cookie3);

            Assert.Equal(result, encodedCookies);
        }
    }
}
