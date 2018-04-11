// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Xunit;

    public sealed class QueryStringDecoderTest
    {
        [Fact]
        public void BasicUris()
        {
            var d = new QueryStringDecoder("http://localhost/path");
            Assert.Equal(0, d.Parameters.Count);
        }

        [Fact]
        public void Basic()
        {
            var d = new QueryStringDecoder("/foo");
            Assert.Equal("/foo", d.Path);
            Assert.Equal(0, d.Parameters.Count);

            d = new QueryStringDecoder("/foo%20bar");
            Assert.Equal("/foo bar", d.Path);
            Assert.Equal(0, d.Parameters.Count);

            d = new QueryStringDecoder("/foo?a=b=c");
            Assert.Equal("/foo", d.Path);
            Assert.Equal(1, d.Parameters.Count);
            Assert.Single(d.Parameters["a"]);
            Assert.Equal("b=c", d.Parameters["a"][0]);

            d = new QueryStringDecoder("/foo?a=1&a=2");
            Assert.Equal("/foo", d.Path);
            Assert.Equal(1, d.Parameters.Count);
            Assert.Equal(2, d.Parameters["a"].Count);
            Assert.Equal("1", d.Parameters["a"][0]);
            Assert.Equal("2", d.Parameters["a"][1]);

            d = new QueryStringDecoder("/foo%20bar?a=1&a=2");
            Assert.Equal("/foo bar", d.Path);
            Assert.Equal(1, d.Parameters.Count);
            Assert.Equal(2, d.Parameters["a"].Count);
            Assert.Equal("1", d.Parameters["a"][0]);
            Assert.Equal("2", d.Parameters["a"][1]);

            d = new QueryStringDecoder("/foo?a=&a=2");
            Assert.Equal("/foo", d.Path);
            Assert.Equal(1, d.Parameters.Count);
            Assert.Equal(2, d.Parameters["a"].Count);
            Assert.Equal("", d.Parameters["a"][0]);
            Assert.Equal("2", d.Parameters["a"][1]);

            d = new QueryStringDecoder("/foo?a=1&a=");
            Assert.Equal("/foo", d.Path);
            Assert.Equal(1, d.Parameters.Count);
            Assert.Equal(2, d.Parameters["a"].Count);
            Assert.Equal("1", d.Parameters["a"][0]);
            Assert.Equal("", d.Parameters["a"][1]);

            d = new QueryStringDecoder("/foo?a=1&a=&a=");
            Assert.Equal("/foo", d.Path);
            Assert.Equal(1, d.Parameters.Count);
            Assert.Equal(3, d.Parameters["a"].Count);
            Assert.Equal("1", d.Parameters["a"][0]);
            Assert.Equal("", d.Parameters["a"][1]);
            Assert.Equal("", d.Parameters["a"][2]);

            d = new QueryStringDecoder("/foo?a=1=&a==2");
            Assert.Equal("/foo", d.Path);
            Assert.Equal(1, d.Parameters.Count);
            Assert.Equal(2, d.Parameters["a"].Count);
            Assert.Equal("1=", d.Parameters["a"][0]);
            Assert.Equal("=2", d.Parameters["a"][1]);

            d = new QueryStringDecoder("/foo?abc=1%2023&abc=124%20");
            Assert.Equal("/foo", d.Path);
            Assert.Equal(1, d.Parameters.Count);
            Assert.Equal(2, d.Parameters["abc"].Count);
            Assert.Equal("1 23", d.Parameters["abc"][0]);
            Assert.Equal("124 ", d.Parameters["abc"][1]);
        }

        [Fact]
        public void Exotic()
        {
            AssertQueryString("", "");
            AssertQueryString("foo", "foo");
            AssertQueryString("foo", "foo?");
            AssertQueryString("/foo", "/foo?");
            AssertQueryString("/foo", "/foo");
            AssertQueryString("?a=", "?a");
            AssertQueryString("foo?a=", "foo?a");
            AssertQueryString("/foo?a=", "/foo?a");
            AssertQueryString("/foo?a=", "/foo?a&");
            AssertQueryString("/foo?a=", "/foo?&a");
            AssertQueryString("/foo?a=", "/foo?&a&");
            AssertQueryString("/foo?a=", "/foo?&=a");
            AssertQueryString("/foo?a=", "/foo?=a&");
            AssertQueryString("/foo?a=", "/foo?a=&");
            AssertQueryString("/foo?a=b&c=d", "/foo?a=b&&c=d");
            AssertQueryString("/foo?a=b&c=d", "/foo?a=b&=&c=d");
            AssertQueryString("/foo?a=b&c=d", "/foo?a=b&==&c=d");
            AssertQueryString("/foo?a=b&c=&x=y", "/foo?a=b&c&x=y");
            AssertQueryString("/foo?a=", "/foo?a=");
            AssertQueryString("/foo?a=", "/foo?&a=");
            AssertQueryString("/foo?a=b&c=d", "/foo?a=b&c=d");
            AssertQueryString("/foo?a=1&a=&a=", "/foo?a=1&a&a=");
        }

        [Fact]
        public void PathSpecific()
        {
            // decode escaped characters
            Assert.Equal("/foo bar/", new QueryStringDecoder("/foo%20bar/?").Path);
            Assert.Equal("/foo\r\n\\bar/", new QueryStringDecoder("/foo%0D%0A\\bar/?").Path);

            // a 'fragment' after '#' should be cuted (see RFC 3986)
            Assert.Equal("", new QueryStringDecoder("#123").Path);
            Assert.Equal("foo", new QueryStringDecoder("foo?bar#anchor").Path);
            Assert.Equal("/foo-bar", new QueryStringDecoder("/foo-bar#anchor").Path);
            Assert.Equal("/foo-bar", new QueryStringDecoder("/foo-bar#a#b?c=d").Path);

            // '+' is not escape ' ' for the path
            Assert.Equal("+", new QueryStringDecoder("+").Path);
            Assert.Equal("/foo+bar/", new QueryStringDecoder("/foo+bar/?").Path);
            Assert.Equal("/foo++", new QueryStringDecoder("/foo++?index.php").Path);
            Assert.Equal("/foo +", new QueryStringDecoder("/foo%20+?index.php").Path);
            Assert.Equal("/foo+ ", new QueryStringDecoder("/foo+%20").Path);
        }

        [Fact]
        public void ExcludeFragment()
        {
            // a 'fragment' after '#' should be cuted (see RFC 3986)
            Assert.Equal("a", new QueryStringDecoder("?a#anchor").Parameters.Keys.ElementAt(0));
            Assert.Equal("b", new QueryStringDecoder("?a=b#anchor").Parameters["a"][0]);
            Assert.True(new QueryStringDecoder("?#").Parameters.Count == 0);
            Assert.True(new QueryStringDecoder("?#anchor").Parameters.Count == 0);
            Assert.True(new QueryStringDecoder("#?a=b#anchor").Parameters.Count == 0);
            Assert.True(new QueryStringDecoder("?#a=b#anchor").Parameters.Count == 0);
        }

        [Fact]
        public void HashDos()
        {
            var buf = new StringBuilder();
            buf.Append('?');
            for (int i = 0; i < 65536; i++)
            {
                buf.Append('k');
                buf.Append(i);
                buf.Append("=v");
                buf.Append(i);
                buf.Append('&');
            }

            var d = new QueryStringDecoder(buf.ToString());
            IDictionary<string, List<string>> parameters = d.Parameters;
            Assert.Equal(1024, parameters.Count);
        }

        [Fact]
        public void HasPath()
        {
            var d = new QueryStringDecoder("1=2", false);
            Assert.Equal("", d.Path);
            IDictionary<string, List<string>> parameters = d.Parameters;
            Assert.Equal(1, parameters.Count);
            Assert.True(parameters.ContainsKey("1"));
            List<string> param = parameters["1"];
            Assert.NotNull(param);
            Assert.Single(param);
            Assert.Equal("2", param[0]);
        }

        [Fact]
        public void UrlDecoding()
        {
            string caffe = new string(
                // "Caffé" but instead of putting the literal E-acute in the
                // source file, we directly use the UTF-8 encoding so as to
                // not rely on the platform's default encoding (not portable).
                new [] { 'C', 'a', 'f', 'f', '\u00E9' /* C3 A9 */ });

            string[] tests =
            {
                // Encoded   ->   Decoded or error message substring
                "",               "",
                "foo",            "foo",
                "f+o",            "f o",
                "f++",            "f  ",
                "fo%",            "unterminated escape sequence at index 2 of: fo%",
                "%42",            "B",
                "%5f",            "_",
                "f%4",            "unterminated escape sequence at index 1 of: f%4",
                "%x2",            "invalid hex byte 'x2' at index 1 of '%x2'",
                "%4x",            "invalid hex byte '4x' at index 1 of '%4x'",
                "Caff%C3%A9",     caffe,
                "случайный праздник",               "случайный праздник",
                "случайный%20праздник",             "случайный праздник",
                "случайный%20праздник%20%E2%98%BA", "случайный праздник ☺",
            };

            for (int i = 0; i < tests.Length; i += 2)
            {
                string encoded = tests[i];
                string expected = tests[i + 1];
                try
                {
                    string decoded = QueryStringDecoder.DecodeComponent(encoded);
                    Assert.Equal(expected, decoded);
                }
                catch (ArgumentException e)
                {
                    Assert.Equal(expected, e.Message);
                }
            }
        }

        static void AssertQueryString(string expected, string actual)
        {
            var ed = new QueryStringDecoder(expected);
            var ad = new QueryStringDecoder(actual);
            Assert.Equal(ed.Path, ad.Path);

            IDictionary<string, List<string>> edParams = ed.Parameters;
            IDictionary<string, List<string>> adParams = ad.Parameters;
            Assert.Equal(edParams.Count, adParams.Count);

            foreach (string name in edParams.Keys)
            {
                List<string> expectedValues = edParams[name];

                Assert.True(adParams.ContainsKey(name));
                List<string> values = adParams[name];
                Assert.Equal(expectedValues.Count, values.Count);

                foreach (string value in expectedValues)
                {
                    Assert.Contains(value, values);
                }
            }
        }

        // See #189
        [Fact]
        public void UrlString()
        {
            var uri = new Uri("http://localhost:8080/foo?param1=value1&param2=value2&param3=value3");
            var d = new QueryStringDecoder(uri);
            Assert.Equal("/foo", d.Path);
            IDictionary<string, List<string> > parameters = d.Parameters;
            Assert.Equal(3, parameters.Count);

            KeyValuePair<string, List<string>> entry = parameters.ElementAt(0);
            Assert.Equal("param1", entry.Key);
            Assert.Single(entry.Value);
            Assert.Equal("value1", entry.Value[0]);

            entry = parameters.ElementAt(1);
            Assert.Equal("param2", entry.Key);
            Assert.Single(entry.Value);
            Assert.Equal("value2", entry.Value[0]);

            entry = parameters.ElementAt(2);
            Assert.Equal("param3", entry.Key);
            Assert.Single(entry.Value);
            Assert.Equal("value3", entry.Value[0]);
        }

        // See #189
        [Fact]
        public void UriSlashPath()
        {
            var uri = new Uri("http://localhost:8080/?param1=value1&param2=value2&param3=value3");
            var d = new QueryStringDecoder(uri);
            Assert.Equal("/", d.Path);
            IDictionary<string, List<string>> parameters = d.Parameters;
            Assert.Equal(3, parameters.Count);

            KeyValuePair<string, List<string>> entry = parameters.ElementAt(0);
            Assert.Equal("param1", entry.Key);
            Assert.Single(entry.Value);
            Assert.Equal("value1", entry.Value[0]);

            entry = parameters.ElementAt(1);
            Assert.Equal("param2", entry.Key);
            Assert.Single(entry.Value);
            Assert.Equal("value2", entry.Value[0]);

            entry = parameters.ElementAt(2);
            Assert.Equal("param3", entry.Key);
            Assert.Single(entry.Value);
            Assert.Equal("value3", entry.Value[0]);
        }

        // See #189
        [Fact]
        public void UriNoPath()
        {
            var uri = new Uri("http://localhost:8080?param1=value1&param2=value2&param3=value3");
            var d = new QueryStringDecoder(uri);
            // The path component cannot be empty string, 
            // if there are no path component, it shoudl be '/' as above UriSlashPath test
            Assert.Equal("/", d.Path);
            IDictionary<string, List<string>> parameters = d.Parameters;
            Assert.Equal(3, parameters.Count);

            KeyValuePair<string, List<string>> entry = parameters.ElementAt(0);
            Assert.Equal("param1", entry.Key);
            Assert.Single(entry.Value);
            Assert.Equal("value1", entry.Value[0]);

            entry = parameters.ElementAt(1);
            Assert.Equal("param2", entry.Key);
            Assert.Single(entry.Value);
            Assert.Equal("value2", entry.Value[0]);

            entry = parameters.ElementAt(2);
            Assert.Equal("param3", entry.Key);
            Assert.Single(entry.Value);
            Assert.Equal("value3", entry.Value[0]);
        }

        // See https://github.com/netty/netty/issues/1833
        [Fact]
        public void Uri2()
        {
            var uri = new Uri("http://foo.com/images;num=10?query=name;value=123");
            var d = new QueryStringDecoder(uri);
            Assert.Equal("/images;num=10", d.Path);
            IDictionary<string, List<string>> parameters = d.Parameters;
            Assert.Equal(2, parameters.Count);

            KeyValuePair<string, List<string>> entry = parameters.ElementAt(0);
            Assert.Equal("query", entry.Key);
            Assert.Single(entry.Value);
            Assert.Equal("name", entry.Value[0]);

            entry = parameters.ElementAt(1);
            Assert.Equal("value", entry.Key);
            Assert.Single(entry.Value);
            Assert.Equal("123", entry.Value[0]);
        }

        [Fact]
        public void EmptyStrings()
        {
            var pathSlash = new QueryStringDecoder("path/");
            Assert.Equal("path/", pathSlash.RawPath());
            Assert.Equal("", pathSlash.RawQuery());
            var pathQuestion = new QueryStringDecoder("path?");
            Assert.Equal("path", pathQuestion.RawPath());
            Assert.Equal("", pathQuestion.RawQuery());
            var empty = new QueryStringDecoder("");
            Assert.Equal("", empty.RawPath());
            Assert.Equal("", empty.RawQuery());
        }
    }
}
