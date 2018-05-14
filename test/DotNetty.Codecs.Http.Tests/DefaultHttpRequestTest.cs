// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using DotNetty.Common.Utilities;
    using Xunit;

    public sealed class DefaultHttpRequestTest
    {
        [Fact]
        public void HeaderRemoval()
        {
            var m = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Get, "/");
            HttpHeaders h = m.Headers;

            // Insert sample keys.
            for (int i = 0; i < 1000; i++)
            {
                h.Set(HttpHeadersTestUtils.Of(i.ToString()), AsciiString.Empty);
            }

            // Remove in reversed order.
            for (int i = 999; i >= 0; i--)
            {
                h.Remove(HttpHeadersTestUtils.Of(i.ToString()));
            }

            // Check if random access returns nothing.
            for (int i = 0; i < 1000; i++)
            {
                Assert.False(h.TryGet(HttpHeadersTestUtils.Of(i.ToString()), out _));
            }

            // Check if sequential access returns nothing.
            Assert.True(h.IsEmpty);
        }
    }
}
