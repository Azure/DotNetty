// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System.Text;
    using Xunit;

    public sealed class QueryStringEncoderTest
    {
        [Fact]
        public void DefaultEncoding()
        {
            var e = new QueryStringEncoder("/foo");
            e.AddParam("a", "b=c");
            Assert.Equal("/foo?a=b%3Dc", e.ToString());

            e = new QueryStringEncoder("/foo/\u00A5");
            e.AddParam("a", "\u00A5");
            Assert.Equal("/foo/\u00A5?a=%C2%A5", e.ToString());

            e = new QueryStringEncoder("/foo");
            e.AddParam("a", "1");
            e.AddParam("b", "2");
            Assert.Equal("/foo?a=1&b=2", e.ToString());

            e = new QueryStringEncoder("/foo");
            e.AddParam("a", "1");
            e.AddParam("b", "");
            e.AddParam("c", null);
            e.AddParam("d", null);
            Assert.Equal("/foo?a=1&b=&c&d", e.ToString());
        }

        [Fact]
        public void NonDefaultEncoding()
        {
            var e = new QueryStringEncoder("/foo/\u00A5", Encoding.BigEndianUnicode);
            e.AddParam("a", "\u00A5");

            //
            // Note that java emits endianess byte order mark results 
            // automatically, therefore the result is:
            //
            // %FE%FF%00%A5.
            //
            // .NET does not do this automatically by GetPreamble() method
            // and manually write to results, therefore the result is:
            //
            // %00%A5
            //
            // URL query strings do not need to encode this

            Assert.Equal("/foo/\u00A5?a=%00%A5", e.ToString());
        }

        [Fact]
        public void WhitespaceEncoding()
        {
            var e = new QueryStringEncoder("/foo");
            e.AddParam("a", "b c");
            Assert.Equal("/foo?a=b%20c", e.ToString());
        }
    }
}
