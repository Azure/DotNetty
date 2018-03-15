// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Channel
{
    using DotNetty.Transport.Channels;
    using Xunit;

    public class DefaulChannelIdTest
    {
        [Fact]
        public void TestShortText()
        {
            string text = DefaultChannelId.NewInstance().AsShortText();
            Assert.Matches(@"^[0-9a-f]{8}$", text);
        }

        [Fact]
        public void TestLongText()
        {
            string text = DefaultChannelId.NewInstance().AsLongText();
            Assert.Matches(@"^[0-9a-f]{16}-[0-9a-f]{8}-[0-9a-f]{8}-[0-9a-f]{16}-[0-9a-f]{8}$", text);
        }

        [Fact]
        public void TestIdempotentMachineId()
        {
            string a = DefaultChannelId.NewInstance().AsLongText().Substring(0, 8);
            string b = DefaultChannelId.NewInstance().AsLongText().Substring(0, 8);
            Assert.Equal(a, b);
        }

        [Fact]
        public void TestIdempotentProcessId()
        {
            string a = DefaultChannelId.NewInstance().AsLongText().Substring(9, 4);
            string b = DefaultChannelId.NewInstance().AsLongText().Substring(9, 4);
            Assert.Equal(a, b);
        }
    }
}