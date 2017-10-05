// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Tests
{
    using System;
    using Xunit;

    public sealed class DateFormatterTest
    {
        // This date is set at "06 Nov 1994 08:49:37 GMT", from
        // <a href="http://www.w3.org/Protocols/rfc2616/rfc2616-sec3.html">examples in RFC documentation</a>
        readonly DateTime expectedTime = new DateTime(1994, 11, 6, 8, 49, 37, DateTimeKind.Utc);

        [Fact]
        public void ParseWithSingleDigitDay()
        {
            Assert.Equal(this.expectedTime, DateFormatter.ParseHttpDate("Sun, 6 Nov 1994 08:49:37 GMT"));
        }

        [Fact]
        public void ParseWithDoubleDigitDay()
        {
            Assert.Equal(this.expectedTime, DateFormatter.ParseHttpDate("Sun, 06 Nov 1994 08:49:37 GMT"));
        }

        [Fact]
        public void ParseWithDashSeparatorSingleDigitDay()
        {
            Assert.Equal(this.expectedTime, DateFormatter.ParseHttpDate("Sunday, 06-Nov-94 08:49:37 GMT"));
        }

        [Fact]
        public void ParseWithSingleDoubleDigitDay()
        {
            Assert.Equal(this.expectedTime, DateFormatter.ParseHttpDate("Sunday, 6-Nov-94 08:49:37 GMT"));
        }

        [Fact]
        public void ParseWithoutGmt()
        {
            Assert.Equal(this.expectedTime, DateFormatter.ParseHttpDate("Sun Nov 6 08:49:37 1994"));
        }

        [Fact]
        public void ParseWithFunkyTimezone()
        {
            Assert.Equal(this.expectedTime, DateFormatter.ParseHttpDate("Sun Nov 6 08:49:37 1994 -0000"));
        }

        [Fact]
        public void ParseWithSingleDigitHourMinutesAndSecond()
        {
            Assert.Equal(this.expectedTime, DateFormatter.ParseHttpDate("Sunday, 6-Nov-94 8:49:37 GMT"));
        }

        [Fact]
        public void ParseWithSingleDigitTime()
        {
            Assert.Equal(this.expectedTime, DateFormatter.ParseHttpDate("Sunday, 6 Nov 1994 8:49:37 GMT"));

            DateTime time080937 = this.expectedTime - TimeSpan.FromMilliseconds(40 * 60 * 1000);
            Assert.Equal(time080937, DateFormatter.ParseHttpDate("Sunday, 6 Nov 1994 8:9:37 GMT"));
            Assert.Equal(time080937, DateFormatter.ParseHttpDate("Sunday, 6 Nov 1994 8:09:37 GMT"));

            DateTime time080907 = this.expectedTime - TimeSpan.FromMilliseconds((40 * 60 + 30) * 1000);
            Assert.Equal(time080907, DateFormatter.ParseHttpDate("Sunday, 6 Nov 1994 8:9:7 GMT"));
            Assert.Equal(time080907, DateFormatter.ParseHttpDate("Sunday, 6 Nov 1994 8:9:07 GMT"));
        }

        [Fact]
        public void ParseMidnight()
        {
            Assert.Equal(new DateTime(1994, 11, 6, 0, 0, 0, DateTimeKind.Utc), DateFormatter.ParseHttpDate("Sunday, 6 Nov 1994 00:00:00 GMT"));
        }

        [Fact]
        public void ParseInvalidInput()
        {
            // missing field
            Assert.Null(DateFormatter.ParseHttpDate("Sun, Nov 1994 08:49:37 GMT"));
            Assert.Null(DateFormatter.ParseHttpDate("Sun, 6 1994 08:49:37 GMT"));
            Assert.Null(DateFormatter.ParseHttpDate("Sun, 6 Nov 08:49:37 GMT"));
            Assert.Null(DateFormatter.ParseHttpDate("Sun, 6 Nov 1994 :49:37 GMT"));
            Assert.Null(DateFormatter.ParseHttpDate("Sun, 6 Nov 1994 49:37 GMT"));
            Assert.Null(DateFormatter.ParseHttpDate("Sun, 6 Nov 1994 08::37 GMT"));
            Assert.Null(DateFormatter.ParseHttpDate("Sun, 6 Nov 1994 08:37 GMT"));
            Assert.Null(DateFormatter.ParseHttpDate("Sun, 6 Nov 1994 08:49: GMT"));
            Assert.Null(DateFormatter.ParseHttpDate("Sun, 6 Nov 1994 08:49 GMT"));
            //invalid value
            Assert.Null(DateFormatter.ParseHttpDate("Sun, 6 FOO 1994 08:49:37 GMT"));
            Assert.Null(DateFormatter.ParseHttpDate("Sun, 36 Nov 1994 08:49:37 GMT"));
            Assert.Null(DateFormatter.ParseHttpDate("Sun, 6 Nov 1994 28:49:37 GMT"));
            Assert.Null(DateFormatter.ParseHttpDate("Sun, 6 Nov 1994 08:69:37 GMT"));
            Assert.Null(DateFormatter.ParseHttpDate("Sun, 6 Nov 1994 08:49:67 GMT"));
            //wrong number of digits in timestamp
            Assert.Null(DateFormatter.ParseHttpDate("Sunday, 6 Nov 1994 0:0:000 GMT"));
            Assert.Null(DateFormatter.ParseHttpDate("Sunday, 6 Nov 1994 0:000:0 GMT"));
            Assert.Null(DateFormatter.ParseHttpDate("Sunday, 6 Nov 1994 000:0:0 GMT"));
        }

        [Fact]
        public void Format()
        {
            Assert.Equal("Sun, 6 Nov 1994 08:49:37 GMT", DateFormatter.Format(this.expectedTime));
        }
    }
}
